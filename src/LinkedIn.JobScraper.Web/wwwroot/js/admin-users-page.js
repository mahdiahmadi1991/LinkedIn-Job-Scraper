(() => {
    const page = document.querySelector("[data-admin-users-page]");
    if (!(page instanceof HTMLElement)) {
        return;
    }

    const showToast = window.appToast?.show ?? (() => {});
    const setButtonLoading = window.appButtons?.setLoading ?? ((button, busy) => {
        if (!(button instanceof HTMLButtonElement)) {
            return;
        }

        button.disabled = busy;
    });
    const createForm = page.querySelector("[data-admin-user-create-form]");
    const updateActionPath = page.dataset.updateUserUrl || "/admin/users/update";
    const toggleActionPath = page.dataset.toggleUserUrl || "/admin/users/set-active-state";
    const softDeleteActionPath = page.dataset.softDeleteUserUrl || "/admin/users/soft-delete";
    const usersTableBody = page.querySelector("table tbody");
    const usersTableWrapper = page.querySelector("[data-admin-users-table-wrapper]");
    const usersEmptyState = page.querySelector("[data-admin-users-empty-state]");
    const paginationRoot = page.querySelector("[data-admin-users-pagination]");
    const paginationInfo = page.querySelector("[data-admin-users-page-info]");
    const paginationPrevButton = page.querySelector("[data-admin-users-prev-page]");
    const paginationNextButton = page.querySelector("[data-admin-users-next-page]");
    const usersPaginationState = {
        pageSize: 20,
        currentPage: 1
    };
    const softDeleteModalElement = document.querySelector("[data-admin-soft-delete-modal]");
    const softDeleteUserNameLabel = softDeleteModalElement?.querySelector("[data-admin-soft-delete-user-name]");
    const softDeleteConfirmButton = softDeleteModalElement?.querySelector("[data-admin-soft-delete-confirm]");
    const softDeleteCancelButton = softDeleteModalElement?.querySelector("[data-admin-soft-delete-cancel]");

    const localDateTimeFormatter = new Intl.DateTimeFormat(undefined, {
        dateStyle: "medium",
        timeStyle: "short"
    });

    const pad = (value) => String(value).padStart(2, "0");

    const toLocalDateTimeInputValue = (utcIsoValue) => {
        if (!utcIsoValue) {
            return "";
        }

        const parsed = new Date(utcIsoValue);
        if (Number.isNaN(parsed.getTime())) {
            return "";
        }

        return [
            `${parsed.getFullYear()}-${pad(parsed.getMonth() + 1)}-${pad(parsed.getDate())}`,
            `${pad(parsed.getHours())}:${pad(parsed.getMinutes())}`
        ].join("T");
    };

    const toLocalDateInputValue = (utcIsoValue) => {
        if (!utcIsoValue) {
            return "";
        }

        const parsed = new Date(utcIsoValue);
        if (Number.isNaN(parsed.getTime())) {
            return "";
        }

        return `${parsed.getFullYear()}-${pad(parsed.getMonth() + 1)}-${pad(parsed.getDate())}`;
    };

    const toUtcIsoValue = (localInputValue) => {
        if (!localInputValue) {
            return "";
        }

        const parsed = new Date(localInputValue);
        if (Number.isNaN(parsed.getTime())) {
            return "";
        }

        return parsed.toISOString();
    };

    const localDateToUtcIsoAtEndOfDay = (localDateValue) => {
        if (!localDateValue) {
            return "";
        }

        const parsed = new Date(`${localDateValue}T23:59:59.999`);
        if (Number.isNaN(parsed.getTime())) {
            return "";
        }

        return parsed.toISOString();
    };

    const normalizeExpiryUtcForDateMode = (utcIsoValue) => {
        const localDateValue = toLocalDateInputValue(utcIsoValue);
        return localDateToUtcIsoAtEndOfDay(localDateValue);
    };

    const formatUtcDisplay = (utcIsoValue) => {
        if (!utcIsoValue) {
            return "-";
        }

        const fromSharedFormatter = window.appDateTime?.formatDateTime?.(utcIsoValue);
        if (fromSharedFormatter) {
            return fromSharedFormatter;
        }

        const parsed = new Date(utcIsoValue);
        if (Number.isNaN(parsed.getTime())) {
            return "-";
        }

        return localDateTimeFormatter.format(parsed);
    };

    const setTimezoneLabels = () => {
        const timezone = Intl.DateTimeFormat().resolvedOptions().timeZone || "Local";
        const timezoneLabels = page.querySelectorAll("[data-local-timezone-label]");
        for (const label of timezoneLabels) {
            if (label instanceof HTMLElement) {
                label.textContent = timezone;
            }
        }
    };

    const hydrateUtcDisplays = (scope = page) => {
        const displays = scope.querySelectorAll("[data-utc-display]");
        for (const element of displays) {
            if (!(element instanceof HTMLElement)) {
                continue;
            }

            const utcIso = element.dataset.utcDisplay || "";
            element.textContent = formatUtcDisplay(utcIso);
        }
    };

    const getRequestVerificationToken = () => {
        if (!(createForm instanceof HTMLFormElement)) {
            return "";
        }

        const input = createForm.querySelector('input[name="__RequestVerificationToken"]');
        return input instanceof HTMLInputElement ? input.value : "";
    };

    const escapeHtml = (value) => {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;");
    };

    const asString = (value) => {
        if (typeof value === "string") {
            return value;
        }

        if (value === null || value === undefined) {
            return "";
        }

        return String(value);
    };

    const asBoolean = (value) => {
        if (typeof value === "boolean") {
            return value;
        }

        if (typeof value === "string") {
            return value.toLowerCase() === "true";
        }

        return Boolean(value);
    };

    const normalizeCreatedUser = (userPayload) => {
        if (!userPayload || typeof userPayload !== "object") {
            return null;
        }

        const idValue = userPayload.id ?? userPayload.Id;
        const id = Number(idValue);
        if (!Number.isFinite(id) || id <= 0) {
            return null;
        }

        return {
            id,
            userName: asString(userPayload.userName ?? userPayload.UserName),
            displayName: asString(userPayload.displayName ?? userPayload.DisplayName),
            isActive: asBoolean(userPayload.isActive ?? userPayload.IsActive),
            isSuperAdmin: asBoolean(userPayload.isSuperAdmin ?? userPayload.IsSuperAdmin),
            expiresAtUtc: asString(userPayload.expiresAtUtc ?? userPayload.ExpiresAtUtc)
        };
    };

    const readJsonResponse = async (response) => {
        try {
            return await response.json();
        } catch {
            return null;
        }
    };

    let deleteModalInstance = null;
    let deleteModalPendingResolve = null;

    const resolveDeleteModalDecision = (confirmed) => {
        if (typeof deleteModalPendingResolve !== "function") {
            return;
        }

        const resolver = deleteModalPendingResolve;
        deleteModalPendingResolve = null;
        resolver(confirmed);
    };

    const initializeSoftDeleteModal = () => {
        if (!(softDeleteModalElement instanceof HTMLElement) || !window.bootstrap?.Modal) {
            return;
        }

        deleteModalInstance = new window.bootstrap.Modal(softDeleteModalElement, {
            backdrop: "static",
            keyboard: true
        });

        softDeleteModalElement.addEventListener("hidden.bs.modal", () => {
            resolveDeleteModalDecision(false);
        });

        if (softDeleteConfirmButton instanceof HTMLButtonElement) {
            softDeleteConfirmButton.addEventListener("click", () => {
                resolveDeleteModalDecision(true);
                deleteModalInstance?.hide();
            });
        }

        if (softDeleteCancelButton instanceof HTMLButtonElement) {
            softDeleteCancelButton.addEventListener("click", () => {
                resolveDeleteModalDecision(false);
            });
        }
    };

    const requestSoftDeleteConfirmation = (userName) => {
        if (!(softDeleteUserNameLabel instanceof HTMLElement) || !deleteModalInstance) {
            showToast("Delete confirmation modal is unavailable.", false);
            return Promise.resolve(false);
        }

        if (typeof deleteModalPendingResolve === "function") {
            return Promise.resolve(false);
        }

        softDeleteUserNameLabel.textContent = userName;
        deleteModalInstance.show();

        return new Promise((resolve) => {
            deleteModalPendingResolve = resolve;
        });
    };

    const postUrlEncoded = async (url, fields) => {
        const body = new URLSearchParams();
        for (const [key, value] of Object.entries(fields)) {
            if (value === null || value === undefined) {
                continue;
            }

            body.append(key, String(value));
        }

        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
                "X-Requested-With": "XMLHttpRequest",
                Accept: "application/json"
            },
            body: body.toString(),
            credentials: "same-origin"
        });

        return {
            response,
            payload: await readJsonResponse(response)
        };
    };

    const syncCreateFormExpiryDateFromHidden = () => {
        if (!(createForm instanceof HTMLFormElement)) {
            return;
        }

        const hiddenInput = createForm.querySelector("[data-utc-hidden]");
        const localInput = createForm.querySelector("[data-local-expiry-date-input]");
        if (!(hiddenInput instanceof HTMLInputElement) || !(localInput instanceof HTMLInputElement)) {
            return;
        }

        localInput.value = toLocalDateInputValue(hiddenInput.value);
    };

    const syncCreateFormExpiryDateToHidden = () => {
        if (!(createForm instanceof HTMLFormElement)) {
            return;
        }

        const hiddenInput = createForm.querySelector("[data-utc-hidden]");
        const localInput = createForm.querySelector("[data-local-expiry-date-input]");
        if (!(hiddenInput instanceof HTMLInputElement) || !(localInput instanceof HTMLInputElement)) {
            return;
        }

        hiddenInput.value = localDateToUtcIsoAtEndOfDay(localInput.value);
    };

    const clearCreateFormValidation = () => {
        if (!(createForm instanceof HTMLFormElement)) {
            return;
        }

        for (const invalidField of createForm.querySelectorAll(".is-invalid")) {
            invalidField.classList.remove("is-invalid");
        }

        for (const validationSpan of createForm.querySelectorAll("[data-valmsg-for]")) {
            if (!(validationSpan instanceof HTMLElement)) {
                continue;
            }

            if (validationSpan.dataset.valmsgFor?.startsWith("CreateForm.")) {
                validationSpan.textContent = "";
            }
        }
    };

    const resolveCreateFormInput = (fieldName) => {
        if (!(createForm instanceof HTMLFormElement)) {
            return null;
        }

        if (fieldName === "ExpiresAtUtc") {
            return createForm.querySelector("[data-local-expiry-date-input]");
        }

        return createForm.querySelector(`[name="CreateForm.${fieldName}"]`);
    };

    const renderCreateFormValidation = (errors) => {
        if (!(createForm instanceof HTMLFormElement) || !errors || typeof errors !== "object") {
            return;
        }

        for (const [fieldName, messages] of Object.entries(errors)) {
            const input = resolveCreateFormInput(fieldName);
            if (input instanceof HTMLElement) {
                input.classList.add("is-invalid");
            }

            const validationSpan = createForm.querySelector(`[data-valmsg-for="CreateForm.${fieldName}"]`);
            if (!(validationSpan instanceof HTMLElement)) {
                continue;
            }

            if (Array.isArray(messages) && messages.length > 0) {
                validationSpan.textContent = String(messages[0] ?? "");
            }
        }
    };

    const generateRandomPassword = (length = 20) => {
        const uppercase = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const lowercase = "abcdefghijkmnopqrstuvwxyz";
        const digits = "23456789";
        const special = "!@$%*-_";
        const all = uppercase + lowercase + digits + special;

        const getRandomIndex = (max) => {
            if (window.crypto?.getRandomValues) {
                const values = new Uint32Array(1);
                window.crypto.getRandomValues(values);
                return values[0] % max;
            }

            return Math.floor(Math.random() * max);
        };

        const passwordChars = [
            uppercase[getRandomIndex(uppercase.length)],
            lowercase[getRandomIndex(lowercase.length)],
            digits[getRandomIndex(digits.length)],
            special[getRandomIndex(special.length)]
        ];

        for (let index = passwordChars.length; index < length; index += 1) {
            passwordChars.push(all[getRandomIndex(all.length)]);
        }

        for (let index = passwordChars.length - 1; index > 0; index -= 1) {
            const swapIndex = getRandomIndex(index + 1);
            [passwordChars[index], passwordChars[swapIndex]] = [passwordChars[swapIndex], passwordChars[index]];
        }

        return passwordChars.join("");
    };

    const bindRandomPasswordGenerator = () => {
        const button = page.querySelector("[data-generate-random-password]");
        const target = page.querySelector("[data-random-password-target]");

        if (!(button instanceof HTMLButtonElement) || !(target instanceof HTMLInputElement)) {
            return;
        }

        button.addEventListener("click", () => {
            target.value = generateRandomPassword();
            target.dispatchEvent(new Event("input", { bubbles: true }));
            target.focus();
        });
    };

    const bindPasswordVisibilityToggle = () => {
        const toggleButton = page.querySelector("[data-toggle-password-visibility]");
        const passwordInput = page.querySelector("[data-password-input]");
        const showIcon = toggleButton?.querySelector("[data-password-icon-show]");
        const hideIcon = toggleButton?.querySelector("[data-password-icon-hide]");
        const hiddenLabel = toggleButton?.querySelector("[data-password-visibility-label]");

        if (!(toggleButton instanceof HTMLButtonElement) || !(passwordInput instanceof HTMLInputElement)) {
            return;
        }

        toggleButton.addEventListener("click", () => {
            const isVisible = passwordInput.type === "text";
            const nextVisible = !isVisible;
            passwordInput.type = nextVisible ? "text" : "password";
            toggleButton.setAttribute("aria-pressed", nextVisible ? "true" : "false");
            toggleButton.setAttribute("aria-label", nextVisible ? "Hide password" : "Show password");
            toggleButton.setAttribute("title", nextVisible ? "Hide password" : "Show password");

            if (showIcon instanceof SVGElement && hideIcon instanceof SVGElement) {
                showIcon.classList.toggle("d-none", nextVisible);
                hideIcon.classList.toggle("d-none", !nextVisible);
            }

            if (hiddenLabel instanceof HTMLElement) {
                hiddenLabel.textContent = nextVisible ? "Hide password" : "Show password";
            }

            passwordInput.focus();
        });
    };

    const bindPasswordCopyAction = () => {
        const copyButton = page.querySelector("[data-copy-password]");
        const passwordInput = page.querySelector("[data-password-input]");

        if (!(copyButton instanceof HTMLButtonElement) || !(passwordInput instanceof HTMLInputElement)) {
            return;
        }

        copyButton.addEventListener("click", async () => {
            const passwordValue = passwordInput.value || "";
            if (!passwordValue) {
                showToast("Type or generate a password before copying.", false);
                return;
            }

            try {
                await navigator.clipboard.writeText(passwordValue);
                showToast("Password copied.", true);
            } catch {
                showToast("Password could not be copied.", false);
            }
        });
    };

    const updateUsersTotalLabel = () => {
        const totalLabel = page.querySelector("[data-admin-users-total]");
        if (!(usersTableBody instanceof HTMLTableSectionElement) || !(totalLabel instanceof HTMLElement)) {
            return;
        }

        const usersCount = usersTableBody.querySelectorAll("tr").length;
        totalLabel.textContent = `Total: ${usersCount}`;
    };

    const syncUsersListEmptyState = () => {
        if (!(usersTableBody instanceof HTMLTableSectionElement)) {
            return;
        }

        const usersCount = usersTableBody.querySelectorAll("tr").length;
        if (usersTableWrapper instanceof HTMLElement) {
            usersTableWrapper.classList.toggle("d-none", usersCount === 0);
        }

        if (usersEmptyState instanceof HTMLElement) {
            usersEmptyState.classList.toggle("d-none", usersCount > 0);
        }

        if (paginationRoot instanceof HTMLElement) {
            paginationRoot.classList.toggle("d-none", usersCount === 0);
        }
    };

    const getUserRows = () => {
        if (!(usersTableBody instanceof HTMLTableSectionElement)) {
            return [];
        }

        return Array.from(usersTableBody.querySelectorAll("tr"));
    };

    const getUsersTotalPages = () => {
        const rows = getUserRows();
        return Math.max(1, Math.ceil(rows.length / usersPaginationState.pageSize));
    };

    const applyUsersPagination = () => {
        if (!(usersTableBody instanceof HTMLTableSectionElement)) {
            return;
        }

        const rows = getUserRows();
        const totalRows = rows.length;
        if (totalRows === 0) {
            if (paginationRoot instanceof HTMLElement) {
                paginationRoot.classList.add("d-none");
            }

            if (paginationInfo instanceof HTMLElement) {
                paginationInfo.textContent = "";
            }

            return;
        }

        const totalPages = getUsersTotalPages();
        usersPaginationState.currentPage = Math.min(
            totalPages,
            Math.max(1, usersPaginationState.currentPage));

        const startIndex = (usersPaginationState.currentPage - 1) * usersPaginationState.pageSize;
        const endIndex = startIndex + usersPaginationState.pageSize;

        for (const [index, row] of rows.entries()) {
            const visible = index >= startIndex && index < endIndex;
            row.classList.toggle("d-none", !visible);
        }

        if (paginationRoot instanceof HTMLElement) {
            paginationRoot.classList.toggle("d-none", totalRows <= usersPaginationState.pageSize);
        }

        if (paginationInfo instanceof HTMLElement) {
            paginationInfo.textContent = `Page ${usersPaginationState.currentPage} of ${totalPages}`;
        }

        if (paginationPrevButton instanceof HTMLButtonElement) {
            paginationPrevButton.disabled = usersPaginationState.currentPage <= 1;
        }

        if (paginationNextButton instanceof HTMLButtonElement) {
            paginationNextButton.disabled = usersPaginationState.currentPage >= totalPages;
        }
    };

    const bindUsersPaginationControls = () => {
        if (paginationPrevButton instanceof HTMLButtonElement) {
            paginationPrevButton.addEventListener("click", () => {
                if (usersPaginationState.currentPage <= 1) {
                    return;
                }

                usersPaginationState.currentPage -= 1;
                applyUsersPagination();
            });
        }

        if (paginationNextButton instanceof HTMLButtonElement) {
            paginationNextButton.addEventListener("click", () => {
                const totalPages = getUsersTotalPages();
                if (usersPaginationState.currentPage >= totalPages) {
                    return;
                }

                usersPaginationState.currentPage += 1;
                applyUsersPagination();
            });
        }
    };

    const setActiveBadgeState = (badge, isActive) => {
        if (!(badge instanceof HTMLElement)) {
            return;
        }

        badge.classList.remove("text-bg-success", "text-bg-secondary");
        badge.classList.add(isActive ? "text-bg-success" : "text-bg-secondary");
        badge.textContent = isActive ? "Active" : "Inactive";
    };

    const getRowElements = (row) => {
        return {
            displayNameView: row.querySelector("[data-row-view-display-name]"),
            displayNameInput: row.querySelector("[data-row-edit-display-name]"),
            activeBadge: row.querySelector("[data-row-active-badge]"),
            activeToggleWrap: row.querySelector("[data-row-edit-active-wrapper]"),
            activeToggle: row.querySelector("[data-row-edit-is-active]"),
            expiresView: row.querySelector("[data-row-view-expires]"),
            expiresHidden: row.querySelector("[data-row-edit-expires-utc]"),
            expiresLocalInput: row.querySelector("[data-row-edit-expires-local]"),
            actionButton: row.querySelector("[data-row-edit-action]"),
            actionEditIcon: row.querySelector("[data-row-action-icon-edit]"),
            actionSaveIcon: row.querySelector("[data-row-action-icon-save]"),
            deleteButton: row.querySelector("[data-row-delete-action]")
        };
    };

    const readRowCurrentState = (row, elements) => {
        const displayName = elements.displayNameInput instanceof HTMLInputElement
            ? elements.displayNameInput.value.trim()
            : asString(row.dataset.initialDisplayName);

        const expiresAtUtc = elements.expiresLocalInput instanceof HTMLInputElement
            ? localDateToUtcIsoAtEndOfDay(elements.expiresLocalInput.value)
            : normalizeExpiryUtcForDateMode(asString(row.dataset.initialExpiresUtc));

        const isActive = elements.activeToggle instanceof HTMLInputElement
            ? elements.activeToggle.checked
            : asBoolean(row.dataset.initialIsActive);

        return {
            displayName,
            expiresAtUtc,
            isActive
        };
    };

    const readRowBaselineState = (row) => {
        return {
            displayName: asString(row.dataset.initialDisplayName),
            expiresAtUtc: normalizeExpiryUtcForDateMode(asString(row.dataset.initialExpiresUtc)),
            isActive: asBoolean(row.dataset.initialIsActive)
        };
    };

    const writeRowBaselineState = (row, state) => {
        row.dataset.initialDisplayName = state.displayName;
        row.dataset.initialExpiresUtc = state.expiresAtUtc;
        row.dataset.initialIsActive = state.isActive ? "true" : "false";
    };

    const rowStateEquals = (a, b) => {
        return a.displayName === b.displayName &&
            a.expiresAtUtc === b.expiresAtUtc &&
            a.isActive === b.isActive;
    };

    const updateRowActionButton = (row, elements) => {
        if (!(elements.actionButton instanceof HTMLButtonElement)) {
            return;
        }

        const isEditing = row.dataset.editing === "true";
        const isDirty = row.dataset.dirty === "true";
        const isSaving = row.dataset.saving === "true";
        const isDeleting = row.dataset.deleting === "true";

        elements.actionButton.disabled = isSaving || isDeleting;
        if (elements.deleteButton instanceof HTMLButtonElement) {
            elements.deleteButton.disabled = isSaving || isDeleting;
        }
        elements.actionButton.classList.remove("admin-row-action-button-save");

        if (isEditing && isDirty) {
            elements.actionButton.classList.add("admin-row-action-button-save");
            elements.actionButton.setAttribute("title", "Save changes");
            elements.actionButton.setAttribute("aria-label", "Save changes");

            if (elements.actionEditIcon instanceof SVGElement && elements.actionSaveIcon instanceof SVGElement) {
                elements.actionEditIcon.classList.add("d-none");
                elements.actionSaveIcon.classList.remove("d-none");
            }

            return;
        }

        elements.actionButton.setAttribute("title", isEditing ? "Close edit mode" : "Edit user");
        elements.actionButton.setAttribute("aria-label", isEditing ? "Close edit mode" : "Edit user");

        if (elements.actionEditIcon instanceof SVGElement && elements.actionSaveIcon instanceof SVGElement) {
            elements.actionEditIcon.classList.remove("d-none");
            elements.actionSaveIcon.classList.add("d-none");
        }
    };

    const setRowEditing = (row, elements, editing) => {
        row.dataset.editing = editing ? "true" : "false";
        if (!editing) {
            row.dataset.dirty = "false";
        }

        const displayNameInput = elements.displayNameInput;
        const activeToggleWrap = elements.activeToggleWrap;
        const expiresLocalInput = elements.expiresLocalInput;

        if (displayNameInput instanceof HTMLInputElement) {
            displayNameInput.classList.toggle("d-none", !editing);
        }

        if (elements.displayNameView instanceof HTMLElement) {
            elements.displayNameView.classList.toggle("d-none", editing);
        }

        if (activeToggleWrap instanceof HTMLElement) {
            activeToggleWrap.classList.toggle("d-none", !editing);
        }

        const viewFlags = row.querySelector("[data-row-view-flags]");
        if (viewFlags instanceof HTMLElement) {
            viewFlags.classList.toggle("d-none", editing);
        }

        if (expiresLocalInput instanceof HTMLInputElement) {
            expiresLocalInput.classList.toggle("d-none", !editing);
        }

        if (elements.expiresView instanceof HTMLElement) {
            elements.expiresView.classList.toggle("d-none", editing);
        }

        updateRowActionButton(row, elements);
    };

    const clearRowValidation = (elements) => {
        if (elements.displayNameInput instanceof HTMLElement) {
            elements.displayNameInput.classList.remove("is-invalid");
        }

        if (elements.expiresLocalInput instanceof HTMLElement) {
            elements.expiresLocalInput.classList.remove("is-invalid");
        }
    };

    const renderRowValidation = (elements, errors) => {
        if (!errors || typeof errors !== "object") {
            return;
        }

        const displayNameErrors = errors.DisplayName ?? errors.displayName;
        if (Array.isArray(displayNameErrors) && displayNameErrors.length > 0 && elements.displayNameInput instanceof HTMLElement) {
            elements.displayNameInput.classList.add("is-invalid");
        }

        const expiresErrors = errors.ExpiresAtUtc ?? errors.expiresAtUtc;
        if (Array.isArray(expiresErrors) && expiresErrors.length > 0 && elements.expiresLocalInput instanceof HTMLElement) {
            elements.expiresLocalInput.classList.add("is-invalid");
        }
    };

    const updateRowViewFromState = (row, elements, state) => {
        if (elements.displayNameView instanceof HTMLElement) {
            elements.displayNameView.textContent = state.displayName;
        }

        if (elements.displayNameInput instanceof HTMLInputElement) {
            elements.displayNameInput.value = state.displayName;
        }

        if (elements.expiresHidden instanceof HTMLInputElement) {
            elements.expiresHidden.value = state.expiresAtUtc;
        }

        if (elements.expiresView instanceof HTMLElement) {
            elements.expiresView.dataset.utcDisplay = state.expiresAtUtc;
            elements.expiresView.textContent = formatUtcDisplay(state.expiresAtUtc);
        }

        if (elements.expiresLocalInput instanceof HTMLInputElement) {
            elements.expiresLocalInput.value = toLocalDateInputValue(state.expiresAtUtc);
        }

        if (elements.activeToggle instanceof HTMLInputElement) {
            elements.activeToggle.checked = state.isActive;
        }

        setActiveBadgeState(elements.activeBadge, state.isActive);
        writeRowBaselineState(row, state);
    };

    const saveEditableRow = async (row, elements) => {
        const rowId = Number(row.dataset.userId);
        if (!Number.isFinite(rowId) || rowId <= 0) {
            showToast("Could not save this user.", false);
            return;
        }

        const baseline = readRowBaselineState(row);
        const current = readRowCurrentState(row, elements);
        const profileChanged = baseline.displayName !== current.displayName || baseline.expiresAtUtc !== current.expiresAtUtc;
        const activeChanged = baseline.isActive !== current.isActive;

        if (!profileChanged && !activeChanged) {
            setRowEditing(row, elements, false);
            return;
        }

        row.dataset.saving = "true";
        updateRowActionButton(row, elements);
        clearRowValidation(elements);

        try {
            let latestMessage = "User saved.";
            const token = getRequestVerificationToken();

            if (profileChanged) {
                const profileResult = await postUrlEncoded(
                    updateActionPath,
                    {
                        __RequestVerificationToken: token,
                        "UpdateForm.UserId": rowId,
                        "UpdateForm.DisplayName": current.displayName,
                        "UpdateForm.ExpiresAtUtc": current.expiresAtUtc
                    });

                if (!profileResult.response.ok) {
                    renderRowValidation(elements, profileResult.payload?.errors);
                    showToast(profileResult.payload?.message || "Could not save user profile.", false);
                    return;
                }

                latestMessage = profileResult.payload?.message || latestMessage;
            }

            if (activeChanged) {
                const toggleResult = await postUrlEncoded(
                    toggleActionPath,
                    {
                        __RequestVerificationToken: token,
                        "ToggleActiveForm.UserId": rowId,
                        "ToggleActiveForm.IsActive": current.isActive
                    });

                if (!toggleResult.response.ok) {
                    showToast(toggleResult.payload?.message || "Could not save activation state.", false);
                    return;
                }

                latestMessage = toggleResult.payload?.message || latestMessage;
            }

            updateRowViewFromState(row, elements, current);
            setRowEditing(row, elements, false);
            showToast(latestMessage, true);
        } catch {
            showToast("Could not save this user.", false);
        } finally {
            row.dataset.saving = "false";
            updateRowActionButton(row, elements);
        }
    };

    const deleteUserRow = async (row, elements) => {
        const rowId = Number(row.dataset.userId);
        const userName = row.dataset.userName || `#${rowId}`;
        if (!Number.isFinite(rowId) || rowId <= 0) {
            showToast("Could not delete this user.", false);
            return;
        }

        const confirmed = await requestSoftDeleteConfirmation(userName);
        if (!confirmed) {
            return;
        }

        row.dataset.deleting = "true";
        updateRowActionButton(row, elements);

        try {
            const token = getRequestVerificationToken();
            const deleteResult = await postUrlEncoded(
                softDeleteActionPath,
                {
                    __RequestVerificationToken: token,
                    "SoftDeleteForm.UserId": rowId
                });

            if (!deleteResult.response.ok) {
                showToast(deleteResult.payload?.message || "Could not delete this user.", false);
                return;
            }

            row.remove();
            updateUsersTotalLabel();
            syncUsersListEmptyState();
            usersPaginationState.currentPage = Math.min(usersPaginationState.currentPage, getUsersTotalPages());
            applyUsersPagination();
            showToast(deleteResult.payload?.message || `Soft-deleted user '${userName}'.`, true);
        } catch {
            showToast("Could not delete this user.", false);
        } finally {
            row.dataset.deleting = "false";
            if (row.isConnected) {
                updateRowActionButton(row, elements);
            }
        }
    };

    const refreshRowDirtyState = (row, elements) => {
        const baseline = readRowBaselineState(row);
        const current = readRowCurrentState(row, elements);
        row.dataset.dirty = rowStateEquals(baseline, current) ? "false" : "true";
        updateRowActionButton(row, elements);
    };

    const initializeEditableUserRow = (row) => {
        if (!(row instanceof HTMLTableRowElement) || row.dataset.adminUserRow !== "true") {
            return;
        }

        const elements = getRowElements(row);
        if (!(elements.actionButton instanceof HTMLButtonElement)) {
            return;
        }

        if (elements.expiresHidden instanceof HTMLInputElement && elements.expiresLocalInput instanceof HTMLInputElement) {
            elements.expiresLocalInput.value = toLocalDateInputValue(elements.expiresHidden.value);
        }

        setActiveBadgeState(elements.activeBadge, asBoolean(row.dataset.initialIsActive));
        setRowEditing(row, elements, false);

        elements.actionButton.addEventListener("click", async () => {
            if (row.dataset.saving === "true" || row.dataset.deleting === "true") {
                return;
            }

            const isEditing = row.dataset.editing === "true";
            const isDirty = row.dataset.dirty === "true";

            if (!isEditing) {
                clearRowValidation(elements);
                if (elements.displayNameInput instanceof HTMLInputElement) {
                    elements.displayNameInput.value = asString(row.dataset.initialDisplayName);
                }

                if (elements.expiresLocalInput instanceof HTMLInputElement) {
                    elements.expiresLocalInput.value = toLocalDateInputValue(asString(row.dataset.initialExpiresUtc));
                }

                if (elements.activeToggle instanceof HTMLInputElement) {
                    elements.activeToggle.checked = asBoolean(row.dataset.initialIsActive);
                }

                row.dataset.dirty = "false";
                setRowEditing(row, elements, true);
                elements.displayNameInput?.focus();
                return;
            }

            if (!isDirty) {
                setRowEditing(row, elements, false);
                return;
            }

            await saveEditableRow(row, elements);
        });

        elements.deleteButton?.addEventListener("click", async () => {
            if (row.dataset.saving === "true" || row.dataset.deleting === "true") {
                return;
            }

            await deleteUserRow(row, elements);
        });

        const markDirty = () => {
            if (row.dataset.editing !== "true") {
                return;
            }

            refreshRowDirtyState(row, elements);
            clearRowValidation(elements);
        };

        elements.displayNameInput?.addEventListener("input", markDirty);
        elements.expiresLocalInput?.addEventListener("input", markDirty);
        elements.activeToggle?.addEventListener("change", markDirty);
    };

    const initializeEditableUserRows = () => {
        const rows = page.querySelectorAll('[data-admin-user-row="true"]');
        for (const row of rows) {
            initializeEditableUserRow(row);
        }
    };

    const buildEditableUserRow = (user) => {
        const row = document.createElement("tr");
        const activeBadgeClass = user.isActive ? "text-bg-success" : "text-bg-secondary";
        const activeBadgeLabel = user.isActive ? "Active" : "Inactive";
        const expiresUtc = user.expiresAtUtc || "";

        row.dataset.adminUserRow = "true";
        row.dataset.userId = String(user.id);
        row.dataset.userName = user.userName;
        row.dataset.initialDisplayName = user.displayName;
        row.dataset.initialExpiresUtc = expiresUtc;
        row.dataset.initialIsActive = user.isActive ? "true" : "false";

        row.innerHTML = `
            <td>${user.id}</td>
            <td>${escapeHtml(user.userName)}</td>
            <td class="min-w-240">
                <span data-row-view-display-name>${escapeHtml(user.displayName)}</span>
                <input type="text" class="form-control form-control-sm d-none" data-row-edit-display-name value="${escapeHtml(user.displayName)}" maxlength="256" autocomplete="off" />
            </td>
            <td>
                <div data-row-view-flags>
                    <span class="badge ${activeBadgeClass} me-1" data-row-active-badge>${activeBadgeLabel}</span>
                </div>
                <div class="form-check form-switch d-none" data-row-edit-active-wrapper>
                    <input class="form-check-input" type="checkbox" data-row-edit-is-active ${user.isActive ? "checked" : ""} />
                    <label class="form-check-label small">Active</label>
                </div>
            </td>
            <td>
                <span data-row-view-expires data-utc-display="${escapeHtml(expiresUtc)}">${escapeHtml(formatUtcDisplay(expiresUtc))}</span>
                <input type="hidden" data-row-edit-expires-utc value="${escapeHtml(expiresUtc)}" />
                <input type="date" class="form-control form-control-sm d-none" data-row-edit-expires-local autocomplete="off" />
            </td>
            <td>
                <div class="d-inline-flex align-items-center gap-2">
                    <button
                        type="button"
                        class="btn admin-row-action-button"
                        data-row-edit-action
                        aria-label="Edit user"
                        title="Edit user">
                        <svg class="admin-icon" data-row-action-icon-edit viewBox="0 0 24 24" fill="none" aria-hidden="true">
                            <path d="M4 20H8L18 10C18.78 9.22 18.78 7.95 18 7.17L16.83 6C16.05 5.22 14.78 5.22 14 6L4 16V20Z" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
                        </svg>
                        <svg class="admin-icon d-none" data-row-action-icon-save viewBox="0 0 24 24" fill="none" aria-hidden="true">
                            <path d="M5 12L10 17L19 8" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
                        </svg>
                        <span class="visually-hidden">Edit user</span>
                    </button>
                    <button
                        type="button"
                        class="btn admin-row-action-button admin-row-action-button-danger"
                        data-row-delete-action
                        aria-label="Soft-delete user"
                        title="Soft-delete user">
                        <svg class="admin-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                            <path d="M4 7H20" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
                            <path d="M9 11V17" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
                            <path d="M15 11V17" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
                            <path d="M6 7L7 19C7.08 19.93 7.85 20.64 8.79 20.64H15.21C16.15 20.64 16.92 19.93 17 19L18 7" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
                            <path d="M10 4H14" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
                        </svg>
                        <span class="visually-hidden">Soft-delete user</span>
                    </button>
                </div>
            </td>
        `;

        return row;
    };

    const appendCreatedUserRow = (user) => {
        if (!user || user.isSuperAdmin) {
            return;
        }

        const tbody = page.querySelector("table tbody");
        if (!(tbody instanceof HTMLTableSectionElement)) {
            return;
        }

        const existing = tbody.querySelector(`[data-user-id="${user.id}"]`);
        if (existing instanceof HTMLTableRowElement) {
            return;
        }

        const row = buildEditableUserRow(user);
        tbody.append(row);
        initializeEditableUserRow(row);
        hydrateUtcDisplays(row);
        syncUsersListEmptyState();
        usersPaginationState.currentPage = getUsersTotalPages();
        applyUsersPagination();
        updateUsersTotalLabel();
    };

    const resetCreateFormUi = () => {
        if (!(createForm instanceof HTMLFormElement)) {
            return;
        }

        createForm.reset();
        clearCreateFormValidation();
        syncCreateFormExpiryDateFromHidden();

        const passwordInput = createForm.querySelector("[data-password-input]");
        if (passwordInput instanceof HTMLInputElement) {
            passwordInput.type = "password";
        }

        const toggleButton = createForm.querySelector("[data-toggle-password-visibility]");
        if (toggleButton instanceof HTMLButtonElement) {
            toggleButton.setAttribute("aria-pressed", "false");
            toggleButton.setAttribute("aria-label", "Show password");
            toggleButton.setAttribute("title", "Show password");
        }

        const showIcon = createForm.querySelector("[data-password-icon-show]");
        const hideIcon = createForm.querySelector("[data-password-icon-hide]");
        if (showIcon instanceof SVGElement && hideIcon instanceof SVGElement) {
            showIcon.classList.remove("d-none");
            hideIcon.classList.add("d-none");
        }

        const hiddenLabel = createForm.querySelector("[data-password-visibility-label]");
        if (hiddenLabel instanceof HTMLElement) {
            hiddenLabel.textContent = "Show password";
        }
    };

    const bindCreateFormSubmission = () => {
        if (!(createForm instanceof HTMLFormElement)) {
            return;
        }

        if (createForm.dataset.ajaxSubmitBound === "true") {
            return;
        }

        createForm.dataset.ajaxSubmitBound = "true";
        createForm.addEventListener("submit", async (event) => {
            event.preventDefault();
            clearCreateFormValidation();
            syncCreateFormExpiryDateToHidden();

            const submitButton = createForm.querySelector('button[type="submit"]');
            setButtonLoading(submitButton, true, "Creating user...");

            try {
                const response = await fetch(createForm.action, {
                    method: "POST",
                    body: new FormData(createForm),
                    headers: {
                        "X-Requested-With": "XMLHttpRequest",
                        Accept: "application/json"
                    },
                    credentials: "same-origin"
                });

                const payload = await readJsonResponse(response);
                if (!response.ok) {
                    renderCreateFormValidation(payload?.errors);
                    showToast(payload?.message || "Could not create user.", false);
                    return;
                }

                const createdUser = normalizeCreatedUser(payload?.user);
                if (createdUser) {
                    appendCreatedUserRow(createdUser);
                }

                resetCreateFormUi();
                showToast(payload?.message || "User created.", true);
            } catch {
                showToast("Could not create user.", false);
            } finally {
                setButtonLoading(submitButton, false);
            }
        });
    };

    const readInitialStatusFromServer = () => {
        const statusElement = document.querySelector("[data-admin-page-status]");
        if (!(statusElement instanceof HTMLElement)) {
            return;
        }

        const message = statusElement.dataset.statusMessage;
        if (!message) {
            return;
        }

        const succeeded = statusElement.dataset.statusSucceeded === "true";
        showToast(message, succeeded);
    };

    syncCreateFormExpiryDateFromHidden();
    setTimezoneLabels();
    hydrateUtcDisplays();
    readInitialStatusFromServer();

    bindRandomPasswordGenerator();
    bindPasswordVisibilityToggle();
    bindPasswordCopyAction();
    bindCreateFormSubmission();
    initializeEditableUserRows();
    bindUsersPaginationControls();
    initializeSoftDeleteModal();
    syncUsersListEmptyState();
    applyUsersPagination();
})();
