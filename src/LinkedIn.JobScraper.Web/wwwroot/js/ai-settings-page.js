(() => {
    const form = document.querySelector("[data-ai-settings-form]");
    const pageShell = document.querySelector(".page-shell[data-page-status-message]");
    const saveButton = document.querySelector("[data-ai-settings-save-button]");
    const concurrencyTokenInput = document.querySelector('input[name="ConcurrencyToken"]');
    const showToast = window.appToast?.show ?? (() => {});
    const setButtonLoading = window.appButtons?.setLoading ?? (() => {});

    if (!form || !saveButton) {
        return;
    }

    const tryReadProblem = async (response) => {
        const contentType = response.headers.get("Content-Type") || "";
        if (!contentType.includes("application/problem+json") && !contentType.includes("application/json")) {
            return null;
        }

        try {
            return await response.json();
        } catch {
            return null;
        }
    };

    const clearValidationErrors = () => {
        form.querySelectorAll("[data-valmsg-for]").forEach((element) => {
            element.textContent = "";
        });

        form.querySelectorAll(".input-validation-error").forEach((element) => {
            element.classList.remove("input-validation-error");
        });
    };

    const applyValidationErrors = (errors) => {
        if (!errors || typeof errors !== "object") {
            return false;
        }

        let hasFieldErrors = false;

        Object.entries(errors).forEach(([key, messages]) => {
            if (!Array.isArray(messages) || messages.length === 0) {
                return;
            }

            if (!key) {
                return;
            }

            const validationMessage = form.querySelector(`[data-valmsg-for="${key}"]`);
            if (validationMessage) {
                validationMessage.textContent = String(messages[0]);
            }

            form.querySelectorAll(`[name="${key}"]`).forEach((element) => {
                element.classList.add("input-validation-error");
            });

            hasFieldErrors = true;
        });

        return hasFieldErrors;
    };

    const showInitialPageMessage = () => {
        const message = pageShell?.dataset.pageStatusMessage?.trim();
        if (!message) {
            return;
        }

        const success = String(pageShell?.dataset.pageStatusSuccess).toLowerCase() === "true";
        showToast(message, success);
    };

    form.addEventListener("submit", async (event) => {
        event.preventDefault();
        clearValidationErrors();

        const idleText = saveButton.textContent;
        setButtonLoading(saveButton, true, "Saving settings...");

        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                credentials: "same-origin"
            });

            const payload = await tryReadProblem(response);

            if (!response.ok) {
                if (!applyValidationErrors(payload?.errors)) {
                    showToast(payload?.detail || payload?.title || "Settings save failed.", false);
                }
                return;
            }

            if (!payload?.success) {
                showToast("Settings save did not return a usable response.", false);
                return;
            }

            if (concurrencyTokenInput && payload.concurrencyToken) {
                concurrencyTokenInput.value = payload.concurrencyToken;
            }

            clearValidationErrors();
            showToast(payload.message || "AI behavior settings were saved.", true);
        } catch {
            showToast("Settings save failed.", false);
        } finally {
            setButtonLoading(saveButton, false, idleText);
        }
    });

    showInitialPageMessage();
})();
