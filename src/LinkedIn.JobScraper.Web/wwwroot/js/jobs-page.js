(() => {
    const form = document.querySelector("[data-fetch-score-form]");
    if (!form) {
        return;
    }

    const button = form.querySelector("[data-fetch-score-button]");
    const idleText = form.querySelector("[data-idle-text]");
    const busyText = form.querySelector("[data-busy-text]");
    const progressPanel = document.querySelector("[data-fetch-progress]");
    const progressBar = document.querySelector("[data-progress-bar]");
    const progressStatus = document.querySelector("[data-progress-status]");
    const progressLog = document.querySelector("[data-progress-log]");
    const progressLogEmpty = document.querySelector("[data-progress-log-empty]");
    const cancelButton = document.querySelector("[data-fetch-cancel-button]");
    const stageNodes = Array.from(document.querySelectorAll("[data-progress-stage]"));
    const stageOrder = ["fetch", "enrichment"];
    const pollUrl = form.dataset.progressPollUrl;
    const cancelUrl = form.dataset.cancelUrl;
    const antiForgeryToken = form.querySelector('input[name="__RequestVerificationToken"]')?.value ?? "";
    const jobsPage = form.closest(".jobs-page");
    const pageAlertMessage = jobsPage?.dataset.pageAlertMessage?.trim() ?? "";
    const pageAlertSeverity = jobsPage?.dataset.pageAlertSeverity ?? "info";
    const showToast = window.appToast?.show ?? (() => {});
    const setButtonLoading = window.appButtons?.setLoading ?? (() => {});
    const busySpinner = form.querySelector("[data-busy-spinner]");
    const workflowStorageKey = "jobs.activeWorkflow";
    const prefersReducedMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches ?? false;
    let signalRConnection = null;
    let isSubmitting = false;
    let lastLoggedMessage = null;
    let activeWorkflowId = null;
    let lastSequence = 0;
    let workflowTerminalState = null;
    let workflowPollingTimer = null;
    let activeFetchPromise = null;
    let workflowRedirectUrl = null;
    let isPageUnloading = false;

    const setBusyState = () => {
        if (button) {
            button.disabled = true;
        }

        if (busySpinner) {
            busySpinner.classList.remove("d-none");
        }

        if (idleText) {
            idleText.classList.add("d-none");
        }

        if (busyText) {
            busyText.classList.remove("d-none");
        }

        if (progressPanel) {
            progressPanel.classList.remove("d-none");
        }

        if (cancelButton) {
            cancelButton.classList.remove("d-none");
            cancelButton.disabled = false;
        }
    };

    const setIdleState = () => {
        if (button) {
            button.disabled = false;
        }

        if (busySpinner) {
            busySpinner.classList.add("d-none");
        }

        if (idleText) {
            idleText.classList.remove("d-none");
        }

        if (busyText) {
            busyText.classList.add("d-none");
        }

        if (cancelButton) {
            cancelButton.classList.add("d-none");
            cancelButton.disabled = false;
        }
    };

    const resetWorkflowUi = ({ hidePanel = false } = {}) => {
        stopPolling();
        isSubmitting = false;
        activeFetchPromise = null;
        activeWorkflowId = null;
        workflowTerminalState = null;
        workflowRedirectUrl = null;
        lastSequence = 0;
        lastLoggedMessage = null;
        clearActiveWorkflowId();
        setIdleState();

        if (hidePanel && progressPanel) {
            progressPanel.classList.add("d-none");
        }
    };

    const stopPolling = () => {
        if (!workflowPollingTimer) {
            return;
        }

        window.clearTimeout(workflowPollingTimer);
        workflowPollingTimer = null;
    };

    const storeActiveWorkflowState = (workflowId, redirectUrl) => {
        try {
            if (workflowId) {
                window.sessionStorage.setItem(workflowStorageKey, JSON.stringify({
                    workflowId,
                    redirectUrl: redirectUrl || window.location.href
                }));
            }
        } catch {
        }
    };

    const clearActiveWorkflowId = () => {
        try {
            window.sessionStorage.removeItem(workflowStorageKey);
        } catch {
        }
    };

    const readActiveWorkflowState = () => {
        try {
            const rawValue = window.sessionStorage.getItem(workflowStorageKey);
            if (!rawValue) {
                return null;
            }

            try {
                const parsedValue = JSON.parse(rawValue);
                if (parsedValue && typeof parsedValue.workflowId === "string" && parsedValue.workflowId.trim()) {
                    return {
                        workflowId: parsedValue.workflowId.trim(),
                        redirectUrl: typeof parsedValue.redirectUrl === "string" && parsedValue.redirectUrl.trim()
                            ? parsedValue.redirectUrl.trim()
                            : window.location.href
                    };
                }
            } catch {
                if (rawValue.trim()) {
                    return {
                        workflowId: rawValue.trim(),
                        redirectUrl: window.location.href
                    };
                }
            }
        } catch {
        }

        return null;
    };

    const scrollProgressLogToLatest = (behavior = "smooth") => {
        if (!progressLog) {
            return;
        }

        const top = progressLog.scrollHeight;
        const effectiveBehavior = prefersReducedMotion ? "auto" : behavior;

        if (typeof progressLog.scrollTo === "function") {
            progressLog.scrollTo({
                top,
                behavior: effectiveBehavior
            });
            return;
        }

        progressLog.scrollTop = top;
    };

    const appendProgressLog = (update, occurredAtUtc) => {
        if (!progressLog || !update || !update.message) {
            return;
        }

        const dedupeKey = `${update.state}|${update.stage}|${update.message}|${update.requestedCount}|${update.processedCount}|${update.succeededCount}|${update.failedCount}`;
        if (lastLoggedMessage === dedupeKey) {
            return;
        }

        lastLoggedMessage = dedupeKey;

        if (progressLogEmpty) {
            progressLogEmpty.classList.add("d-none");
        }

        progressLog.classList.remove("d-none");

        const item = document.createElement("div");
        item.className = "progress-activity-item";

        const state = String(update.state || "running");
        if (state === "completed") {
            item.classList.add("completed");
        } else if (state === "warning" || state === "cancelled") {
            item.classList.add("warning");
        } else if (state === "failed") {
            item.classList.add("failed");
        }

        const timestamp = occurredAtUtc ? new Date(occurredAtUtc) : new Date();
        const timeLabel = new Intl.DateTimeFormat([], {
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit"
        }).format(timestamp);

        const detailParts = [];
        if (Number.isFinite(Number(update.requestedCount)) && Number(update.requestedCount) > 0) {
            detailParts.push(`Requested: ${update.requestedCount}`);
        }
        if (Number.isFinite(Number(update.processedCount)) && Number(update.processedCount) > 0) {
            detailParts.push(`Processed: ${update.processedCount}`);
        }
        if (Number.isFinite(Number(update.succeededCount)) && Number(update.succeededCount) > 0) {
            detailParts.push(`Succeeded: ${update.succeededCount}`);
        }
        if (Number.isFinite(Number(update.failedCount)) && Number(update.failedCount) > 0) {
            detailParts.push(`Failed: ${update.failedCount}`);
        }

        const detailsMarkup = detailParts.length > 0
            ? `<div class="progress-activity-details">${detailParts.join(" · ")}</div>`
            : "";

        item.innerHTML = `
            <div class="progress-activity-meta">
                <span class="progress-activity-time">${timeLabel}</span>
                <span class="progress-activity-state">${state}</span>
            </div>
            <div class="progress-activity-message">${update.message}</div>
            ${detailsMarkup}
        `;

        progressLog.append(item);

        window.requestAnimationFrame(() => {
            item.classList.add("is-visible");
            scrollProgressLogToLatest();
        });
    };

    const applyProgressUpdate = (update, occurredAtUtc) => {
        if (!update) {
            return;
        }

        if (progressPanel) {
            progressPanel.classList.remove("d-none");
        }

        if (progressStatus && update.message) {
            progressStatus.textContent = update.message;
        }

        appendProgressLog(update, occurredAtUtc);

        if (progressBar) {
            const percent = Math.max(0, Math.min(100, Number(update.percent) || 0));
            progressBar.style.width = `${percent}%`;
            progressBar.setAttribute("aria-valuenow", String(percent));
            progressBar.classList.toggle("progress-bar-animated", update.state === "running");
            progressBar.classList.toggle("progress-bar-striped", update.state === "running");
            progressBar.classList.toggle("bg-warning", update.state === "warning" || update.state === "cancelled");
            progressBar.classList.toggle("bg-danger", update.state === "failed");
            progressBar.classList.toggle("bg-success", update.state === "completed");
        }

        const activeIndex = stageOrder.indexOf(update.stage);

        stageNodes.forEach((node, currentIndex) => {
            const isActive = currentIndex === activeIndex;
            const isCompleted = activeIndex > currentIndex || update.stage === "completed";

            node.classList.toggle("border-primary", isActive);
            node.classList.toggle("bg-primary-subtle", isActive);
            node.classList.toggle("text-primary-emphasis", isActive);
            node.classList.toggle("border-success", !isActive && isCompleted);
            node.classList.toggle("bg-success-subtle", !isActive && isCompleted);
            node.classList.toggle("text-success-emphasis", !isActive && isCompleted);
        });

        const shouldReloadAfterRestore = update.state === "completed" && !activeFetchPromise && workflowRedirectUrl;
        const completionRedirectUrl = shouldReloadAfterRestore ? workflowRedirectUrl : null;

        if (["completed", "failed", "cancelled"].includes(update.state)) {
            workflowTerminalState = update.state;
            resetWorkflowUi();
        }

        if (completionRedirectUrl) {
            window.setTimeout(() => {
                window.location.assign(completionRedirectUrl);
            }, 250);
        }
    };

    const ensureConnection = async () => {
        if (!window.signalR) {
            return null;
        }

        if (signalRConnection && signalRConnection.state === "Connected") {
            return signalRConnection;
        }

        signalRConnection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/jobs-workflow-progress")
            .withAutomaticReconnect()
            .build();

        signalRConnection.on("WorkflowProgress", (update) => {
            applyProgressUpdate(update, null);
        });

        await signalRConnection.start();
        return signalRConnection;
    };

    const pollWorkflowProgress = async () => {
        if (!activeWorkflowId || !pollUrl) {
            return;
        }

        try {
            const response = await fetch(`${pollUrl}?workflowId=${encodeURIComponent(activeWorkflowId)}&afterSequence=${lastSequence}`, {
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                credentials: "same-origin"
            });

            if (!response.ok) {
                throw new Error("Could not read workflow progress.");
            }

            const payload = await response.json();
            if (payload?.workflowFound === false) {
                resetWorkflowUi({ hidePanel: true });
                return;
            }

            const events = Array.isArray(payload?.events) ? payload.events : [];

            for (const item of events) {
                const sequence = Number(item?.sequence);
                if (Number.isFinite(sequence) && sequence > lastSequence) {
                    lastSequence = sequence;
                }

                if (item?.update) {
                    applyProgressUpdate(item.update, item.occurredAtUtc);
                }
            }
        } catch {
            if (!workflowTerminalState) {
                appendProgressLog({
                    state: "warning",
                    stage: "fetch",
                    message: "Progress polling missed one server update. Retrying automatically..."
                });
            }
        } finally {
            if (!workflowTerminalState) {
                workflowPollingTimer = window.setTimeout(() => {
                    void pollWorkflowProgress();
                }, 900);
            }
        }
    };

    const beginWorkflowTracking = (workflowId, redirectUrl) => {
        activeWorkflowId = workflowId;
        workflowRedirectUrl = redirectUrl || window.location.href;
        lastSequence = 0;
        lastLoggedMessage = null;
        workflowTerminalState = null;
        storeActiveWorkflowState(workflowId, workflowRedirectUrl);
        stopPolling();

        if (progressLog) {
            progressLog.innerHTML = "";
            progressLog.classList.add("d-none");
        }

        if (progressLogEmpty) {
            progressLogEmpty.classList.remove("d-none");
            progressLogEmpty.textContent = "Waiting for the first workflow event to appear...";
        }

        void pollWorkflowProgress();
    };

    window.addEventListener("pagehide", () => {
        isPageUnloading = true;
    });

    window.addEventListener("beforeunload", () => {
        isPageUnloading = true;
    });

    window.addEventListener("pageshow", () => {
        isPageUnloading = false;
    });

    form.addEventListener("submit", async (event) => {
        if (isSubmitting) {
            event.preventDefault();
            return;
        }

        event.preventDefault();
        isSubmitting = true;

        let connection = null;

        try {
            connection = await ensureConnection();
        } catch {
            connection = null;
        }

        const workflowId = window.crypto?.randomUUID?.() ?? `workflow-${Date.now()}`;
        beginWorkflowTracking(workflowId, window.location.href);
        setBusyState();
        applyProgressUpdate({
            workflowId,
            state: "running",
            stage: "fetch",
            percent: 6,
            message: "Starting the fetch workflow and waiting for the first server event..."
        });

        try {
            activeFetchPromise = fetch(form.action, {
                method: "POST",
                headers: {
                    "X-Requested-With": "XMLHttpRequest",
                    "X-Workflow-Id": workflowId,
                    ...(connection?.connectionId ? { "X-Progress-ConnectionId": connection.connectionId } : {})
                },
                body: new FormData(form),
                credentials: "same-origin"
            });

            const response = await activeFetchPromise;
            const contentType = response.headers.get("Content-Type") || "";
            const canReadJson = contentType.includes("application/json") || contentType.includes("application/problem+json");
            const payload = canReadJson ? await response.json() : null;

            if (response.status === 409 && payload?.workflowId) {
                beginWorkflowTracking(String(payload.workflowId), payload.redirectUrl || window.location.href);
                setBusyState();
                applyProgressUpdate({
                    workflowId: String(payload.workflowId),
                    state: "running",
                    stage: "fetch",
                    percent: 8,
                    message: "A fetch workflow is already running. Connected to the active run automatically."
                });

                return;
            }

            if (!response.ok || !payload || !payload.redirectUrl) {
                const error = new Error(payload?.detail || payload?.message || payload?.title || "Fetch Jobs request failed.");
                error.name = response.status === 409 && workflowTerminalState === "cancelled"
                    ? "WorkflowCancelled"
                    : "WorkflowFailed";
                throw error;
            }

            workflowRedirectUrl = payload.redirectUrl;

            window.setTimeout(() => {
                window.location.assign(payload.redirectUrl);
            }, 250);
        } catch (error) {
            if (isPageUnloading) {
                return;
            }

            if (!(error instanceof Error && error.name === "WorkflowCancelled" && workflowTerminalState === "cancelled")) {
                applyProgressUpdate({
                    workflowId,
                    state: "failed",
                    stage: "completed",
                    percent: 100,
                    message: error instanceof Error ? error.message : "Fetch Jobs request failed."
                });
            } else {
                isSubmitting = false;
                activeFetchPromise = null;
                setIdleState();
            }
        }
    });

    cancelButton?.addEventListener("click", async () => {
        if (!activeWorkflowId || !cancelUrl || !antiForgeryToken) {
            return;
        }

        setButtonLoading(cancelButton, true, "Cancelling...");
        appendProgressLog({
            state: "warning",
            stage: "completed",
            message: "Cancellation requested. Waiting for the current background step to stop..."
        });

        try {
            const formData = new FormData();
            formData.append("__RequestVerificationToken", antiForgeryToken);
            formData.append("workflowId", activeWorkflowId);

            const response = await fetch(cancelUrl, {
                method: "POST",
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: formData,
                credentials: "same-origin"
            });

            const contentType = response.headers.get("Content-Type") || "";
            const canReadJson = contentType.includes("application/json") || contentType.includes("application/problem+json");
            const payload = canReadJson ? await response.json() : null;

            if (!response.ok) {
                throw new Error(payload?.detail || payload?.message || payload?.title || "Could not cancel the current workflow.");
            }

            appendProgressLog({
                state: "warning",
                stage: "completed",
                message: payload?.message || "Cancellation request accepted."
            });
        } catch (error) {
            setButtonLoading(cancelButton, false);
            appendProgressLog({
                state: "failed",
                stage: "completed",
                message: error instanceof Error ? error.message : "Could not cancel the current workflow."
            });
        }
    });

    if (pageAlertMessage) {
        const severity = String(pageAlertSeverity).toLowerCase();
        showToast(pageAlertMessage, severity !== "warning" && severity !== "danger");
    }

    const persistedWorkflowState = readActiveWorkflowState();
    if (persistedWorkflowState?.workflowId) {
        isSubmitting = true;
        beginWorkflowTracking(persistedWorkflowState.workflowId, persistedWorkflowState.redirectUrl);
        setBusyState();
        applyProgressUpdate({
            workflowId: persistedWorkflowState.workflowId,
            state: "running",
            stage: "fetch",
            percent: 6,
            message: "Restoring fetch workflow status after page refresh..."
        });
    }

    jobsPage?.addEventListener("submit", (event) => {
        const formElement = event.target;
        if (!(formElement instanceof HTMLFormElement) || !formElement.classList.contains("status-form-compact")) {
            return;
        }

        const submitButton = event.submitter instanceof HTMLButtonElement ? event.submitter : null;
        if (!submitButton) {
            return;
        }

        setButtonLoading(submitButton, true, "Saving...");
    });
})();

