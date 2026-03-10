(() => {
    const toastContainer = document.querySelector("[data-app-toast-container]");

    const show = (message, success = true) => {
        if (!toastContainer || !message) {
            return;
        }

        const toast = document.createElement("div");
        toast.className = "toast align-items-center border-0 app-toast-shell";
        toast.setAttribute("role", "status");
        toast.setAttribute("aria-live", "polite");
        toast.setAttribute("aria-atomic", "true");

        const shell = document.createElement("div");
        shell.className = `d-flex ${success ? "app-toast-success" : "app-toast-danger"}`;

        const body = document.createElement("div");
        body.className = "toast-body";
        body.textContent = message;

        const closeButton = document.createElement("button");
        closeButton.type = "button";
        closeButton.className = "btn-close btn-close-white me-2 m-auto";
        closeButton.setAttribute("data-bs-dismiss", "toast");
        closeButton.setAttribute("aria-label", "Close");

        shell.append(body, closeButton);
        toast.appendChild(shell);
        toastContainer.appendChild(toast);

        const instance = bootstrap.Toast.getOrCreateInstance(toast, { delay: 5000 });
        toast.addEventListener("hidden.bs.toast", () => toast.remove(), { once: true });
        instance.show();
    };

    window.appToast = { show };
})();

(() => {
    const setLoading = (button, busy, fallbackBusyText) => {
        if (!(button instanceof HTMLButtonElement)) {
            return;
        }

        if (busy) {
            button.dataset.originalHtml = button.innerHTML;

            const busyText = button.dataset.loadingText || fallbackBusyText || button.textContent?.trim() || "Working...";
            button.disabled = true;
            button.innerHTML = `
                <span class="button-loading-shell">
                    <span class="button-loading-spinner" aria-hidden="true"></span>
                    <span>${busyText}</span>
                </span>
            `;
            return;
        }

        button.disabled = false;
        if (button.dataset.originalHtml) {
            button.innerHTML = button.dataset.originalHtml;
        }
    };

    window.appButtons = {
        setLoading
    };
})();

(() => {
    const dateTimeFormatter = new Intl.DateTimeFormat(undefined, {
        dateStyle: "medium",
        timeStyle: "short"
    });
    const dateTimeWithSecondsFormatter = new Intl.DateTimeFormat(undefined, {
        dateStyle: "medium",
        timeStyle: "medium"
    });

    const parseDate = (value) => {
        if (!value) {
            return null;
        }

        const parsed = new Date(value);
        if (Number.isNaN(parsed.getTime())) {
            return null;
        }

        return parsed;
    };

    const formatDateTime = (value, includeSeconds = false) => {
        const parsed = parseDate(value);
        if (!parsed) {
            return null;
        }

        return includeSeconds
            ? dateTimeWithSecondsFormatter.format(parsed)
            : dateTimeFormatter.format(parsed);
    };

    const hydrateUtcDisplays = (scope = document) => {
        if (!(scope instanceof Document || scope instanceof Element)) {
            return;
        }

        const displays = scope.querySelectorAll("[data-utc-display]");
        for (const element of displays) {
            if (!(element instanceof HTMLElement)) {
                continue;
            }

            const utcValue = element.dataset.utcDisplay || "";
            const emptyValue = element.dataset.utcEmpty || "-";
            const formatted = formatDateTime(utcValue);
            element.textContent = formatted || emptyValue;
        }
    };

    window.appDateTime = {
        formatDateTime,
        hydrateUtcDisplays
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", () => {
            hydrateUtcDisplays(document);
        }, { once: true });
    } else {
        hydrateUtcDisplays(document);
    }
})();
