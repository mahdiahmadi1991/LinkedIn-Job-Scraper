(() => {
    const root = document.querySelector("[data-admin-openai-setup]");
    if (!(root instanceof HTMLElement)) {
        return;
    }

    const form = root.querySelector("[data-admin-openai-setup-form]");
    const saveButton = root.querySelector("[data-admin-openai-save-button]");
    const connectionCheckButton = root.querySelector("[data-admin-openai-connection-check]");
    const statusNote = root.querySelector("[data-admin-openai-connection-status-note]");
    const modelSelect = root.querySelector("[data-admin-openai-model-select]");
    const modelDescription = root.querySelector("[data-admin-openai-model-description]");
    const apiKeyValue = root.querySelector("[data-admin-openai-api-key-value]");
    const modelValue = root.querySelector("[data-admin-openai-model-value]");
    const baseUrlValue = root.querySelector("[data-admin-openai-base-url-value]");
    const concurrencyTokenInput = root.querySelector('input[name="OpenAiSetupForm.ConcurrencyToken"]');
    const openAiStatusElement = document.querySelector("[data-admin-openai-status]");
    const showToast = window.appToast?.show ?? (() => {});
    const setButtonLoading = window.appButtons?.setLoading ?? (() => {});
    const connectionUrl = connectionCheckButton?.getAttribute("data-admin-openai-connection-url");
    const antiForgeryInput = form?.querySelector('input[name="__RequestVerificationToken"]');

    if (!(form instanceof HTMLFormElement) || !(saveButton instanceof HTMLButtonElement)) {
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

    const createAjaxHeaders = () => {
        const headers = {
            "X-Requested-With": "XMLHttpRequest"
        };

        if (antiForgeryInput instanceof HTMLInputElement && antiForgeryInput.value) {
            headers.RequestVerificationToken = antiForgeryInput.value;
        }

        return headers;
    };

    const clearValidationErrors = () => {
        form.querySelectorAll("[data-valmsg-for]").forEach((element) => {
            if (element instanceof HTMLElement) {
                element.textContent = "";
            }
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
            if (!Array.isArray(messages) || messages.length === 0 || !key) {
                return;
            }

            const fieldKey = key.startsWith("OpenAiSetupForm.")
                ? key
                : `OpenAiSetupForm.${key}`;

            const validationMessage = form.querySelector(`[data-valmsg-for="${fieldKey}"]`);
            if (validationMessage instanceof HTMLElement) {
                validationMessage.textContent = String(messages[0]);
            }

            form.querySelectorAll(`[name="${fieldKey}"]`).forEach((element) => {
                element.classList.add("input-validation-error");
            });

            hasFieldErrors = true;
        });

        return hasFieldErrors;
    };

    const setConnectionStatus = (message, ready) => {
        if (!(statusNote instanceof HTMLElement)) {
            return;
        }

        statusNote.textContent = message;
        statusNote.classList.remove("success", "danger");
        statusNote.classList.add(ready ? "success" : "danger");
    };

    const applyConnectionPayload = (payload) => {
        if (!payload?.state) {
            const message = "OpenAI readiness response was invalid.";
            setConnectionStatus(message, false);
            showToast(message, false);
            return;
        }

        const message = payload.message || "OpenAI connection settings are configured and ready for scoring.";
        setConnectionStatus(message, !!payload.state.ready);

        if (apiKeyValue instanceof HTMLElement) {
            apiKeyValue.textContent = payload.state.apiKeyConfigured ? "Configured" : "Not configured";
        }

        if (modelValue instanceof HTMLElement) {
            modelValue.textContent = payload.state.model || "Not configured";
        }

        if (baseUrlValue instanceof HTMLElement) {
            baseUrlValue.textContent = payload.state.baseUrl || "https://api.openai.com/v1";
        }

        showToast(message, !!payload.state.ready);
    };

    const readInitialStatus = () => {
        if (!(openAiStatusElement instanceof HTMLElement)) {
            return;
        }

        const message = openAiStatusElement.dataset.statusMessage?.trim();
        if (!message) {
            return;
        }

        const success = String(openAiStatusElement.dataset.statusSucceeded).toLowerCase() === "true";
        showToast(message, success);
    };

    const bindModelDescription = () => {
        if (!(modelSelect instanceof HTMLSelectElement) || !(modelDescription instanceof HTMLElement)) {
            return;
        }

        const refreshDescription = () => {
            const selectedOption = modelSelect.selectedOptions?.[0];
            const description = selectedOption?.dataset?.modelDescription || "No model description is available.";
            modelDescription.textContent = description;
        };

        modelSelect.addEventListener("change", refreshDescription);
        refreshDescription();
    };

    if (connectionCheckButton instanceof HTMLButtonElement && connectionUrl) {
        connectionCheckButton.addEventListener("click", async () => {
            const idleText = connectionCheckButton.textContent;
            setButtonLoading(connectionCheckButton, true, "Checking readiness...");

            try {
                const response = await fetch(connectionUrl, {
                    method: "POST",
                    body: new FormData(form),
                    headers: createAjaxHeaders(),
                    credentials: "same-origin"
                });

                const payload = await tryReadProblem(response);
                if (!response.ok) {
                    if (applyValidationErrors(payload?.errors)) {
                        const message = payload?.detail || payload?.title || "OpenAI readiness check validation failed.";
                        setConnectionStatus(message, false);
                        showToast(message, false);
                        return;
                    }

                    const message = payload?.detail || payload?.title || "OpenAI readiness check failed.";
                    setConnectionStatus(message, false);
                    showToast(message, false);
                    return;
                }

                applyConnectionPayload(payload);
            } catch {
                const message = "OpenAI readiness check failed.";
                setConnectionStatus(message, false);
                showToast(message, false);
            } finally {
                setButtonLoading(connectionCheckButton, false, idleText);
            }
        });
    }

    form.addEventListener("submit", async (event) => {
        event.preventDefault();
        clearValidationErrors();

        const idleText = saveButton.textContent;
        setButtonLoading(saveButton, true, "Saving OpenAI setup...");

        try {
            const response = await fetch(form.action, {
                method: "POST",
                body: new FormData(form),
                headers: createAjaxHeaders(),
                credentials: "same-origin"
            });

            const payload = await tryReadProblem(response);
            if (!response.ok) {
                if (!applyValidationErrors(payload?.errors)) {
                    showToast(payload?.detail || payload?.title || "OpenAI setup save failed.", false);
                }

                return;
            }

            if (!payload?.success) {
                showToast("OpenAI setup save did not return a usable response.", false);
                return;
            }

            if (concurrencyTokenInput instanceof HTMLInputElement && payload.concurrencyToken) {
                concurrencyTokenInput.value = payload.concurrencyToken;
            }

            showToast(payload.message || "OpenAI runtime settings were saved.", true);
        } catch {
            showToast("OpenAI setup save failed.", false);
        } finally {
            setButtonLoading(saveButton, false, idleText);
        }
    });

    readInitialStatus();
    bindModelDescription();
})();
