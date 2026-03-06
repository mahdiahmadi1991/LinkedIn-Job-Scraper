(() => {
    const form = document.querySelector("[data-login-form]");
    if (!(form instanceof HTMLFormElement)) {
        return;
    }

    const setButtonLoading = window.appButtons?.setLoading ?? ((button, busy) => {
        if (!(button instanceof HTMLButtonElement)) {
            return;
        }

        button.disabled = busy;
    });

    const submitButton = form.querySelector("[data-login-submit-button]");
    const passwordInput = form.querySelector("[data-login-password-input]");
    const passwordPeekButton = form.querySelector("[data-login-password-peek]");

    form.addEventListener("submit", () => {
        setButtonLoading(submitButton, true, "Signing in...");
    });

    if (!(passwordInput instanceof HTMLInputElement) || !(passwordPeekButton instanceof HTMLButtonElement)) {
        return;
    }

    const setPasswordVisible = (visible) => {
        passwordInput.type = visible ? "text" : "password";
        passwordPeekButton.setAttribute("aria-pressed", visible ? "true" : "false");
    };

    const beginReveal = (event) => {
        event.preventDefault();
        setPasswordVisible(true);
    };

    const endReveal = () => {
        setPasswordVisible(false);
    };

    passwordPeekButton.addEventListener("pointerdown", beginReveal);
    passwordPeekButton.addEventListener("pointerup", endReveal);
    passwordPeekButton.addEventListener("pointercancel", endReveal);
    passwordPeekButton.addEventListener("pointerleave", endReveal);
    passwordPeekButton.addEventListener("blur", endReveal);

    passwordPeekButton.addEventListener("keydown", (event) => {
        if (event.key === " " || event.key === "Enter") {
            beginReveal(event);
        }
    });

    passwordPeekButton.addEventListener("keyup", (event) => {
        if (event.key === " " || event.key === "Enter") {
            endReveal();
        }
    });
})();
