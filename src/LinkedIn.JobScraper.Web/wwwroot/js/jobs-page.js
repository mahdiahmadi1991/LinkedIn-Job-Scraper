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
    const stageOrder = ["fetch", "enrichment", "scoring"];
    const pollUrl = form.dataset.progressPollUrl;
    const cancelUrl = form.dataset.cancelUrl;
    const antiForgeryToken = form.querySelector('input[name="__RequestVerificationToken"]')?.value ?? "";
    const jobsPage = form.closest(".jobs-page");
    const pageAlertMessage = jobsPage?.dataset.pageAlertMessage?.trim() ?? "";
    const pageAlertSeverity = jobsPage?.dataset.pageAlertSeverity ?? "info";
    const showToast = window.appToast?.show ?? (() => {});
    const setButtonLoading = window.appButtons?.setLoading ?? (() => {});
    const busySpinner = form.querySelector("[data-busy-spinner]");
    const workflowStorageKey = "jobs.activeWorkflowId";
    const prefersReducedMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches ?? false;
    let signalRConnection = null;
    let isSubmitting = false;
    let lastLoggedMessage = null;
    let activeWorkflowId = null;
    let lastSequence = 0;
    let workflowTerminalState = null;
    let workflowPollingTimer = null;
    let activeFetchPromise = null;

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

    const stopPolling = () => {
        if (!workflowPollingTimer) {
            return;
        }

        window.clearTimeout(workflowPollingTimer);
        workflowPollingTimer = null;
    };

    const storeActiveWorkflowId = (workflowId) => {
        try {
            if (workflowId) {
                window.sessionStorage.setItem(workflowStorageKey, workflowId);
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

    const readActiveWorkflowId = () => {
        try {
            return window.sessionStorage.getItem(workflowStorageKey);
        } catch {
            return null;
        }
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

        if (["completed", "failed", "cancelled"].includes(update.state)) {
            workflowTerminalState = update.state;
            stopPolling();
            isSubmitting = false;
            activeFetchPromise = null;
            clearActiveWorkflowId();
            setIdleState();
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

    const beginWorkflowTracking = (workflowId) => {
        activeWorkflowId = workflowId;
        lastSequence = 0;
        lastLoggedMessage = null;
        workflowTerminalState = null;
        storeActiveWorkflowId(workflowId);
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
        beginWorkflowTracking(workflowId);
        setBusyState();
        applyProgressUpdate({
            workflowId,
            state: "running",
            stage: "fetch",
            percent: 6,
            message: "Starting Fetch & Score workflow and waiting for the first server event..."
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

            if (!response.ok || !payload || !payload.redirectUrl) {
                const error = new Error(payload?.detail || payload?.message || payload?.title || "Fetch & Score request failed.");
                error.name = response.status === 409 && workflowTerminalState === "cancelled"
                    ? "WorkflowCancelled"
                    : "WorkflowFailed";
                throw error;
            }

            window.setTimeout(() => {
                window.location.assign(payload.redirectUrl);
            }, 250);
        } catch (error) {
            if (!(error instanceof Error && error.name === "WorkflowCancelled" && workflowTerminalState === "cancelled")) {
                applyProgressUpdate({
                    workflowId,
                    state: "failed",
                    stage: "completed",
                    percent: 100,
                    message: error instanceof Error ? error.message : "Fetch & Score request failed."
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

    const persistedWorkflowId = readActiveWorkflowId();
    if (persistedWorkflowId) {
        isSubmitting = true;
        beginWorkflowTracking(persistedWorkflowId);
        setBusyState();
        applyProgressUpdate({
            workflowId: persistedWorkflowId,
            state: "running",
            stage: "fetch",
            percent: 6,
            message: "Restoring workflow status after page refresh..."
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
