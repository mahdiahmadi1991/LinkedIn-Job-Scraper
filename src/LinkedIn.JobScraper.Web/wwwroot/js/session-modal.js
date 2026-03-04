(() => {
    const modalElement = document.querySelector("[data-linked-in-session-modal]");
    const indicator = document.querySelector("[data-session-indicator]");
    const indicatorLabel = document.querySelector("[data-session-indicator-label]");
    const antiForgeryInput = document.querySelector("#linkedInSessionAntiForgeryForm input[name='__RequestVerificationToken']");

    if (!modalElement || !indicator || !indicatorLabel || !antiForgeryInput) {
        return;
    }

    const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
    const autoCaptureNote = modalElement.querySelector("[data-session-auto-capture-note]");
    const browserOpen = modalElement.querySelector("[data-session-browser-open]");
    const currentPage = modalElement.querySelector("[data-session-current-page]");
    const capturedAt = modalElement.querySelector("[data-session-captured-at]");
    const source = modalElement.querySelector("[data-session-source]");
    const statusLabel = modalElement.querySelector("[data-session-status-label]");
    const waitingPanel = modalElement.querySelector("[data-session-waiting]");
    const launchButton = modalElement.querySelector("[data-session-launch]");
    const captureButton = modalElement.querySelector("[data-session-capture]");
    const importCurlButton = modalElement.querySelector("[data-session-import-curl]");
    const curlTextInput = modalElement.querySelector("[data-session-curl-text]");
    const curlFeedbackNote = modalElement.querySelector("[data-session-curl-feedback]");
    const revokeButton = modalElement.querySelector("[data-session-revoke]");
    const stateUrl = modalElement.getAttribute("data-session-state-url") || "/LinkedInSession/State";
    const launchUrl = modalElement.getAttribute("data-session-launch-url") || "/LinkedInSession/Launch";
    const captureUrl = modalElement.getAttribute("data-session-capture-url") || "/LinkedInSession/Capture";
    const importCurlUrl = modalElement.getAttribute("data-session-import-curl-url") || "/LinkedInSession/ImportCurl";
    const verifyUrl = modalElement.getAttribute("data-session-verify-url") || "/LinkedInSession/Verify";
    const revokeUrl = modalElement.getAttribute("data-session-revoke-url") || "/LinkedInSession/Revoke";
    const showToast = window.appToast?.show ?? (() => {});
    const setButtonLoading = window.appButtons?.setLoading ?? (() => {});

    let autoCloseArmed = false;
    let autoVerifyInFlight = false;
    let pollTimer = null;

    const setBusy = (busy, activeButton = null, busyText = null) => {
        [launchButton, captureButton, importCurlButton, revokeButton].forEach((button) => {
            if (button) {
                if (button === activeButton) {
                    setButtonLoading(button, busy, busyText);
                } else {
                    button.disabled = busy;
                    if (!busy) {
                        setButtonLoading(button, false);
                    }
                }
            }
        });

        if (curlTextInput) {
            curlTextInput.disabled = busy;
        }
    };

    const setFeedback = (message, success, shouldToast) => {
        if (shouldToast && message) {
            showToast(message, success);
        }
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

    const setAutoCaptureMessage = (message, active) => {
        if (!autoCaptureNote) {
            return;
        }

        if (!message) {
            autoCaptureNote.classList.add("d-none");
            autoCaptureNote.textContent = "";
            autoCaptureNote.classList.remove("active");
            return;
        }

        autoCaptureNote.classList.remove("d-none", "active");
        if (active) {
            autoCaptureNote.classList.add("active");
        }
        autoCaptureNote.textContent = message;
    };

    const applyState = (state) => {
        if (!state) {
            return;
        }

        if (browserOpen) {
            browserOpen.textContent = state.browserOpen ? "Open" : "Closed";
        }

        if (currentPage) {
            currentPage.textContent = state.currentPageUrl || "Not available";
        }

        if (capturedAt) {
            capturedAt.textContent = state.storedSessionCapturedAtUtc
                ? new Date(state.storedSessionCapturedAtUtc).toISOString().replace("T", " ").replace(".000Z", " UTC")
                : "Not available";
        }

        if (source) {
            source.textContent = state.storedSessionSource || "Not available";
        }

        if (statusLabel) {
            statusLabel.textContent = state.sessionIndicatorLabel || "Missing";
        }

        if (launchButton) {
            launchButton.textContent = state.primaryActionLabel || "Connect Session";
        }

        if (captureButton) {
            captureButton.classList.toggle("d-none", !state.showManualCaptureAction);
        }

        if (revokeButton) {
            revokeButton.classList.toggle("d-none", !state.storedSessionAvailable || state.autoCaptureActive);
        }

        if (waitingPanel) {
            waitingPanel.classList.toggle("d-none", !state.autoCaptureActive);
        }

        indicatorLabel.textContent = state.sessionIndicatorLabel || "Missing";
        indicator.classList.remove("session-state-connected", "session-state-connecting", "session-state-missing");
        if (state.sessionIndicatorClass) {
            indicator.classList.add(state.sessionIndicatorClass);
        }

        setAutoCaptureMessage(state.autoCaptureStatusMessage, state.autoCaptureActive);

        if (autoCloseArmed && state.autoCaptureCompletedSuccessfully && state.storedSessionAvailable && !autoVerifyInFlight) {
            autoVerifyInFlight = true;
            void (async () => {
                try {
                    await postAction(verifyUrl, true);
                } finally {
                    autoVerifyInFlight = false;
                    autoCloseArmed = false;
                }
            })();
            return;
        }

        if (autoCloseArmed && state.autoCaptureCompletedSuccessfully && state.storedSessionAvailable) {
            autoCloseArmed = false;
            window.clearTimeout(pollTimer);
            pollTimer = null;
            modal.hide();
        }

        if (autoCloseArmed && !state.autoCaptureActive && !state.autoCaptureCompletedSuccessfully) {
            window.clearTimeout(pollTimer);
            pollTimer = null;
        }
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

    const postAction = async (path, closeOnSuccess, actionButton = null, busyText = null, formFields = null, shouldToast = true) => {
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
                const problemMessage = payload?.detail || payload?.title || "LinkedIn session action failed.";
                setFeedback(problemMessage, false, shouldToast);

                try {
                    await fetchState();
                } catch {
                }

                return {
                    success: false,
                    state: null,
                    message: problemMessage
                };
            }

            setFeedback(payload.message, payload.success, shouldToast);
            applyState(payload.state);

            if (closeOnSuccess && payload.success && payload.state?.storedSessionAvailable) {
                modal.hide();
            }

            return payload;
        } finally {
            setBusy(false);
        }
    };

    const startPolling = () => {
        window.clearTimeout(pollTimer);

        const tick = async () => {
            try {
                await fetchState();
            } catch {
            }

            if (autoCloseArmed) {
                pollTimer = window.setTimeout(tick, 2000);
            }
        };

        pollTimer = window.setTimeout(tick, 1500);
    };

    modalElement.addEventListener("show.bs.modal", () => {
        autoCloseArmed = false;
        autoVerifyInFlight = false;
        setCurlFeedback(null);
        void fetchState();
    });

    modalElement.addEventListener("hidden.bs.modal", () => {
        autoCloseArmed = false;
        autoVerifyInFlight = false;
        window.clearTimeout(pollTimer);
        pollTimer = null;
        setCurlFeedback(null);
    });

    if (launchButton) {
        launchButton.addEventListener("click", async () => {
            const payload = await postAction(launchUrl, false, launchButton, "Opening browser...");
            if (payload.success) {
                autoCloseArmed = true;
                startPolling();
            }
        });
    }

    if (captureButton) {
        captureButton.addEventListener("click", async () => {
            await postAction(captureUrl, true, captureButton, "Capturing session...");
        });
    }

    if (curlTextInput) {
        curlTextInput.addEventListener("input", () => {
            setCurlFeedback(null);
        });
    }

    if (importCurlButton && curlTextInput) {
        importCurlButton.addEventListener("click", async () => {
            const curlText = curlTextInput.value.trim();

            if (!curlText) {
                setCurlFeedback("Paste a LinkedIn Copy as cURL (bash) request first.", "danger");
                return;
            }

            setCurlFeedback("Validating the pasted cURL and checking the imported session...", "subtle");

            const payload = await postAction(
                importCurlUrl,
                false,
                importCurlButton,
                "Validating cURL...",
                {
                    curlText
                },
                false);

            setCurlFeedback(
                payload.message || (payload.success
                    ? "LinkedIn session was imported and verified."
                    : "LinkedIn cURL import failed."),
                payload.success ? "success" : "danger");
        });
    }

    if (revokeButton) {
        revokeButton.addEventListener("click", async () => {
            autoCloseArmed = false;
            await postAction(revokeUrl, false, revokeButton, "Revoking session...");
        });
    }
})();
