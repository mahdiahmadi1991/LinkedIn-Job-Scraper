(() => {
    const form = document.getElementById("search-settings-form");
    if (!form) {
        return;
    }

    const pageShell = form.closest(".page-shell");
    const saveButton = form.querySelector("[data-search-settings-save-button]");
    const concurrencyTokenInput = form.querySelector('input[name="ConcurrencyToken"]');
    const geoIdInput = form.querySelector('input[name="LocationGeoId"]');
    const displayNameInput = form.querySelector('input[name="LocationDisplayName"]');
    const locationInput = form.querySelector('input[name="LocationInput"]');
    const suggestionButtons = form.querySelectorAll("[data-location-select]");
    const showToast = window.appToast?.show ?? (() => {});
    const setButtonLoading = window.appButtons?.setLoading ?? (() => {});

    if (!saveButton) {
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

    const showInitialPageMessage = () => {
        const message = pageShell?.dataset.pageStatusMessage?.trim();
        if (!message) {
            return;
        }

        const success = String(pageShell?.dataset.pageStatusSuccess).toLowerCase() === "true";
        showToast(message, success);
    };

    locationInput?.addEventListener("input", () => {
        if (geoIdInput) {
            geoIdInput.value = "";
        }

        if (displayNameInput) {
            displayNameInput.value = "";
        }
    });

    suggestionButtons.forEach((button) => {
        button.addEventListener("click", () => {
            const geoId = button.getAttribute("data-geo-id");
            const displayName = button.getAttribute("data-display-name");

            if (geoIdInput) {
                geoIdInput.value = geoId ?? "";
            }

            if (displayNameInput) {
                displayNameInput.value = displayName ?? "";
            }

            if (locationInput && displayName) {
                locationInput.value = displayName;
            }

            suggestionButtons.forEach((currentButton) => {
                currentButton.classList.remove("btn-primary");
                currentButton.classList.remove("btn-outline-primary");
                currentButton.classList.add("btn-outline-secondary");
            });

            button.classList.remove("btn-outline-secondary");
            button.classList.add("btn-primary");
        });
    });

    form.addEventListener("submit", async (event) => {
        const submitter = event.submitter;
        if (submitter && submitter.hasAttribute("data-search-settings-location-button")) {
            setButtonLoading(submitter, true, "Finding locations...");
            return;
        }

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

            showToast(payload.message || "Search settings were saved.", true);
        } catch {
            showToast("Settings save failed.", false);
        } finally {
            setButtonLoading(saveButton, false, idleText);
        }
    });

    showInitialPageMessage();
})();
