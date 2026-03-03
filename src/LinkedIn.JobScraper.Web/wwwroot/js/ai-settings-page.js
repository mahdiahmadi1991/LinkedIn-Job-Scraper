(() => {
    const form = document.querySelector("[data-ai-settings-form]");
    const button = document.querySelector("[data-ai-connection-check]");
    const pageShell = document.querySelector(".page-shell[data-page-status-message]");
    const statusNote = document.querySelector("[data-ai-connection-status-note]");
    const apiKeyValue = document.querySelector("[data-ai-connection-api-key-value]");
    const modelValue = document.querySelector("[data-ai-connection-model-value]");
    const baseUrlValue = document.querySelector("[data-ai-connection-base-url-value]");
    const saveButton = document.querySelector("[data-ai-settings-save-button]");
    const concurrencyTokenInput = document.querySelector('input[name="ConcurrencyToken"]');
    const connectionUrl = button?.getAttribute("data-ai-connection-url");
    const showToast = window.appToast?.show ?? (() => {});
    const setButtonLoading = window.appButtons?.setLoading ?? (() => {});

    if (!button || !statusNote || !apiKeyValue || !modelValue || !baseUrlValue || !form || !saveButton || !connectionUrl) {
        return;
    }

    const setStatus = (message, ready) => {
        statusNote.textContent = message;
        statusNote.classList.remove("success", "danger");
        statusNote.classList.add(ready ? "success" : "danger");
    };

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

    const showInitialPageMessage = () => {
        const message = pageShell?.dataset.pageStatusMessage?.trim();
        if (!message) {
            return;
        }

        const success = String(pageShell?.dataset.pageStatusSuccess).toLowerCase() === "true";
        showToast(message, success);
    };

    button.addEventListener("click", async () => {
        const idleText = button.textContent;
        setButtonLoading(button, true, "Checking readiness...");

        try {
            const response = await fetch(connectionUrl, {
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                credentials: "same-origin"
            });

            const payload = await tryReadProblem(response);

            if (!response.ok) {
                const message = payload?.detail || payload?.title || "AI connection check failed.";
                setStatus(message, false);
                showToast(message, false);
                return;
            }

            if (!payload?.state) {
                const message = "AI connection check did not return a usable response.";
                setStatus(message, false);
                showToast(message, false);
                return;
            }

            const message = payload.message || "OpenAI connection settings are configured and ready for scoring.";
            setStatus(message, !!payload.state.ready);
            showToast(message, !!payload.state.ready);
            apiKeyValue.textContent = payload.state.apiKeyConfigured ? "Configured" : "Not configured";
            modelValue.textContent = payload.state.model || "Not configured";
            baseUrlValue.textContent = payload.state.baseUrl || "https://api.openai.com/v1";
        } catch {
            const message = "AI connection check failed.";
            setStatus(message, false);
            showToast(message, false);
        } finally {
            setButtonLoading(button, false, idleText);
        }
    });

    form.addEventListener("submit", async (event) => {
        event.preventDefault();

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
                showToast(payload?.detail || payload?.title || "Settings save failed.", false);
                return;
            }

            if (!payload?.success) {
                showToast("Settings save did not return a usable response.", false);
                return;
            }

            if (concurrencyTokenInput && payload.concurrencyToken) {
                concurrencyTokenInput.value = payload.concurrencyToken;
            }

            showToast(payload.message || "AI behavior settings were saved.", true);
        } catch {
            showToast("Settings save failed.", false);
        } finally {
            setButtonLoading(saveButton, false, idleText);
        }
    });

    showInitialPageMessage();
})();
