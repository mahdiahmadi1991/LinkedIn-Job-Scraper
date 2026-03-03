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

        const instance = bootstrap.Toast.getOrCreateInstance(toast, { delay: 4200 });
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