(() => {
    const rowsBody = document.querySelector("[data-jobs-rows-body]");
    if (!rowsBody) {
        return;
    }

    let observer = null;
    let loadInFlight = false;

    rowsBody.addEventListener("click", (event) => {
        const toggle = event.target instanceof Element
            ? event.target.closest("[data-job-row-toggle]")
            : null;

        if (!toggle) {
            return;
        }

        const targetId = toggle.getAttribute("data-target-id");
        if (!targetId) {
            return;
        }

        const detailRow = rowsBody.querySelector(`#${targetId}`);
        if (!detailRow) {
            return;
        }

        const isExpanded = toggle.getAttribute("aria-expanded") === "true";
        const nextExpanded = !isExpanded;

        toggle.setAttribute("aria-expanded", nextExpanded ? "true" : "false");
        toggle.classList.toggle("is-open", nextExpanded);
        detailRow.classList.toggle("is-expanded", nextExpanded);
        detailRow.classList.toggle("is-collapsed", !nextExpanded);
        detailRow.setAttribute("aria-hidden", nextExpanded ? "false" : "true");

        const label = toggle.querySelector(".job-expand-label");
        if (label) {
            label.textContent = nextExpanded ? "Less" : "More";
        }
    });

    const loadNextRows = async (sentinelRow) => {
        if (!sentinelRow || loadInFlight) {
            return;
        }

        const loadUrl = sentinelRow.dataset.jobsLoadMoreUrl;
        if (!loadUrl) {
            return;
        }

        loadInFlight = true;
        sentinelRow.classList.add("is-loading");

        const button = sentinelRow.querySelector("[data-jobs-load-more-button]");
        const status = sentinelRow.querySelector("[data-jobs-load-more-status]");
        const idleButtonText = button ? button.textContent : null;

        if (button) {
            button.disabled = true;
            button.textContent = "Loading...";
        }

        if (status) {
            status.textContent = "Loading next jobs batch...";
        }

        try {
            const response = await fetch(loadUrl, {
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                credentials: "same-origin"
            });

            if (!response.ok) {
                throw new Error("Could not load the next jobs batch.");
            }

            const html = await response.text();
            sentinelRow.remove();
            rowsBody.insertAdjacentHTML("beforeend", html);
            attachLazySentinel();
        } catch {
            sentinelRow.classList.remove("is-loading");

            if (button) {
                button.disabled = false;
                if (idleButtonText) {
                    button.textContent = idleButtonText;
                }
            }

            if (status) {
                status.textContent = "Could not load more jobs. Retry.";
            }
        } finally {
            loadInFlight = false;
        }
    };

    const attachLazySentinel = () => {
        if (observer) {
            observer.disconnect();
        }

        const sentinelRow = rowsBody.querySelector("[data-jobs-lazy-sentinel]");
        if (!sentinelRow) {
            return;
        }

        const button = sentinelRow.querySelector("[data-jobs-load-more-button]");
        if (button) {
            button.onclick = () => {
                void loadNextRows(sentinelRow);
            };
        }

        observer = new IntersectionObserver(
            (entries) => {
                const isVisible = entries.some((entry) => entry.isIntersecting);
                if (isVisible) {
                    void loadNextRows(sentinelRow);
                }
            },
            {
                rootMargin: "220px 0px"
            });

        observer.observe(sentinelRow);
    };

    attachLazySentinel();
})();

