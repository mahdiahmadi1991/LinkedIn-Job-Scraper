(() => {
    const modalElement = document.querySelector("[data-linked-in-session-modal]");
    const indicator = document.querySelector("[data-session-indicator]");
    const indicatorLabel = document.querySelector("[data-session-indicator-label]");
    const antiForgeryInput = document.querySelector("#linkedInSessionAntiForgeryForm input[name='__RequestVerificationToken']");

    if (!modalElement || !indicator || !indicatorLabel || !antiForgeryInput) {
        return;
    }

    const showToast = window.appToast?.show ?? (() => {});
    const setButtonLoading = window.appButtons?.setLoading ?? (() => {});

    const stateUrl = modalElement.getAttribute("data-session-state-url") || "/LinkedInSession/State";
    const importCurlUrl = modalElement.getAttribute("data-session-import-curl-url") || "/LinkedInSession/ImportCurl";
    const revokeUrl = modalElement.getAttribute("data-session-revoke-url") || "/LinkedInSession/Revoke";

    const statusLabel = modalElement.querySelector("[data-session-status-label]");
    const capturedAt = modalElement.querySelector("[data-session-captured-at]");
    const source = modalElement.querySelector("[data-session-source]");
    const expiration = modalElement.querySelector("[data-session-expiration]");
    const resetNote = modalElement.querySelector("[data-session-reset-note]");
    const connectedPanel = modalElement.querySelector("[data-session-connected-panel]");
    const curlPanel = modalElement.querySelector("[data-session-curl-panel]");
    const startReplaceButton = modalElement.querySelector("[data-session-start-replace]");
    const cancelReplaceButton = modalElement.querySelector("[data-session-cancel-replace]");
    const importCurlButton = modalElement.querySelector("[data-session-import-curl]");
    const revokeButton = modalElement.querySelector("[data-session-revoke]");
    const curlTextInput = modalElement.querySelector("[data-session-curl-text]");
    const curlFeedbackNote = modalElement.querySelector("[data-session-curl-feedback]");
    const curlBrowserHint = modalElement.querySelector("[data-session-curl-browser-hint]");
    const curlGuideCards = modalElement.querySelectorAll("[data-session-curl-guide-card]");
    let replacingSession = false;
    let currentState = null;

    const resolveSessionIndicator = (state) => {
        if (state?.resetRequirement?.required) {
            return {
                label: "Reset Required",
                cssClass: "session-state-missing"
            };
        }

        if (state?.storedSessionAvailable) {
            return {
                label: "Connected",
                cssClass: "session-state-connected"
            };
        }

        return {
            label: "Missing",
            cssClass: "session-state-missing"
        };
    };

    const setResetMessage = (message) => {
        if (!resetNote) {
            return;
        }

        if (!message) {
            resetNote.classList.add("d-none");
            resetNote.textContent = "";
            return;
        }

        resetNote.classList.remove("d-none");
        resetNote.textContent = message;
    };

    const setCurlFeedback = (message, tone = "subtle") => {
        if (!curlFeedbackNote) {
            return;
        }

        curlFeedbackNote.classList.remove("d-none", "subtle", "success", "danger");

        if (!message) {
            curlFeedbackNote.textContent = "";
            curlFeedbackNote.classList.add("d-none");
            return;
        }

        curlFeedbackNote.textContent = message;
        curlFeedbackNote.classList.add(tone);
    };

    const setBusy = (busy, activeButton = null, busyText = null) => {
        [importCurlButton, revokeButton, startReplaceButton, cancelReplaceButton].forEach((button) => {
            if (!button) {
                return;
            }

            if (button === activeButton) {
                setButtonLoading(button, busy, busyText);
                return;
            }

            button.disabled = busy;
            if (!busy) {
                setButtonLoading(button, false);
            }
        });

        if (curlTextInput) {
            curlTextInput.disabled = busy;
        }
    };

    const formatUtcDateTime = (value) => {
        const fromSharedFormatter = window.appDateTime?.formatDateTime?.(value);
        if (fromSharedFormatter) {
            return fromSharedFormatter;
        }

        const parsed = new Date(value);
        if (Number.isNaN(parsed.getTime())) {
            return null;
        }

        return new Intl.DateTimeFormat(undefined, {
            dateStyle: "medium",
            timeStyle: "short"
        }).format(parsed);
    };

    const formatSessionSource = (sourceValue) => {
        if (!sourceValue) {
            return "Not available";
        }

        const normalized = String(sourceValue).trim();
        if (!normalized) {
            return "Not available";
        }

        if (normalized.toLowerCase() === "curlimport") {
            return "cURL Import";
        }

        return normalized;
    };

    const isResetRequiredState = (state) => Boolean(state?.resetRequirement?.required);

    const updatePanelVisibility = (state) => {
        const connectedAndHealthy = Boolean(state?.storedSessionAvailable) && !isResetRequiredState(state);
        const showConnectedPanel = connectedAndHealthy && !replacingSession;
        const showCurlPanel = !showConnectedPanel;

        if (connectedPanel) {
            connectedPanel.classList.toggle("d-none", !showConnectedPanel);
        }

        if (curlPanel) {
            curlPanel.classList.toggle("d-none", !showCurlPanel);
        }

        if (cancelReplaceButton) {
            cancelReplaceButton.classList.toggle("d-none", !(connectedAndHealthy && replacingSession));
        }
    };

    const buildResetRequiredMessage = (state) => {
        if (!isResetRequiredState(state)) {
            return null;
        }

        const reason = state?.resetRequirement?.message ||
            "LinkedIn refused requests with your stored session.";

        return `${reason} Reset Session, then import a fresh cURL request to continue.`;
    };

    const detectCurlGuideBrowserFamily = () => {
        const userAgent = navigator.userAgent || "";
        const lowerUserAgent = userAgent.toLowerCase();

        if (lowerUserAgent.includes("firefox")) {
            return "firefox";
        }

        if (
            lowerUserAgent.includes("edg/") ||
            lowerUserAgent.includes("chrome/") ||
            lowerUserAgent.includes("brave/") ||
            lowerUserAgent.includes("opr/")
        ) {
            return "chromium";
        }

        return "unknown";
    };

    const applyCurlGuideRecommendation = () => {
        if (!curlGuideCards?.length) {
            return;
        }

        const browserFamily = detectCurlGuideBrowserFamily();
        const preferredBrowserFamily = browserFamily === "unknown" ? "chromium" : browserFamily;

        curlGuideCards.forEach((card) => {
            const cardBrowserFamily = card.getAttribute("data-session-curl-guide-card");
            if (!cardBrowserFamily) {
                return;
            }

            card.open = cardBrowserFamily === preferredBrowserFamily;
        });

        if (!curlBrowserHint) {
            return;
        }

        if (browserFamily === "chromium") {
            curlBrowserHint.classList.remove("d-none");
            curlBrowserHint.textContent = "Recommended for your browser: follow Chrome/Edge/Brave steps.";
            return;
        }

        if (browserFamily === "firefox") {
            curlBrowserHint.classList.remove("d-none");
            curlBrowserHint.textContent = "Recommended for your browser: follow Firefox steps.";
            return;
        }

        curlBrowserHint.classList.add("d-none");
        curlBrowserHint.textContent = "";
    };

    const applyState = (state) => {
        if (!state) {
            return;
        }

        currentState = state;

        if (!state.storedSessionAvailable || isResetRequiredState(state)) {
            replacingSession = false;
        }

        const sessionIndicator = resolveSessionIndicator(state);
        indicatorLabel.textContent = sessionIndicator.label;
        indicator.classList.remove("session-state-connected", "session-state-connecting", "session-state-missing");
        indicator.classList.add(sessionIndicator.cssClass);

        if (statusLabel) {
            statusLabel.textContent = sessionIndicator.label;
        }

        if (capturedAt) {
            capturedAt.textContent = state.storedSessionCapturedAtUtc
                ? (formatUtcDateTime(state.storedSessionCapturedAtUtc) || "Not available")
                : "Not available";
        }

        if (source) {
            source.textContent = formatSessionSource(state.storedSessionSource);
        }

        if (expiration) {
            if (state.storedSessionEstimatedExpiresAtUtc) {
                const expiryValue = formatUtcDateTime(state.storedSessionEstimatedExpiresAtUtc) || "Unknown";

                expiration.textContent = state.storedSessionExpirySource
                    ? `${expiryValue} (${state.storedSessionExpirySource})`
                    : expiryValue;
            } else {
                expiration.textContent = "Unknown";
            }
        }

        if (revokeButton) {
            revokeButton.classList.toggle("d-none", !state.storedSessionAvailable);
        }

        updatePanelVisibility(state);
        setResetMessage(buildResetRequiredMessage(state));
        window.dispatchEvent(new CustomEvent("linkedinsession:state", { detail: state }));
    };

    const fetchState = async () => {
        const response = await fetch(stateUrl, {
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            },
            credentials: "same-origin"
        });

        if (!response.ok) {
            throw new Error("Could not refresh LinkedIn session state.");
        }

        const payload = await response.json();
        applyState(payload.state);
        return payload;
    };

    const tryReadJsonPayload = async (response) => {
        const contentType = response.headers.get("Content-Type") || "";
        if (!contentType.includes("application/json") && !contentType.includes("application/problem+json")) {
            return null;
        }

        try {
            return await response.json();
        } catch {
            return null;
        }
    };

    const postAction = async (path, actionButton = null, busyText = null, formFields = null, shouldToast = true) => {
        setBusy(true, actionButton, busyText);

        try {
            const requestBody = new URLSearchParams({
                "__RequestVerificationToken": antiForgeryInput.value
            });

            if (formFields) {
                Object.entries(formFields).forEach(([key, value]) => {
                    requestBody.append(key, value ?? "");
                });
            }

            const response = await fetch(path, {
                method: "POST",
                headers: {
                    "X-Requested-With": "XMLHttpRequest",
                    "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"
                },
                body: requestBody,
                credentials: "same-origin"
            });

            const payload = await tryReadJsonPayload(response);

            if (!response.ok) {
                const message = payload?.detail || payload?.title || "LinkedIn session action failed.";
                if (shouldToast) {
                    showToast(message, false);
                }

                try {
                    await fetchState();
                } catch {
                }

                return {
                    success: false,
                    message
                };
            }

            if (shouldToast && payload?.message) {
                showToast(payload.message, payload.success);
            }

            applyState(payload.state);
            return payload;
        } finally {
            setBusy(false);
        }
    };

    modalElement.addEventListener("show.bs.modal", () => {
        replacingSession = false;
        setCurlFeedback(null);
        applyCurlGuideRecommendation();
        void fetchState();
    });

    modalElement.addEventListener("hidden.bs.modal", () => {
        replacingSession = false;
        currentState = null;
        setCurlFeedback(null);
    });

    if (curlTextInput) {
        curlTextInput.addEventListener("input", () => {
            setCurlFeedback(null);
        });
    }

    if (importCurlButton && curlTextInput) {
        importCurlButton.addEventListener("click", async () => {
            const curlText = curlTextInput.value.trim();

            if (!curlText) {
                setCurlFeedback("Paste a LinkedIn Copy as cURL request first.", "danger");
                return;
            }

            setCurlFeedback("Validating the pasted cURL and checking the imported session...", "subtle");

            const payload = await postAction(
                importCurlUrl,
                importCurlButton,
                "Validating cURL...",
                {
                    curlText
                },
                false
            );

            if (payload.success) {
                replacingSession = false;
            }

            setCurlFeedback(
                payload.message || (payload.success
                    ? "LinkedIn session was imported and verified."
                    : "LinkedIn cURL import failed."),
                payload.success ? "success" : "danger"
            );
        });
    }

    if (startReplaceButton) {
        startReplaceButton.addEventListener("click", () => {
            replacingSession = true;
            updatePanelVisibility(currentState);
            setCurlFeedback("Paste a fresh LinkedIn cURL request to replace the current session.", "subtle");
            curlTextInput?.focus();
        });
    }

    if (cancelReplaceButton) {
        cancelReplaceButton.addEventListener("click", () => {
            replacingSession = false;
            updatePanelVisibility(currentState);
            setCurlFeedback(null);
        });
    }

    if (revokeButton) {
        revokeButton.addEventListener("click", async () => {
            replacingSession = false;
            setCurlFeedback(null);
            await postAction(revokeUrl, revokeButton, "Resetting session...", null, true);
        });
    }
})();
