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
    const locationAutocomplete = form.querySelector("[data-location-autocomplete]");
    const locationInput = form.querySelector("[data-location-input]");
    const suggestionsUrl = locationAutocomplete?.dataset.locationSuggestionsUrl;
    const suggestionsPanel = form.querySelector("[data-location-suggestions-panel]");
    const suggestionsList = form.querySelector("[data-location-suggestions-list]");
    const selectedSummary = form.querySelector("[data-location-selected-summary]");
    const selectedName = form.querySelector("[data-location-selected-name]");
    const selectedGeoId = form.querySelector("[data-location-selected-geo-id]");
    const showToast = window.appToast?.show ?? (() => {});
    const setButtonLoading = window.appButtons?.setLoading ?? (() => {});
    let suggestionRequestId = 0;
    let searchDebounceHandle = 0;

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

        updateSelectedSummary("", "");
        queueLocationSuggestions(locationInput.value);
    });

    locationInput?.addEventListener("focus", () => {
        if (!suggestionsPanel || !suggestionsList || suggestionsList.childElementCount === 0) {
            return;
        }

        suggestionsPanel.classList.remove("d-none");
    });

    locationInput?.addEventListener("blur", () => {
        window.setTimeout(() => {
            suggestionsPanel?.classList.add("d-none");
        }, 150);
    });

    suggestionsList?.addEventListener("mousedown", (event) => {
        const option = event.target instanceof Element
            ? event.target.closest("[data-location-option]")
            : null;
        if (!option) {
            return;
        }

        event.preventDefault();

        const geoId = option.getAttribute("data-geo-id") ?? "";
        const displayName = option.getAttribute("data-display-name") ?? "";

        if (geoIdInput) {
            geoIdInput.value = geoId;
        }

        if (displayNameInput) {
            displayNameInput.value = displayName;
        }

        if (locationInput) {
            locationInput.value = displayName;
        }

        updateSelectedSummary(displayName, geoId);
        clearSuggestions();
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

            showToast(payload.message || "Search settings were saved.", true);
        } catch {
            showToast("Settings save failed.", false);
        } finally {
            setButtonLoading(saveButton, false, idleText);
        }
    });

    function queueLocationSuggestions(query) {
        if (!suggestionsUrl || !locationInput) {
            return;
        }

        window.clearTimeout(searchDebounceHandle);

        const trimmedQuery = (query || "").trim();
        if (trimmedQuery.length < 2) {
            clearSuggestions();
            return;
        }

        searchDebounceHandle = window.setTimeout(() => {
            void fetchLocationSuggestions(trimmedQuery);
        }, 250);
    }

    async function fetchLocationSuggestions(query) {
        if (!suggestionsUrl) {
            return;
        }

        const requestId = ++suggestionRequestId;

        try {
            const separator = suggestionsUrl.includes("?") ? "&" : "?";
            const response = await fetch(`${suggestionsUrl}${separator}query=${encodeURIComponent(query)}`, {
                method: "GET",
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                credentials: "same-origin"
            });

            const payload = await tryReadProblem(response);

            if (requestId !== suggestionRequestId) {
                return;
            }

            if (!response.ok) {
                clearSuggestions();
                showToast(payload?.detail || payload?.title || "Location suggestions are unavailable right now.", false);
                return;
            }

            renderSuggestions(Array.isArray(payload?.suggestions) ? payload.suggestions : []);
        } catch {
            if (requestId !== suggestionRequestId) {
                return;
            }

            clearSuggestions();
            showToast("Location suggestions are unavailable right now.", false);
        }
    }

    function renderSuggestions(suggestions) {
        if (!suggestionsList || !suggestionsPanel) {
            return;
        }

        suggestionsList.replaceChildren();

        if (!suggestions.length) {
            const emptyState = document.createElement("div");
            emptyState.className = "search-location-empty";
            emptyState.textContent = "No LinkedIn location suggestions found.";
            suggestionsList.appendChild(emptyState);
            suggestionsPanel.classList.remove("d-none");
            return;
        }

        suggestions.forEach((suggestion) => {
            const option = document.createElement("button");
            option.type = "button";
            option.className = "search-location-option";
            option.setAttribute("data-location-option", "true");
            option.setAttribute("data-geo-id", suggestion.geoId || "");
            option.setAttribute("data-display-name", suggestion.displayName || "");
            option.innerHTML = `<span class="search-location-option-name">${escapeHtml(suggestion.displayName || "")}</span><span class="search-location-option-id">${escapeHtml(suggestion.geoId || "")}</span>`;
            suggestionsList.appendChild(option);
        });

        suggestionsPanel.classList.remove("d-none");
    }

    function clearSuggestions() {
        if (suggestionsList) {
            suggestionsList.replaceChildren();
        }

        suggestionsPanel?.classList.add("d-none");
    }

    function updateSelectedSummary(displayName, geoId) {
        if (!selectedSummary || !selectedName || !selectedGeoId) {
            return;
        }

        if (!displayName) {
            selectedSummary.classList.add("d-none");
            selectedName.textContent = "";
            selectedGeoId.textContent = "";
            return;
        }

        selectedName.textContent = displayName;
        selectedGeoId.textContent = geoId ? `(${geoId})` : "";
        selectedSummary.classList.remove("d-none");
    }

    function escapeHtml(value) {
        return String(value)
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll("\"", "&quot;")
            .replaceAll("'", "&#39;");
    }

    showInitialPageMessage();
})();