(() => {
    const showToast = window.appToast?.show ?? (() => {});
    const pendingStorageKey = "jobs.pendingScoreRequests";
    const activeScoreRequests = new Set();

    const readPendingEntries = () => {
        try {
            const payload = window.sessionStorage.getItem(pendingStorageKey);
            if (!payload) {
                return {};
            }

            const parsed = JSON.parse(payload);
            return parsed && typeof parsed === "object" ? parsed : {};
        } catch {
            return {};
        }
    };

    const writePendingEntries = (entries) => {
        try {
            if (Object.keys(entries).length === 0) {
                window.sessionStorage.removeItem(pendingStorageKey);
                return;
            }

            window.sessionStorage.setItem(pendingStorageKey, JSON.stringify(entries));
        } catch {
        }
    };

    const markPending = (jobId) => {
        const entries = readPendingEntries();
        entries[jobId] = { startedAt: Date.now() };
        writePendingEntries(entries);
    };

    const clearPending = (jobId) => {
        const entries = readPendingEntries();
        delete entries[jobId];
        writePendingEntries(entries);
    };

    const getRestorablePendingJobIds = () => {
        const now = Date.now();
        const entries = readPendingEntries();
        const retainedEntries = {};
        const jobIds = [];

        for (const [jobId, metadata] of Object.entries(entries)) {
            const startedAt = Number(metadata?.startedAt);
            if (!Number.isFinite(startedAt) || (now - startedAt) > 5 * 60 * 1000) {
                continue;
            }

            retainedEntries[jobId] = { startedAt };
            jobIds.push(jobId);
        }

        writePendingEntries(retainedEntries);
        return jobIds;
    };

    const getJobRoots = (jobId) => Array.from(document.querySelectorAll(`[data-job-ui-root][data-job-id="${jobId}"]`));

    const getJobButtons = (jobId) => Array.from(document.querySelectorAll(`[data-score-job-form][data-job-id="${jobId}"] [data-score-job-button]`));

    const parseInteger = (value) => {
        const parsed = Number.parseInt(String(value ?? ""), 10);
        return Number.isFinite(parsed) ? parsed : 0;
    };

    const setIntegerText = (element, value) => {
        if (!(element instanceof HTMLElement)) {
            return;
        }

        element.textContent = String(Math.max(0, value));
    };

    const applyDashboardStatsDelta = (job, previousState) => {
        if (!job) {
            return;
        }

        const scoredNode = document.querySelector("[data-dashboard-scored-jobs]");
        const strongMatchesNode = document.querySelector("[data-dashboard-strong-matches]");
        const unscoredNode = document.querySelector("[data-dashboard-unscored-jobs]");

        if (!(scoredNode instanceof HTMLElement) ||
            !(strongMatchesNode instanceof HTMLElement) ||
            !(unscoredNode instanceof HTMLElement)) {
            return;
        }

        const isNowScored = Number.isFinite(Number(job.aiScore)) && Boolean(job.scoredAtUtc);
        const wasScored = Boolean(previousState?.wasScored);

        if (!wasScored && isNowScored) {
            setIntegerText(scoredNode, parseInteger(scoredNode.textContent) + 1);
            setIntegerText(unscoredNode, parseInteger(unscoredNode.textContent) - 1);
        }

        const previousLabel = previousState?.aiLabel || "";
        const nextLabel = job.aiLabel || "";

        if (previousLabel !== "StrongMatch" && nextLabel === "StrongMatch") {
            setIntegerText(strongMatchesNode, parseInteger(strongMatchesNode.textContent) + 1);
            return;
        }

        if (previousLabel === "StrongMatch" && nextLabel !== "StrongMatch") {
            setIntegerText(strongMatchesNode, parseInteger(strongMatchesNode.textContent) - 1);
        }
    };

    const setScoreButtonState = (button, state) => {
        if (!(button instanceof HTMLButtonElement)) {
            return;
        }

        const spinner = button.querySelector("[data-score-spinner]");
        const text = button.querySelector("[data-score-button-text]");

        if (state === "busy") {
            button.disabled = true;
            button.dataset.scoreComplete = "false";
            spinner?.classList.remove("d-none");
            if (text) {
                text.textContent = button.dataset.busyText || "Scoring...";
            }

            return;
        }

        spinner?.classList.add("d-none");

        if (state === "done") {
            button.disabled = true;
            button.dataset.scoreComplete = "true";
            if (text) {
                text.textContent = button.dataset.doneText || "Scored";
            }

            return;
        }

        button.disabled = false;
        button.dataset.scoreComplete = "false";
        if (text) {
            text.textContent = button.dataset.idleText || "Score";
        }
    };

    const updateLabelBadge = (badge, aiLabel) => {
        if (!(badge instanceof HTMLElement)) {
            return;
        }

        badge.textContent = aiLabel || "Pending";
        badge.classList.remove(
            "bg-success-subtle",
            "text-success-emphasis",
            "bg-warning-subtle",
            "text-warning-emphasis",
            "bg-secondary-subtle",
            "text-secondary-emphasis",
            "bg-light",
            "text-dark");

        if (aiLabel === "StrongMatch") {
            badge.classList.add("bg-success-subtle", "text-success-emphasis");
        } else if (aiLabel === "Review") {
            badge.classList.add("bg-warning-subtle", "text-warning-emphasis");
        } else if (aiLabel === "Skip") {
            badge.classList.add("bg-secondary-subtle", "text-secondary-emphasis");
        } else {
            badge.classList.add("bg-light", "text-dark");
        }
    };

    const updateSignalSection = (root, sectionSelector, textSelector, value) => {
        const section = root.querySelector(sectionSelector);
        const text = root.querySelector(textSelector);

        if (!(section instanceof HTMLElement) || !(text instanceof HTMLElement)) {
            return;
        }

        text.textContent = value || "";
        section.classList.toggle("d-none", !value);
    };

    const formatLocalTimestamp = (isoValue) => {
        const date = new Date(isoValue);
        if (Number.isNaN(date.getTime())) {
            return "Just now";
        }

        return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")} ${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
    };

    const updateJobUi = (job) => {
        if (!job?.id) {
            return;
        }

        for (const root of getJobRoots(job.id)) {
            const scoreShell = root.querySelector("[data-job-score-shell]");
            const scoreNumber = root.querySelector("[data-job-score-number]");
            const scoreEmpty = root.querySelector("[data-job-score-empty]");
            const labelBadge = root.querySelector("[data-job-label-badge]");
            const summaryText = root.querySelector("[data-job-ai-summary-text]");
            const summaryEmpty = root.querySelector("[data-job-ai-summary-empty]");
            const signalsStack = root.querySelector("[data-job-ai-signals]");
            const signalsEmpty = root.querySelector("[data-job-ai-signals-empty]");
            const scoredAtValue = root.querySelector("[data-job-scored-at-value]");

            if (scoreShell instanceof HTMLElement) {
                scoreShell.classList.remove("d-none");
            }

            if (scoreNumber instanceof HTMLElement) {
                scoreNumber.textContent = String(job.aiScore);
            }

            if (scoreEmpty instanceof HTMLElement) {
                scoreEmpty.classList.add("d-none");
            }

            updateLabelBadge(labelBadge, job.aiLabel);

            if (summaryText instanceof HTMLElement) {
                summaryText.textContent = job.aiSummary || "";
                summaryText.classList.toggle("d-none", !job.aiSummary);
                if (job.aiOutputDirection) {
                    summaryText.setAttribute("dir", job.aiOutputDirection);
                }
                if (job.aiOutputLanguageCode) {
                    summaryText.setAttribute("lang", job.aiOutputLanguageCode);
                }
            }

            if (summaryEmpty instanceof HTMLElement) {
                summaryEmpty.classList.toggle("d-none", Boolean(job.aiSummary));
            }

            updateSignalSection(root, "[data-job-why-section]", "[data-job-why-text]", job.aiWhyMatched);
            updateSignalSection(root, "[data-job-concerns-section]", "[data-job-concerns-text]", job.aiConcerns);

            const hasSignals = Boolean(job.aiWhyMatched) || Boolean(job.aiConcerns);
            if (signalsStack instanceof HTMLElement) {
                signalsStack.classList.toggle("d-none", !hasSignals);
            }

            if (signalsEmpty instanceof HTMLElement) {
                signalsEmpty.classList.toggle("d-none", hasSignals);
            }

            if (scoredAtValue instanceof HTMLElement) {
                scoredAtValue.textContent = formatLocalTimestamp(job.scoredAtUtc);
            }
        }

        for (const button of getJobButtons(job.id)) {
            setScoreButtonState(button, "done");
        }
    };

    const submitScore = async (form, { restoring = false } = {}) => {
        if (!(form instanceof HTMLFormElement)) {
            return;
        }

        const jobId = form.dataset.jobId || form.querySelector('input[name="jobId"]')?.value;
        const button = form.querySelector("[data-score-job-button]");

        if (!jobId || !(button instanceof HTMLButtonElement)) {
            return;
        }

        const existingLabel = getJobRoots(jobId)
            .map((root) => root.querySelector("[data-job-label-badge]")?.textContent?.trim())
            .find((value) => Boolean(value)) || "";
        const previousState = {
            wasScored: button.dataset.scoreComplete === "true",
            aiLabel: existingLabel
        };

        if (button.dataset.scoreComplete === "true" || activeScoreRequests.has(jobId)) {
            clearPending(jobId);
            return;
        }

        activeScoreRequests.add(jobId);
        markPending(jobId);

        for (const candidate of getJobButtons(jobId)) {
            setScoreButtonState(candidate, "busy");
        }

        try {
            const response = await fetch(form.action, {
                method: "POST",
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: new FormData(form),
                credentials: "same-origin"
            });

            const contentType = response.headers.get("Content-Type") || "";
            const canReadJson = contentType.includes("application/json") || contentType.includes("application/problem+json");
            const payload = canReadJson ? await response.json() : null;

            if (!payload) {
                throw new Error("AI scoring response could not be read.");
            }

            if (payload.job) {
                applyDashboardStatsDelta(payload.job, previousState);
                updateJobUi(payload.job);
            }

            if (!response.ok || !payload.success) {
                for (const candidate of getJobButtons(jobId)) {
                    if (candidate.dataset.scoreComplete !== "true") {
                        setScoreButtonState(candidate, "idle");
                    }
                }

                throw new Error(payload.message || payload.detail || "AI scoring failed.");
            }

            clearPending(jobId);
            showToast(payload.message || "AI scoring completed.", true);
        } catch (error) {
            clearPending(jobId);

            for (const candidate of getJobButtons(jobId)) {
                if (candidate.dataset.scoreComplete !== "true") {
                    setScoreButtonState(candidate, "idle");
                }
            }

            const message = error instanceof Error ? error.message : "AI scoring failed.";
            showToast(restoring ? `Restored score request failed: ${message}` : message, false);
        } finally {
            activeScoreRequests.delete(jobId);
        }
    };

    document.addEventListener("submit", (event) => {
        const form = event.target instanceof HTMLFormElement && event.target.matches("[data-score-job-form]")
            ? event.target
            : null;

        if (!form) {
            return;
        }

        event.preventDefault();
        void submitScore(form);
    });

    const restorePendingRequests = () => {
        for (const jobId of getRestorablePendingJobIds()) {
            const form = document.querySelector(`[data-score-job-form][data-job-id="${jobId}"]`);
            if (form instanceof HTMLFormElement) {
                void submitScore(form, { restoring: true });
            }
        }
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", restorePendingRequests, { once: true });
    } else {
        restorePendingRequests();
    }
})();
