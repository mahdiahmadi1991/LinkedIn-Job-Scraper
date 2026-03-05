(() => {
    const root = document.querySelector("[data-ai-live-review]");
    if (!root) {
        return;
    }

    const startForm = root.querySelector("[data-live-review-start-form]");
    const startButton = root.querySelector("[data-live-review-start]");
    const loadLatestButton = root.querySelector("[data-live-review-load-latest]");
    const resumeButton = root.querySelector("[data-live-review-resume]");
    const stopButton = root.querySelector("[data-live-review-stop]");
    const statusNode = root.querySelector("[data-live-review-status]");
    const emptyNode = root.querySelector("[data-live-review-empty]");
    const tableShell = root.querySelector("[data-live-review-table-shell]");
    const rowsBody = root.querySelector("[data-live-review-rows]");
    const filterSelect = root.querySelector("[data-live-review-filter]");
    const reportLogNode = root.querySelector("[data-live-review-log]");
    const reportLogEmptyNode = root.querySelector("[data-live-review-log-empty]");
    const reportLogContentNode = root.querySelector("[data-live-review-log-content]");
    const reportLogToggleButton = root.querySelector("[data-live-review-log-toggle]");
    const paginationNode = root.querySelector("[data-live-review-pagination]");
    const loadMoreButton = root.querySelector("[data-live-review-load-more]");
    const paginationStatusNode = root.querySelector("[data-live-review-pagination-status]");

    const stateNode = root.querySelector("[data-live-review-state]");
    const eligibleTotalNode = root.querySelector("[data-live-review-eligible-total]");
    const alreadyReviewedNode = root.querySelector("[data-live-review-already-reviewed]");
    const queueRemainingNode = root.querySelector("[data-live-review-queue-remaining]");
    const candidatesNode = root.querySelector("[data-live-review-candidates]");
    const processedNode = root.querySelector("[data-live-review-processed]");
    const acceptedNode = root.querySelector("[data-live-review-accepted]");
    const needsReviewNode = root.querySelector("[data-live-review-needs-review]");
    const failedNode = root.querySelector("[data-live-review-failed]");

    const startUrl = root.dataset.startUrl;
    const latestUrl = root.dataset.latestUrl;
    const overviewUrl = root.dataset.overviewUrl;
    const runUrlTemplate = root.dataset.runUrlTemplate;
    const resumeUrlTemplate = root.dataset.resumeUrlTemplate;
    const cancelUrlTemplate = root.dataset.cancelUrlTemplate;
    const progressUrlTemplate = root.dataset.progressUrlTemplate;
    const progressHubUrl = root.dataset.progressHubUrl || "/hubs/ai-global-shortlist-progress";
    const jobDetailsUrl = root.dataset.jobDetailsUrl;
    const showRunCandidatesMetric = String(root.dataset.showRunCandidatesMetric || "true").toLowerCase() === "true";

    const showToast = window.appToast?.show ?? (() => {});
    const setButtonLoading = window.appButtons?.setLoading ?? (() => {});
    const prefersReducedMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches ?? false;
    const startButtonDefaultHtml = startButton instanceof HTMLButtonElement
        ? startButton.innerHTML
        : "";

    const tablePageSize = 10;
    const terminalStates = new Set(["Completed", "Failed", "Cancelled"]);
    const maxReportLogItems = 180;
    const reportHeartbeatIntervalMs = 3200;
    const reportHeartbeatSilenceMs = 6500;
    const overviewRefreshDebounceMs = 900;

    let activeRunId = null;
    let activeRunState = "Idle";
    let lastSequence = 0;
    let pollTimer = null;
    let connection = null;
    let loading = false;
    let itemsByJobId = new Map();
    let filterValue = "high";
    let visibleCount = tablePageSize;
    let lastLogKey = null;
    let isReportLogCollapsed = false;
    let loadMoreObserver = null;
    let autoLoadInFlight = false;
    let isInitialSyncPending = true;
    let reportHeartbeatTimer = null;
    let lastReportEventAt = 0;
    let overviewRefreshTimer = null;
    let overviewRefreshInFlight = false;
    let runCancellationRequestedAtUtc = null;

    const seenProgressEvents = new Set();

    const setStatus = (message, isError = false) => {
        if (!(statusNode instanceof HTMLElement)) {
            return;
        }

        statusNode.textContent = message;
        statusNode.classList.toggle("is-error", isError);
    };

    const setMetric = (node, value) => {
        if (node instanceof HTMLElement) {
            node.textContent = String(value ?? "0");
        }
    };

    const toDisplayState = (state) => {
        if (typeof state !== "string" || state.length === 0) {
            return "Running";
        }

        const normalized = state.trim().toLowerCase();
        if (normalized === "pending") {
            return "Pending";
        }

        if (normalized === "running") {
            return "Running";
        }

        if (normalized === "completed") {
            return "Completed";
        }

        if (normalized === "failed") {
            return "Failed";
        }

        if (normalized === "cancelled") {
            return "Cancelled";
        }

        if (normalized === "idle") {
            return "Idle";
        }

        return state;
    };

    const normalizeDecision = (value) => {
        if (typeof value !== "string") {
            return "NeedsReview";
        }

        const normalized = value.trim().toLowerCase();
        if (normalized === "accepted") {
            return "Accepted";
        }

        if (normalized === "rejected") {
            return "Rejected";
        }

        return "NeedsReview";
    };

    const normalizeFitLevel = (value) => {
        if (typeof value !== "string") {
            return "Medium";
        }

        const normalized = value.trim().toLowerCase();
        if (normalized === "high") {
            return "High";
        }

        if (normalized === "low") {
            return "Low";
        }

        return "Medium";
    };

    const getFitLevelFromSignals = (score, decision) => {
        if (Number.isFinite(Number(score))) {
            const scoreValue = Number(score);
            if (scoreValue >= 80) {
                return "High";
            }

            if (scoreValue <= 49) {
                return "Low";
            }

            return "Medium";
        }

        const normalizedDecision = normalizeDecision(decision);
        if (normalizedDecision === "Accepted") {
            return "High";
        }

        if (normalizedDecision === "Rejected") {
            return "Low";
        }

        return "Medium";
    };

    const getFitLevelClass = (fitLevel) => {
        if (fitLevel === "High") {
            return "is-accepted";
        }

        if (fitLevel === "Low") {
            return "is-rejected";
        }

        return "is-needs-review";
    };

    const formatTimeLabel = (value) => {
        const timestamp = value ? new Date(value) : new Date();
        return new Intl.DateTimeFormat([], {
            hour: "2-digit",
            minute: "2-digit",
            second: "2-digit"
        }).format(timestamp);
    };

    const renderBusyButtonHtml = (text) => {
        const safeText = text || "Working...";
        return `
            <span class="button-loading-shell">
                <span class="button-loading-spinner" aria-hidden="true"></span>
                <span>${safeText}</span>
            </span>
        `;
    };

    const setStartButtonVisualState = (state) => {
        if (!(startButton instanceof HTMLButtonElement)) {
            return;
        }

        if (state === "syncing") {
            startButton.innerHTML = renderBusyButtonHtml("Syncing...");
            return;
        }

        if (state === "running") {
            startButton.innerHTML = renderBusyButtonHtml("Running...");
            return;
        }

        startButton.innerHTML = startButtonDefaultHtml;
    };

    const updateActionButtons = () => {
        if (isInitialSyncPending) {
            if (startButton instanceof HTMLButtonElement) {
                startButton.disabled = true;
                startButton.classList.add("disabled");
            }

            if (resumeButton instanceof HTMLButtonElement) {
                resumeButton.disabled = true;
                resumeButton.classList.add("disabled");
            }

            if (stopButton instanceof HTMLButtonElement) {
                stopButton.disabled = true;
                stopButton.classList.add("disabled");
            }

            setStartButtonVisualState("syncing");

            return;
        }

        const hasRun = typeof activeRunId === "string" && activeRunId.length > 0;
        const normalizedState = String(activeRunState || "").toLowerCase();
        const isRunningLike = normalizedState === "running" || normalizedState === "pending";
        const cancellationRequested = Boolean(runCancellationRequestedAtUtc);
        const canStart = !isRunningLike;
        const canResume = hasRun && (normalizedState === "cancelled" || normalizedState === "failed");
        const canStop = hasRun && isRunningLike && !cancellationRequested;

        if (startButton instanceof HTMLButtonElement) {
            startButton.disabled = !canStart;
            startButton.classList.toggle("disabled", !canStart);
        }

        if (resumeButton instanceof HTMLButtonElement) {
            resumeButton.disabled = !canResume;
            resumeButton.classList.toggle("disabled", !canResume);
        }

        if (stopButton instanceof HTMLButtonElement) {
            stopButton.disabled = !canStop;
            stopButton.classList.toggle("disabled", !canStop);
        }

        setStartButtonVisualState(isRunningLike ? "running" : "default");
    };

    const updateRunSummary = (summary) => {
        if (stateNode instanceof HTMLElement) {
            stateNode.textContent = summary?.state ?? "Idle";
        }

        setMetric(candidatesNode, summary?.candidateCount ?? 0);
        setMetric(processedNode, summary?.processedCount ?? 0);
        setMetric(acceptedNode, summary?.acceptedCount ?? 0);
        setMetric(needsReviewNode, summary?.needsReviewCount ?? 0);
        setMetric(failedNode, summary?.failedCount ?? 0);
    };

    const updateQueueOverview = (overview) => {
        setMetric(eligibleTotalNode, overview?.eligibleTotal ?? 0);
        setMetric(alreadyReviewedNode, overview?.alreadyReviewed ?? 0);
        setMetric(queueRemainingNode, overview?.queueRemaining ?? 0);
    };

    const loadOverview = async () => {
        if (!overviewUrl || overviewRefreshInFlight) {
            return;
        }

        overviewRefreshInFlight = true;
        try {
            const response = await fetch(overviewUrl, {
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                credentials: "same-origin"
            });

            if (!response.ok) {
                return;
            }

            const payload = await response.json();
            if (!payload?.success || !payload.overview) {
                return;
            }

            updateQueueOverview(payload.overview);
        } catch {
            // best-effort live sync for overview metrics
        } finally {
            overviewRefreshInFlight = false;
        }
    };

    const scheduleOverviewRefresh = (delayMs = overviewRefreshDebounceMs) => {
        if (!overviewUrl) {
            return;
        }

        if (overviewRefreshTimer) {
            window.clearTimeout(overviewRefreshTimer);
        }

        overviewRefreshTimer = window.setTimeout(() => {
            overviewRefreshTimer = null;
            void loadOverview();
        }, Math.max(0, Number(delayMs) || 0));
    };

    const scrollReportToLatest = () => {
        if (!(reportLogNode instanceof HTMLElement)) {
            return;
        }

        const top = reportLogNode.scrollHeight;
        const behavior = prefersReducedMotion ? "auto" : "smooth";

        if (typeof reportLogNode.scrollTo === "function") {
            reportLogNode.scrollTo({
                top,
                behavior
            });
            return;
        }

        reportLogNode.scrollTop = top;
    };

    const syncReportLogCollapseUi = () => {
        if (reportLogContentNode instanceof HTMLElement) {
            reportLogContentNode.classList.toggle("is-collapsed", isReportLogCollapsed);
        }

        if (reportLogToggleButton instanceof HTMLButtonElement) {
            reportLogToggleButton.setAttribute("aria-expanded", isReportLogCollapsed ? "false" : "true");
            const label = isReportLogCollapsed
                ? "Expand live activity report"
                : "Collapse live activity report";
            reportLogToggleButton.setAttribute("aria-label", label);
            reportLogToggleButton.setAttribute("title", label);
        }
    };

    const setReportLogCollapsed = (collapsed) => {
        isReportLogCollapsed = collapsed;
        syncReportLogCollapseUi();
    };

    const clearReportLog = () => {
        if (reportLogNode instanceof HTMLElement) {
            reportLogNode.innerHTML = "";
            reportLogNode.classList.add("d-none");
        }

        if (reportLogEmptyNode instanceof HTMLElement) {
            reportLogEmptyNode.classList.remove("d-none");
        }

        lastLogKey = null;
        lastReportEventAt = 0;
    };

    const stopReportHeartbeat = () => {
        if (!reportHeartbeatTimer) {
            return;
        }

        window.clearInterval(reportHeartbeatTimer);
        reportHeartbeatTimer = null;
    };

    const startReportHeartbeat = () => {
        stopReportHeartbeat();
        lastReportEventAt = Date.now();

        reportHeartbeatTimer = window.setInterval(() => {
            if (!activeRunId || terminalStates.has(activeRunState)) {
                return;
            }

            const silentDurationMs = Date.now() - lastReportEventAt;
            if (silentDurationMs < reportHeartbeatSilenceMs) {
                return;
            }

            const silentSeconds = Math.max(1, Math.floor(silentDurationMs / 1000));
            appendUiLog(
                `Run is active. Waiting for next candidate update (${silentSeconds}s idle)...`,
                "running",
                "heartbeat",
                { bypassDedupe: true });
        }, reportHeartbeatIntervalMs);
    };

    const syncReportHeartbeat = () => {
        if (activeRunId && !terminalStates.has(activeRunState)) {
            startReportHeartbeat();
            return;
        }

        stopReportHeartbeat();
    };

    const appendReportLog = (update, occurredAtUtc, eventSequence = null, options = {}) => {
        if (!(reportLogNode instanceof HTMLElement) || !update?.message) {
            return;
        }

        const bypassDedupe = Boolean(options.bypassDedupe);
        const dedupeKey = Number.isFinite(Number(eventSequence))
            ? `sequence:${eventSequence}`
            : `${update.state}|${update.stage}|${update.message}|${update.processedCount}|${update.acceptedCount}|${update.needsReviewCount}|${update.failedCount}`;

        if (!bypassDedupe && lastLogKey === dedupeKey) {
            return;
        }

        lastLogKey = dedupeKey;
        lastReportEventAt = Date.now();

        if (reportLogEmptyNode instanceof HTMLElement) {
            reportLogEmptyNode.classList.add("d-none");
        }

        reportLogNode.classList.remove("d-none");

        const stateLabel = toDisplayState(update.state || "running");
        const stageLabel = typeof update.stage === "string" && update.stage.length > 0
            ? update.stage
            : "progress";

        const item = document.createElement("div");
        item.className = "progress-activity-item";

        if (stateLabel === "Completed") {
            item.classList.add("completed");
        } else if (stateLabel === "Failed") {
            item.classList.add("failed");
        } else if (stateLabel === "Cancelled" || stateLabel === "Pending") {
            item.classList.add("warning");
        }

        const meta = document.createElement("div");
        meta.className = "progress-activity-meta";

        const timeNode = document.createElement("span");
        timeNode.className = "progress-activity-time";
        timeNode.textContent = formatTimeLabel(occurredAtUtc);

        const stateMetaNode = document.createElement("span");
        stateMetaNode.className = "progress-activity-state";
        stateMetaNode.textContent = `${stateLabel} · ${stageLabel}`;

        meta.append(timeNode, stateMetaNode);

        const message = document.createElement("div");
        message.className = "progress-activity-message";
        message.textContent = update.message;

        item.append(meta, message);

        const detailParts = [];
        if (showRunCandidatesMetric &&
            Number.isFinite(Number(update.candidateCount)) &&
            Number(update.candidateCount) > 0) {
            detailParts.push(`Candidates: ${update.candidateCount}`);
        }
        if (Number.isFinite(Number(update.processedCount)) && Number(update.processedCount) >= 0) {
            detailParts.push(`Processed: ${update.processedCount}`);
        }
        if (Number.isFinite(Number(update.acceptedCount)) && Number(update.acceptedCount) >= 0) {
            detailParts.push(`Accepted: ${update.acceptedCount}`);
        }
        if (Number.isFinite(Number(update.needsReviewCount)) && Number(update.needsReviewCount) >= 0) {
            detailParts.push(`Needs Review: ${update.needsReviewCount}`);
        }
        if (Number.isFinite(Number(update.failedCount)) && Number(update.failedCount) >= 0) {
            detailParts.push(`Failed: ${update.failedCount}`);
        }

        if (detailParts.length > 0) {
            const details = document.createElement("div");
            details.className = "progress-activity-details";
            details.textContent = detailParts.join(" · ");
            item.appendChild(details);
        }

        reportLogNode.appendChild(item);
        while (reportLogNode.children.length > maxReportLogItems) {
            reportLogNode.removeChild(reportLogNode.firstElementChild);
        }

        window.requestAnimationFrame(() => {
            item.classList.add("is-visible");
            scrollReportToLatest();
        });
    };

    const appendUiLog = (message, state = "running", stage = "ui", options = {}) => {
        appendReportLog(
            {
                state,
                stage,
                message,
                candidateCount: Number(candidatesNode?.textContent || 0),
                processedCount: Number(processedNode?.textContent || 0),
                acceptedCount: Number(acceptedNode?.textContent || 0),
                needsReviewCount: Number(needsReviewNode?.textContent || 0),
                failedCount: Number(failedNode?.textContent || 0)
            },
            new Date().toISOString(),
            null,
            options);
    };

    const buildFilteredRows = () => {
        return Array
            .from(itemsByJobId.values())
            .filter((item) => filterValue === "all" || String(item.fitLevel || "").toLowerCase() === filterValue)
            .sort((a, b) => {
                const rankA = Number.isFinite(Number(a.rank)) ? Number(a.rank) : -1;
                const rankB = Number.isFinite(Number(b.rank)) ? Number(b.rank) : -1;
                if (rankA !== rankB) {
                    return rankB - rankA;
                }

                return String(b.linkedInJobId || "").localeCompare(String(a.linkedInJobId || ""));
            });
    };

    const updatePagination = (totalRows, renderedRows) => {
        const hasMore = renderedRows < totalRows;

        if (paginationNode instanceof HTMLElement) {
            paginationNode.classList.toggle("d-none", !hasMore);
        }

        if (loadMoreButton instanceof HTMLButtonElement) {
            loadMoreButton.disabled = !hasMore;
        }

        if (paginationStatusNode instanceof HTMLElement) {
            paginationStatusNode.textContent = totalRows > 0
                ? `Showing ${renderedRows} of ${totalRows} row(s).`
                : "";
        }
    };

    const ensureLoadMoreObserver = () => {
        if (!(paginationNode instanceof HTMLElement) || typeof IntersectionObserver !== "function") {
            return;
        }

        if (loadMoreObserver) {
            loadMoreObserver.disconnect();
        }

        loadMoreObserver = new IntersectionObserver(
            (entries) => {
                const isVisible = entries.some((entry) => entry.isIntersecting);
                if (!isVisible || autoLoadInFlight) {
                    return;
                }

                autoLoadInFlight = true;
                window.setTimeout(() => {
                    autoLoadInFlight = false;
                    const filteredRows = buildFilteredRows();
                    if (visibleCount >= filteredRows.length) {
                        return;
                    }

                    visibleCount = Math.min(filteredRows.length, visibleCount + tablePageSize);
                    renderRows();
                }, 120);
            },
            {
                rootMargin: "220px 0px"
            });

        loadMoreObserver.observe(paginationNode);
    };

    const renderRows = () => {
        if (!(rowsBody instanceof HTMLElement)) {
            return;
        }

        const filteredRows = buildFilteredRows();
        const rowsToRender = filteredRows.slice(0, Math.max(tablePageSize, visibleCount));

        rowsBody.innerHTML = "";

        for (const item of rowsToRender) {
            const row = document.createElement("tr");

            const rankCell = document.createElement("td");
            rankCell.textContent = Number.isFinite(Number(item.rank))
                ? `#${item.rank}`
                : "—";

            const jobCell = document.createElement("td");
            const titleLink = document.createElement("a");
            titleLink.className = "ai-live-review-job-link";
            titleLink.href = buildJobDetailsLink(item.jobId);
            titleLink.textContent = item.jobTitle || item.linkedInJobId || "Unknown job";
            jobCell.appendChild(titleLink);

            const meta = document.createElement("div");
            meta.className = "ai-live-review-job-meta";
            meta.textContent = [item.companyName, item.locationName].filter(Boolean).join(" · ");
            if (meta.textContent) {
                jobCell.appendChild(meta);
            }

            const decisionCell = document.createElement("td");
            const decisionBadge = document.createElement("span");
            decisionBadge.className = `ai-live-review-decision ${getFitLevelClass(item.fitLevel)}`;
            decisionBadge.textContent = item.fitLevel || "Medium";
            decisionCell.appendChild(decisionBadge);

            const scoreCell = document.createElement("td");
            scoreCell.textContent = Number.isFinite(Number(item.score))
                ? String(item.score)
                : "—";

            const confidenceCell = document.createElement("td");
            confidenceCell.textContent = Number.isFinite(Number(item.confidence))
                ? `${item.confidence}%`
                : "—";

            const reasonCell = document.createElement("td");
            reasonCell.className = "ai-live-review-reason";
            reasonCell.setAttribute("dir", "auto");
            reasonCell.textContent = item.recommendationReason || "—";
            if (item.concerns) {
                const concerns = document.createElement("div");
                concerns.className = "ai-live-review-concerns";
                concerns.setAttribute("dir", "auto");
                concerns.textContent = item.concerns;
                reasonCell.appendChild(concerns);
            }

            const errorCell = document.createElement("td");
            errorCell.textContent = item.errorCode || "—";

            row.append(rankCell, jobCell, decisionCell, scoreCell, confidenceCell, reasonCell, errorCell);
            rowsBody.appendChild(row);
        }

        const hasRows = rowsToRender.length > 0;
        if (emptyNode instanceof HTMLElement) {
            if (!hasRows && itemsByJobId.size > 0 && filteredRows.length === 0) {
                emptyNode.textContent = "No rows match the selected filter.";
            } else if (!hasRows) {
                emptyNode.textContent = "No streamed results yet.";
            }

            emptyNode.classList.toggle("d-none", hasRows);
        }

        if (tableShell instanceof HTMLElement) {
            tableShell.classList.toggle("d-none", !hasRows);
        }

        updatePagination(filteredRows.length, rowsToRender.length);
        ensureLoadMoreObserver();
    };

    const readErrorMessage = async (response) => {
        const contentType = response.headers.get("Content-Type") || "";
        if (!contentType.includes("application/json") && !contentType.includes("application/problem+json")) {
            return `Request failed with HTTP ${response.status}.`;
        }

        try {
            const payload = await response.json();
            return payload?.detail || payload?.message || payload?.title || `Request failed with HTTP ${response.status}.`;
        } catch {
            return `Request failed with HTTP ${response.status}.`;
        }
    };

    const buildRunUrl = (template, runId) => {
        if (!template || !runId) {
            return "";
        }

        return template.replace("__RUN_ID__", encodeURIComponent(String(runId)));
    };

    const appendQueryParameter = (url, key, value) => {
        if (!url) {
            return "";
        }

        const separator = url.includes("?") ? "&" : "?";
        return `${url}${separator}${encodeURIComponent(key)}=${encodeURIComponent(String(value))}`;
    };

    const buildJobDetailsLink = (jobId) => {
        if (!jobDetailsUrl || !jobId) {
            return "#";
        }

        const url = new URL(jobDetailsUrl, window.location.origin);
        url.searchParams.set("jobId", String(jobId));
        return `${url.pathname}${url.search}`;
    };

    const upsertItem = (item) => {
        const key = item?.jobId
            ? String(item.jobId)
            : (item?.linkedInJobId ? `li:${item.linkedInJobId}` : null);
        if (!key) {
            return;
        }

        const previous = itemsByJobId.get(key) || {};
        itemsByJobId.set(key, {
            ...previous,
            ...item,
            decision: normalizeDecision(item?.decision ?? previous.decision),
            fitLevel: normalizeFitLevel(
                getFitLevelFromSignals(
                    item?.score ?? previous.score,
                    item?.decision ?? previous.decision))
        });
    };

    const resetProgressStreamState = () => {
        lastSequence = 0;
        seenProgressEvents.clear();
        visibleCount = tablePageSize;
        clearReportLog();
    };

    const applyRunSnapshot = (run) => {
        const snapshotRunId = run?.runId ? String(run.runId) : null;
        const runChanged = String(snapshotRunId || "") !== String(activeRunId || "");
        if (runChanged) {
            resetProgressStreamState();
        }

        activeRunId = snapshotRunId;
        activeRunState = run?.status || "Idle";
        runCancellationRequestedAtUtc = run?.cancellationRequestedAtUtc || null;
        updateActionButtons();
        syncReportHeartbeat();

        updateRunSummary({
            state: activeRunState,
            candidateCount: run?.candidateCount ?? 0,
            processedCount: run?.processedCount ?? 0,
            acceptedCount: run?.shortlistedCount ?? 0,
            needsReviewCount: run?.needsReviewCount ?? 0,
            failedCount: run?.failedCount ?? 0
        });

        itemsByJobId = new Map();
        const items = Array.isArray(run?.items) ? run.items : [];
        for (const item of items) {
            upsertItem({
                ...item,
                decision: item.decision,
                rank: item.rank
            });
        }

        renderRows();
        setStatus(run?.summary || `Run ${activeRunState} loaded.`);

        if (activeRunId) {
            if (activeRunState === "Running") {
                startProgressPolling(activeRunId);
            } else {
                stopProgressPolling();
                void pollProgressOnce(activeRunId);
            }
        }
    };

    const applyProgressUpdate = (update, occurredAtUtc, eventSequence) => {
        if (!update || !update.runId) {
            return;
        }

        if (!activeRunId) {
            activeRunId = String(update.runId);
        }

        if (String(update.runId) !== String(activeRunId)) {
            return;
        }

        if (eventSequence && Number.isFinite(Number(eventSequence))) {
            const sequenceNumber = Number(eventSequence);
            const eventKey = `${activeRunId}:${sequenceNumber}`;
            if (seenProgressEvents.has(eventKey)) {
                return;
            }

            seenProgressEvents.add(eventKey);
            lastSequence = Math.max(lastSequence, sequenceNumber);
        }

        if (update.state) {
            activeRunState = toDisplayState(update.state);
        }

        if (update.stage === "cancel-requested") {
            runCancellationRequestedAtUtc = occurredAtUtc || new Date().toISOString();
        }

        if (update.stage === "run-started") {
            runCancellationRequestedAtUtc = null;
        }

        updateRunSummary({
            state: activeRunState,
            candidateCount: update.candidateCount ?? Number(candidatesNode?.textContent || 0),
            processedCount: update.processedCount ?? Number(processedNode?.textContent || 0),
            acceptedCount: update.acceptedCount ?? Number(acceptedNode?.textContent || 0),
            needsReviewCount: update.needsReviewCount ?? Number(needsReviewNode?.textContent || 0),
            failedCount: update.failedCount ?? Number(failedNode?.textContent || 0)
        });
        updateActionButtons();
        syncReportHeartbeat();
        appendReportLog(update, occurredAtUtc, eventSequence);

        if (update.stage === "candidate-processed") {
            upsertItem({
                jobId: update.jobId,
                linkedInJobId: update.linkedInJobId,
                jobTitle: update.jobTitle,
                companyName: update.companyName,
                locationName: update.locationName,
                decision: update.decision,
                rank: update.processedCount || update.sequenceNumber,
                score: update.score,
                confidence: update.confidence,
                recommendationReason: update.recommendationReason,
                concerns: update.concerns,
                errorCode: update.errorCode,
                createdAtUtc: occurredAtUtc
            });
            renderRows();
            scheduleOverviewRefresh();
        }

        setStatus(update.message || `Run ${activeRunState}.`);

        if (terminalStates.has(activeRunState)) {
            runCancellationRequestedAtUtc = null;
            stopProgressPolling();
            syncReportHeartbeat();
            scheduleOverviewRefresh(0);
        }
    };

    const ensureSignalRConnection = async () => {
        if (!window.signalR) {
            return null;
        }

        if (!connection) {
            connection = new signalR.HubConnectionBuilder()
                .withUrl(progressHubUrl)
                .withAutomaticReconnect([0, 1000, 3000, 5000])
                .build();

            connection.on("GlobalShortlistProgress", (progressEvent) => {
                if (!progressEvent?.update) {
                    return;
                }

                applyProgressUpdate(progressEvent.update, progressEvent.occurredAtUtc, progressEvent.sequence);
            });

            connection.onreconnecting(() => {
                setStatus("SignalR reconnecting, polling fallback remains active...");
            });

            connection.onreconnected(() => {
                setStatus("SignalR reconnected.");
                if (activeRunId) {
                    void pollProgressOnce(activeRunId);
                }
            });

            connection.onclose(() => {
                setStatus("SignalR disconnected. Polling fallback continues.");
            });
        }

        if (connection.state === signalR.HubConnectionState.Disconnected) {
            await connection.start();
        }

        return connection;
    };

    const getConnectionId = () => {
        if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
            return null;
        }

        return connection.connectionId || null;
    };

    const startProgressPolling = (runId) => {
        stopProgressPolling();
        if (!runId) {
            return;
        }

        pollTimer = window.setInterval(() => {
            void pollProgressOnce(runId);
        }, 2500);

        void pollProgressOnce(runId);
    };

    const stopProgressPolling = () => {
        if (pollTimer) {
            window.clearInterval(pollTimer);
            pollTimer = null;
        }
    };

    const pollProgressOnce = async (runId) => {
        const url = buildRunUrl(progressUrlTemplate, runId);
        if (!url) {
            return;
        }

        const pollUrl = appendQueryParameter(url, "afterSequence", lastSequence);

        try {
            const response = await fetch(pollUrl, {
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                credentials: "same-origin"
            });

            if (!response.ok) {
                return;
            }

            const batch = await response.json();
            if (!batch?.runFound || !Array.isArray(batch.events)) {
                return;
            }

            for (const eventItem of batch.events) {
                if (eventItem?.update) {
                    applyProgressUpdate(eventItem.update, eventItem.occurredAtUtc, eventItem.sequence);
                }
            }

            if (Number.isFinite(Number(batch.nextSequence))) {
                lastSequence = Math.max(lastSequence, Number(batch.nextSequence) - 1);
            }
        } catch {
            // best-effort fallback polling; failures are surfaced through status only when user action fails
        }
    };

    const loadRun = async (runId) => {
        const runUrl = buildRunUrl(runUrlTemplate, runId);
        if (!runUrl) {
            return;
        }

        const response = await fetch(runUrl, {
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            },
            credentials: "same-origin"
        });

        if (!response.ok) {
            throw new Error(await readErrorMessage(response));
        }

        const payload = await response.json();
        if (!payload?.success || !payload.run) {
            throw new Error(payload?.message || "Run was not found.");
        }

        updateQueueOverview(payload.overview);
        applyRunSnapshot(payload.run);
    };

    const loadLatest = async () => {
        if (loading || !latestUrl) {
            return;
        }

        loading = true;
        setStatus("Loading latest run...");

        try {
            const response = await fetch(latestUrl, {
                headers: {
                    "X-Requested-With": "XMLHttpRequest"
                },
                credentials: "same-origin"
            });

            if (!response.ok) {
                throw new Error(await readErrorMessage(response));
            }

            const payload = await response.json();
            if (!payload?.success) {
                throw new Error(payload?.message || "Could not load latest run.");
            }

            updateQueueOverview(payload.overview);

            if (!payload.run) {
                activeRunId = null;
                activeRunState = "Idle";
                runCancellationRequestedAtUtc = null;
                itemsByJobId = new Map();
                resetProgressStreamState();
                updateActionButtons();
                syncReportHeartbeat();
                updateRunSummary({
                    state: "Idle",
                    candidateCount: 0,
                    processedCount: 0,
                    acceptedCount: 0,
                    needsReviewCount: 0,
                    failedCount: 0
                });
                renderRows();
                setStatus(payload.message || "No run is available yet.");
                return;
            }

            applyRunSnapshot(payload.run);
        } finally {
            loading = false;
            if (isInitialSyncPending) {
                isInitialSyncPending = false;
                updateActionButtons();
            }
        }
    };

    const postCommand = async (url, button, loadingText, options = {}) => {
        if (!url || !(startForm instanceof HTMLFormElement)) {
            return null;
        }

        const {
            timeoutMs = null,
            treatTimeoutAsAccepted = false,
            timeoutMessage = "Command was submitted. Waiting for live run updates..."
        } = options;

        const headers = {
            "X-Requested-With": "XMLHttpRequest"
        };

        const connectionId = getConnectionId();
        if (connectionId) {
            headers["X-Progress-ConnectionId"] = connectionId;
        }

        const controller = timeoutMs ? new AbortController() : null;
        let timeoutHandle = null;
        if (controller) {
            timeoutHandle = window.setTimeout(() => controller.abort("command-timeout"), timeoutMs);
        }

        setButtonLoading(button, true, loadingText);
        try {
            const response = await fetch(url, {
                method: "POST",
                headers,
                body: new FormData(startForm),
                credentials: "same-origin",
                signal: controller?.signal
            });

            if (!response.ok) {
                throw new Error(await readErrorMessage(response));
            }

            const payload = await response.json();
            if (!payload?.success) {
                throw new Error(payload?.message || "Command failed.");
            }

            return payload;
        } catch (error) {
            const isTimeoutAbort = Boolean(
                controller?.signal?.aborted &&
                controller.signal.reason === "command-timeout");

            if (isTimeoutAbort && treatTimeoutAsAccepted) {
                return {
                    success: true,
                    message: timeoutMessage,
                    timedOut: true
                };
            }

            throw error;
        } finally {
            if (timeoutHandle) {
                window.clearTimeout(timeoutHandle);
            }

            setButtonLoading(button, false);
        }
    };

    startForm?.addEventListener("submit", async (event) => {
        event.preventDefault();

        if (!startUrl) {
            return;
        }

        if (isInitialSyncPending) {
            setStatus("Syncing latest run state...");
            return;
        }

        try {
            await ensureSignalRConnection();

            resetProgressStreamState();
            itemsByJobId = new Map();
            activeRunId = null;
            activeRunState = "Running";
            runCancellationRequestedAtUtc = null;
            updateRunSummary({
                state: "Running",
                candidateCount: 0,
                processedCount: 0,
                acceptedCount: 0,
                needsReviewCount: 0,
                failedCount: 0
            });
            updateActionButtons();
            syncReportHeartbeat();
            renderRows();
            setStatus("Starting live review...");
            appendUiLog("Start command submitted. Waiting for first progress event.", "running", "start-requested");

            const payload = await postCommand(
                startUrl,
                startButton,
                "Starting...",
                {
                    timeoutMs: 2200,
                    treatTimeoutAsAccepted: true,
                    timeoutMessage: "Start request was submitted. Waiting for live updates..."
                });
            if (!payload) {
                return;
            }

            showToast(payload.message || "Run started.", true);
            if (payload.runId) {
                if (!activeRunId) {
                    activeRunId = String(payload.runId);
                }
                activeRunState = "Running";
                updateActionButtons();
                startProgressPolling(activeRunId);
                await loadRun(activeRunId);
            } else {
                await loadLatest();
            }
        } catch (error) {
            const message = error instanceof Error ? error.message : "Failed to start live review.";
            setStatus(message, true);
            appendUiLog(message, "failed", "start-failed");
            showToast(message, false);
        }
    });

    loadLatestButton?.addEventListener("click", async () => {
        try {
            await loadLatest();
        } catch (error) {
            const message = error instanceof Error ? error.message : "Failed to load latest run.";
            setStatus(message, true);
            appendUiLog(message, "failed", "load-latest-failed");
            showToast(message, false);
        }
    });

    resumeButton?.addEventListener("click", async () => {
        if (isInitialSyncPending) {
            setStatus("Syncing latest run state...");
            return;
        }

        if (!activeRunId) {
            return;
        }

        try {
            await ensureSignalRConnection();
            const resumeUrl = buildRunUrl(resumeUrlTemplate, activeRunId);
            const payload = await postCommand(
                resumeUrl,
                resumeButton,
                "Resuming...",
                {
                    timeoutMs: 2200,
                    treatTimeoutAsAccepted: true,
                    timeoutMessage: "Resume request was submitted. Waiting for live updates..."
                });
            if (!payload) {
                return;
            }

            appendUiLog(payload.message || "Run resumed.", "running", "resume-requested");
            showToast(payload.message || "Run resumed.", true);
            activeRunState = "Running";
            runCancellationRequestedAtUtc = null;
            updateActionButtons();
            startProgressPolling(activeRunId);
            await loadRun(activeRunId);
        } catch (error) {
            const message = error instanceof Error ? error.message : "Failed to resume run.";
            setStatus(message, true);
            appendUiLog(message, "failed", "resume-failed");
            showToast(message, false);
        }
    });

    stopButton?.addEventListener("click", async () => {
        if (isInitialSyncPending) {
            setStatus("Syncing latest run state...");
            return;
        }

        if (!activeRunId) {
            return;
        }

        try {
            const cancelUrl = buildRunUrl(cancelUrlTemplate, activeRunId);
            runCancellationRequestedAtUtc = new Date().toISOString();
            updateActionButtons();
            const payload = await postCommand(cancelUrl, stopButton, "Stopping...");
            if (!payload) {
                return;
            }

            appendUiLog(payload.message || "Cancellation requested.", "warning", "cancel-requested");
            showToast(payload.message || "Cancellation requested.", true);
            setStatus(payload.message || "Cancellation requested.");
            await loadRun(activeRunId);
        } catch (error) {
            runCancellationRequestedAtUtc = null;
            updateActionButtons();
            const message = error instanceof Error ? error.message : "Failed to stop run.";
            setStatus(message, true);
            appendUiLog(message, "failed", "cancel-failed");
            showToast(message, false);
        }
    });

    reportLogToggleButton?.addEventListener("click", () => {
        setReportLogCollapsed(!isReportLogCollapsed);
    });

    loadMoreButton?.addEventListener("click", () => {
        const filteredRows = buildFilteredRows();
        if (visibleCount >= filteredRows.length) {
            return;
        }

        visibleCount = Math.min(filteredRows.length, visibleCount + tablePageSize);
        renderRows();
    });

    filterSelect?.addEventListener("change", () => {
        filterValue = String(filterSelect.value || "high");
        visibleCount = tablePageSize;
        renderRows();
    });

    if (filterSelect instanceof HTMLSelectElement) {
        filterSelect.value = filterValue;
    }

    syncReportLogCollapseUi();
    updateActionButtons();

    void ensureSignalRConnection()
        .catch(() => null)
        .then(() => loadLatest())
        .catch((error) => {
            const message = error instanceof Error ? error.message : "Failed to initialize live review page.";
            setStatus(message, true);
            appendUiLog(message, "failed", "init-failed");
        });
})();
