'use strict';

module.exports = async ({ github, context, core }) => {
  const projectOwner = process.env.PROJECT_OWNER;
  const projectNumber = Number.parseInt(process.env.PROJECT_NUMBER ?? "", 10);
  const eventName = context.eventName;
  const eventAction = context.payload?.action ?? "";
  const mode =
    eventName === "workflow_dispatch"
      ? normalizeSpace(context.payload?.inputs?.mode).toLowerCase() || "event"
      : "event";
  const repository = `${context.repo.owner}/${context.repo.repo}`;
  
  if (!projectOwner) {
    throw new Error("PROJECT_OWNER is required.");
  }
  
  if (!Number.isInteger(projectNumber) || projectNumber <= 0) {
    throw new Error("PROJECT_NUMBER must be a positive integer.");
  }
  
  function log(message) {
    core.info(`[project-task-ops] ${message}`);
  }
  
  function normalizeSpace(value) {
    return (value ?? "").replace(/\r/g, "").trim();
  }
  
  function escapeRegex(value) {
    return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
  }
  
  function extractSection(body, label) {
    const normalizedBody = normalizeSpace(body);
    if (!normalizedBody) {
      return null;
    }
  
    const pattern = new RegExp(
      `###\\s*${escapeRegex(label)}\\s*\\n+([\\s\\S]*?)(?=\\n###\\s|$)`,
      "i"
    );
    const match = normalizedBody.match(pattern);
    if (!match) {
      return null;
    }
  
    const block = normalizeSpace(match[1]);
    if (!block) {
      return null;
    }
  
    const firstMeaningfulLine = block
      .split("\n")
      .map((line) => normalizeSpace(line))
      .find((line) => line.length > 0);
  
    return firstMeaningfulLine ?? null;
  }
  
  function canonicalIssueFromRest(issue) {
    return {
      id: issue?.node_id ?? null,
      number: issue?.number ?? null,
      title: issue?.title ?? "",
      state: normalizeSpace(issue?.state).toUpperCase(),
      body: issue?.body ?? "",
      labels: issue?.labels ?? []
    };
  }
  
  function createFieldMaps(fields) {
    const byName = new Map();
    const optionByFieldAndName = new Map();
  
    for (const field of fields) {
      if (!field?.name || !field?.id) {
        continue;
      }
  
      byName.set(field.name.toLowerCase(), field);
      if (!Array.isArray(field.options)) {
        continue;
      }
  
      const optionsMap = new Map();
      for (const option of field.options) {
        if (!option?.name || !option?.id) {
          continue;
        }
  
        optionsMap.set(option.name.toLowerCase(), option.id);
      }
  
      optionByFieldAndName.set(field.name.toLowerCase(), optionsMap);
    }
  
    return { byName, optionByFieldAndName };
  }
  
  async function getProjectContext() {
    const data = await github.graphql(
      `
        query($owner: String!, $number: Int!) {
          user(login: $owner) {
            projectV2(number: $number) {
              id
              title
              fields(first: 100) {
                nodes {
                  __typename
                  ... on ProjectV2Field {
                    id
                    name
                  }
                  ... on ProjectV2SingleSelectField {
                    id
                    name
                    options {
                      id
                      name
                    }
                  }
                  ... on ProjectV2IterationField {
                    id
                    name
                  }
                }
              }
            }
          }
        }
      `,
      { owner: projectOwner, number: projectNumber }
    );
  
    const project = data?.user?.projectV2;
    if (!project?.id) {
      throw new Error(
        `Project ${projectOwner}/${projectNumber} was not found or is not accessible.`
      );
    }
  
    const fields = project.fields?.nodes ?? [];
    return {
      id: project.id,
      title: project.title,
      fields,
      ...createFieldMaps(fields)
    };
  }
  
  async function getIssueByNumber(number) {
    try {
      const response = await github.rest.issues.get({
        owner: context.repo.owner,
        repo: context.repo.repo,
        issue_number: number
      });
  
      if (response.data?.pull_request) {
        return null;
      }
  
      return canonicalIssueFromRest(response.data);
    } catch (error) {
      core.warning(
        `[project-task-ops] Unable to fetch issue #${number} from ${repository}: ${error.message}`
      );
      return null;
    }
  }
  
  async function listRepositoryIssues() {
    const rows = await github.paginate(github.rest.issues.listForRepo, {
      owner: context.repo.owner,
      repo: context.repo.repo,
      state: "all",
      per_page: 100,
      sort: "created",
      direction: "asc"
    });
  
    return rows
      .filter((row) => !row.pull_request)
      .map((row) => canonicalIssueFromRest(row));
  }
  
  async function getProjectItemIdForIssue(projectId, issueNodeId) {
    const data = await github.graphql(
      `
        query($issueId: ID!) {
          node(id: $issueId) {
            ... on Issue {
              projectItems(first: 100) {
                nodes {
                  id
                  project {
                    id
                  }
                }
              }
            }
          }
        }
      `,
      { issueId: issueNodeId }
    );
  
    const items = data?.node?.projectItems?.nodes ?? [];
    const existing = items.find((item) => item?.project?.id === projectId);
    return existing?.id ?? null;
  }
  
  async function ensureProjectItem(projectId, issueNodeId) {
    const existingItemId = await getProjectItemIdForIssue(projectId, issueNodeId);
    if (existingItemId) {
      return existingItemId;
    }
  
    try {
      const data = await github.graphql(
        `
          mutation($projectId: ID!, $contentId: ID!) {
            addProjectV2ItemById(input: { projectId: $projectId, contentId: $contentId }) {
              item {
                id
              }
            }
          }
        `,
        { projectId, contentId: issueNodeId }
      );
  
      return data?.addProjectV2ItemById?.item?.id ?? null;
    } catch (error) {
      log(
        `addProjectV2ItemById failed, retrying by lookup (likely already exists): ${error.message}`
      );
      return getProjectItemIdForIssue(projectId, issueNodeId);
    }
  }
  
  async function setTextField(projectContext, projectItemId, fieldName, textValue) {
    const normalizedValue = normalizeSpace(textValue);
    if (!normalizedValue) {
      return;
    }
  
    const field = projectContext.byName.get(fieldName.toLowerCase());
    if (!field?.id) {
      log(`Field '${fieldName}' was not found; skipping.`);
      return;
    }
  
    await github.graphql(
      `
        mutation(
          $projectId: ID!
          $itemId: ID!
          $fieldId: ID!
          $text: String!
        ) {
          updateProjectV2ItemFieldValue(
            input: {
              projectId: $projectId
              itemId: $itemId
              fieldId: $fieldId
              value: { text: $text }
            }
          ) {
            projectV2Item {
              id
            }
          }
        }
      `,
      {
        projectId: projectContext.id,
        itemId: projectItemId,
        fieldId: field.id,
        text: normalizedValue
      }
    );
  }
  
  async function setSingleSelectByName(
    projectContext,
    projectItemId,
    fieldName,
    optionName
  ) {
    const normalizedOption = normalizeSpace(optionName);
    if (!normalizedOption) {
      return;
    }
  
    const field = projectContext.byName.get(fieldName.toLowerCase());
    if (!field?.id) {
      log(`Field '${fieldName}' was not found; skipping.`);
      return;
    }
  
    const fieldOptions = projectContext.optionByFieldAndName.get(
      fieldName.toLowerCase()
    );
    if (!fieldOptions) {
      log(`Field '${fieldName}' has no selectable options; skipping.`);
      return;
    }
  
    const optionId = fieldOptions.get(normalizedOption.toLowerCase());
    if (!optionId) {
      log(
        `Option '${normalizedOption}' does not exist on field '${fieldName}'; skipping.`
      );
      return;
    }
  
    await github.graphql(
      `
        mutation(
          $projectId: ID!
          $itemId: ID!
          $fieldId: ID!
          $optionId: String!
        ) {
          updateProjectV2ItemFieldValue(
            input: {
              projectId: $projectId
              itemId: $itemId
              fieldId: $fieldId
              value: { singleSelectOptionId: $optionId }
            }
          ) {
            projectV2Item {
              id
            }
          }
        }
      `,
      {
        projectId: projectContext.id,
        itemId: projectItemId,
        fieldId: field.id,
        optionId
      }
    );
  }
  
  function getLabelSet(issue) {
    return new Set(
      (issue?.labels ?? [])
        .map((node) => normalizeSpace(typeof node === "string" ? node : node?.name).toLowerCase())
        .filter(Boolean)
    );
  }
  
  const MANAGED_EXECUTION_LABELS = new Set([
    "inbox",
    "approved",
    "in-progress",
    "user-test-gate",
    "conformance-gate",
    "integration-sync-gate",
    "ready-for-develop-merge",
    "done",
    "dropped"
  ]);
  
  const MANAGED_METADATA_PREFIXES = [
    "type/",
    "priority/",
    "area/",
    "risk/",
    "effort/"
  ];
  
  function mapExecutionStateToLabel(executionState) {
    const normalized = normalizeSpace(executionState).toLowerCase();
    const map = {
      "inbox": "inbox",
      "approved": "approved",
      "in progress": "in-progress",
      "user test gate": "user-test-gate",
      "conformance gate": "conformance-gate",
      "integration sync gate": "integration-sync-gate",
      "ready for develop merge": "ready-for-develop-merge",
      "done": "done",
      "dropped": "dropped"
    };
  
    return map[normalized] ?? null;
  }
  
  function mapTypeToLabel(typeValue) {
    const normalized = normalizeSpace(typeValue).toLowerCase();
    const map = {
      "feature": "type/feature",
      "improvement": "type/improvement",
      "bugfix": "type/bugfix",
      "hotfix": "type/hotfix",
      "ops": "type/ops"
    };
    return map[normalized] ?? null;
  }
  
  function mapPriorityToLabel(priorityValue) {
    const normalized = normalizeSpace(priorityValue).toLowerCase();
    return normalized ? `priority/${normalized}` : null;
  }
  
  function mapAreaToLabel(areaValue) {
    const normalized = normalizeSpace(areaValue).toLowerCase();
    const map = {
      "linkedin": "area/linkedin",
      "jobs": "area/jobs",
      "ai": "area/ai",
      "persistence": "area/persistence",
      "security": "area/security",
      "docs": "area/docs",
      "devex": "area/devex"
    };
    return map[normalized] ?? null;
  }
  
  function mapRiskToLabel(riskValue) {
    const normalized = normalizeSpace(riskValue).toLowerCase();
    return normalized ? `risk/${normalized}` : null;
  }
  
  function mapEffortToLabel(effortValue) {
    const normalized = normalizeSpace(effortValue).toLowerCase();
    return normalized ? `effort/${normalized}` : null;
  }
  
  function isManagedLabel(label) {
    const normalized = normalizeSpace(label).toLowerCase();
    if (!normalized) {
      return false;
    }
  
    if (normalized === "intake") {
      return true;
    }
  
    if (MANAGED_EXECUTION_LABELS.has(normalized)) {
      return true;
    }
  
    return MANAGED_METADATA_PREFIXES.some((prefix) =>
      normalized.startsWith(prefix)
    );
  }
  
  function inferTypeFromIssue(issue) {
    const text = `${issue?.title ?? ""}\n${issue?.body ?? ""}`.toLowerCase();
    if (text.includes("hotfix")) {
      return "Hotfix";
    }
  
    if (
      /\bbug\b/.test(text) ||
      /\bfix\b/.test(text) ||
      text.includes("error") ||
      text.includes("regression")
    ) {
      return "Bugfix";
    }
  
    if (text.includes("feature") || text.includes("new capability")) {
      return "Feature";
    }
  
    if (
      text.includes("ops") ||
      text.includes("workflow") ||
      text.includes("governance")
    ) {
      return "Ops";
    }
  
    return "Improvement";
  }
  
  async function syncManagedLabels(issueNumber, requiredManagedLabels) {
    if (!Number.isInteger(issueNumber) || issueNumber <= 0) {
      return;
    }
  
    const labelsResponse = await github.rest.issues.listLabelsOnIssue({
      owner: context.repo.owner,
      repo: context.repo.repo,
      issue_number: issueNumber,
      per_page: 100
    });
  
    const currentLabels = labelsResponse.data
      .map((label) => normalizeSpace(label?.name).toLowerCase())
      .filter(Boolean);
    const currentSet = new Set(currentLabels);
  
    const unmanagedLabels = currentLabels.filter(
      (label) => !isManagedLabel(label)
    );
    const managedSet = new Set(
      (requiredManagedLabels ?? [])
        .map((label) => normalizeSpace(label).toLowerCase())
        .filter(Boolean)
    );
  
    const nextLabels = [...new Set([...unmanagedLabels, ...managedSet])];
    const nextSet = new Set(nextLabels);
    const isSameLabelSet =
      currentSet.size === nextSet.size &&
      [...currentSet].every((label) => nextSet.has(label));
  
    if (isSameLabelSet) {
      return;
    }
  
    await github.rest.issues.setLabels({
      owner: context.repo.owner,
      repo: context.repo.repo,
      issue_number: issueNumber,
      labels: nextLabels
    });
  
    log(`Normalized labels for issue #${issueNumber}.`);
  }
  
  async function syncExecutionStateLabel(issueNumber, executionState) {
    if (!Number.isInteger(issueNumber) || issueNumber <= 0) {
      return;
    }
  
    const stateLabel = mapExecutionStateToLabel(executionState);
    if (!stateLabel) {
      return;
    }
  
    const labelsResponse = await github.rest.issues.listLabelsOnIssue({
      owner: context.repo.owner,
      repo: context.repo.repo,
      issue_number: issueNumber,
      per_page: 100
    });
  
    const currentLabels = labelsResponse.data
      .map((label) => normalizeSpace(label?.name).toLowerCase())
      .filter(Boolean);
    const preservedLabels = currentLabels.filter(
      (label) => !MANAGED_EXECUTION_LABELS.has(label)
    );
    const nextLabels = [...new Set([...preservedLabels, "intake", stateLabel])];
  
    const currentSet = new Set(currentLabels);
    const nextSet = new Set(nextLabels);
    const isSameLabelSet =
      currentSet.size === nextSet.size &&
      [...currentSet].every((label) => nextSet.has(label));
  
    if (isSameLabelSet) {
      return;
    }
  
    await github.rest.issues.setLabels({
      owner: context.repo.owner,
      repo: context.repo.repo,
      issue_number: issueNumber,
      labels: nextLabels
    });
  
    log(
      `Updated execution-state label for issue #${issueNumber} -> ${stateLabel}.`
    );
  }
  
  function selectByBodyOrLabel(issue, bodySectionName, labelMap) {
    const bodyValue = extractSection(issue?.body, bodySectionName);
    if (bodyValue) {
      return bodyValue;
    }
  
    const labels = getLabelSet(issue);
    for (const [labelName, mappedValue] of Object.entries(labelMap)) {
      if (labels.has(labelName)) {
        return mappedValue;
      }
    }
  
    return null;
  }
  
  function resolveExecutionState(issue) {
    const labels = getLabelSet(issue);
  
    if (normalizeSpace(issue?.state).toUpperCase() === "CLOSED") {
      return labels.has("dropped") ? "Dropped" : "Done";
    }
  
    const precedence = [
      ["dropped", "Dropped"],
      ["done", "Done"],
      ["ready-for-develop-merge", "Ready For Develop Merge"],
      ["integration-sync-gate", "Integration Sync Gate"],
      ["conformance-gate", "Conformance Gate"],
      ["user-test-gate", "User Test Gate"],
      ["in-progress", "In Progress"],
      ["approved", "Approved"],
      ["inbox", "Inbox"]
    ];
  
    for (const [labelName, stateValue] of precedence) {
      if (labels.has(labelName)) {
        return stateValue;
      }
    }
  
    const executionIntent = normalizeSpace(
      extractSection(issue?.body, "Execution Intent")
    ).toLowerCase();
    if (executionIntent === "execute-now") {
      return "Approved";
    }
  
    return "Inbox";
  }
  
  function extractIssueNumbersFromText(value) {
    const text = normalizeSpace(value);
    if (!text) {
      return [];
    }
  
    const numbers = new Set();
    const regex = /#(\d+)/g;
    let match = regex.exec(text);
  
    while (match) {
      const number = Number.parseInt(match[1], 10);
      if (Number.isInteger(number) && number > 0) {
        numbers.add(number);
      }
  
      match = regex.exec(text);
    }
  
    return [...numbers];
  }
  
  async function syncIssue(projectContext, issue, sourceLabel) {
    if (!issue?.id) {
      log(`Skipping issue sync; issue payload missing id (${sourceLabel}).`);
      return;
    }
  
    const projectItemId = await ensureProjectItem(projectContext.id, issue.id);
    if (!projectItemId) {
      throw new Error(
        `Unable to locate or create a project item for issue #${issue.number}.`
      );
    }
  
    const typeValue = selectByBodyOrLabel(issue, "Type", {
      "type/feature": "Feature",
      "type/improvement": "Improvement",
      "type/bugfix": "Bugfix",
      "type/hotfix": "Hotfix",
      "type/ops": "Ops"
    });
  
    const priorityValue = selectByBodyOrLabel(issue, "Priority", {
      "priority/p0": "P0",
      "priority/p1": "P1",
      "priority/p2": "P2",
      "priority/p3": "P3"
    });
  
    const areaValue = selectByBodyOrLabel(issue, "Area", {
      "area/linkedin": "LinkedIn",
      "area/jobs": "Jobs",
      "area/ai": "AI",
      "area/persistence": "Persistence",
      "area/security": "Security",
      "area/docs": "Docs",
      "area/devex": "DevEx"
    });
  
    const riskValue = selectByBodyOrLabel(issue, "Risk", {
      "risk/low": "Low",
      "risk/medium": "Medium",
      "risk/high": "High"
    });
  
    const effortValue = selectByBodyOrLabel(issue, "Effort", {
      "effort/xs": "XS",
      "effort/s": "S",
      "effort/m": "M",
      "effort/l": "L",
      "effort/xl": "XL"
    });
  
    const resolvedType = typeValue ?? inferTypeFromIssue(issue);
    const resolvedPriority = priorityValue ?? "P2";
    const resolvedArea = areaValue ?? "DevEx";
    const resolvedRisk = riskValue ?? "Low";
    const resolvedEffort = effortValue ?? "M";
    const ideaDocPath = extractSection(issue.body, "IdeaDocPath");
    const executionState = resolveExecutionState(issue);
    const executionStateLabel = mapExecutionStateToLabel(executionState);
  
    const requiredManagedLabels = [
      "intake",
      executionStateLabel,
      mapTypeToLabel(resolvedType),
      mapPriorityToLabel(resolvedPriority),
      mapAreaToLabel(resolvedArea),
      mapRiskToLabel(resolvedRisk),
      mapEffortToLabel(resolvedEffort)
    ].filter(Boolean);
  
    await syncManagedLabels(issue.number, requiredManagedLabels);
  
    await setTextField(projectContext, projectItemId, "Owner", "Codex");
    await setSingleSelectByName(
      projectContext,
      projectItemId,
      "Execution State",
      executionState
    );
  
    await setSingleSelectByName(projectContext, projectItemId, "Type", resolvedType);
    await setSingleSelectByName(
      projectContext,
      projectItemId,
      "Priority",
      resolvedPriority
    );
    await setSingleSelectByName(projectContext, projectItemId, "Area", resolvedArea);
    await setSingleSelectByName(projectContext, projectItemId, "Risk", resolvedRisk);
    await setSingleSelectByName(
      projectContext,
      projectItemId,
      "Effort",
      resolvedEffort
    );
  
    if (ideaDocPath) {
      await setTextField(projectContext, projectItemId, "IdeaDocPath", ideaDocPath);
    }
  
    log(
      `Synced issue #${issue.number} from ${sourceLabel}. State='${executionState}', Type='${resolvedType}', Priority='${resolvedPriority}'.`
    );
  }
  
  async function syncPullRequest(projectContext, pullRequest) {
    const issueNumbers = new Set();
    const referenceText = `${pullRequest?.title ?? ""}\n${pullRequest?.body ?? ""}`;
    for (const number of extractIssueNumbersFromText(referenceText)) {
      issueNumbers.add(number);
    }
  
    if (issueNumbers.size === 0) {
      log(
        `PR #${pullRequest?.number} has no issue references (e.g. #123). Skipping PR-linked sync.`
      );
      return;
    }
  
    for (const issueNumber of issueNumbers) {
      const issue = await getIssueByNumber(issueNumber);
      if (!issue?.id) {
        log(
          `Referenced issue #${issueNumber} was not found in ${repository}; skipping.`
        );
        continue;
      }
  
      await syncIssue(projectContext, issue, `pull_request#${pullRequest.number}`);
  
      const projectItemId = await ensureProjectItem(projectContext.id, issue.id);
      if (!projectItemId) {
        throw new Error(
          `Unable to locate/create project item for PR-linked issue #${issue.number}.`
        );
      }
  
      await setTextField(projectContext, projectItemId, "PR", pullRequest.html_url);
      await setTextField(
        projectContext,
        projectItemId,
        "WorkBranch",
        pullRequest?.head?.ref
      );
      await setSingleSelectByName(
        projectContext,
        projectItemId,
        "TargetBranch",
        pullRequest?.base?.ref
      );
  
      if (normalizeSpace(eventAction) === "closed" && pullRequest?.merged) {
        await setSingleSelectByName(
          projectContext,
          projectItemId,
          "Execution State",
          "Done"
        );
        await syncExecutionStateLabel(issue.number, "Done");
        log(
          `PR #${pullRequest.number} merged; set Execution State=Done for issue #${issue.number}.`
        );
      } else if (normalizeSpace(issue.state).toUpperCase() === "OPEN") {
        await setSingleSelectByName(
          projectContext,
          projectItemId,
          "Execution State",
          "In Progress"
        );
        await syncExecutionStateLabel(issue.number, "In Progress");
        log(
          `PR #${pullRequest.number} active; set Execution State=In Progress for issue #${issue.number}.`
        );
      }
    }
  }
  
  async function main() {
    const projectContext = await getProjectContext();
    log(
      `Loaded project '${projectContext.title}' (#${projectNumber}) for ${projectOwner}. Mode=${mode}.`
    );
  
    if (mode === "backfill") {
      const issues = await listRepositoryIssues();
      log(`Backfill mode active. Syncing ${issues.length} issues from ${repository}.`);
  
      for (const issue of issues) {
        await syncIssue(projectContext, issue, "backfill");
      }
  
      log("Backfill sync completed.");
      return;
    }
  
    if (eventName === "issues") {
      if (context.payload?.issue?.pull_request) {
        log("Issue event is for a pull-request issue proxy; skipping.");
        return;
      }
  
      await syncIssue(
        projectContext,
        canonicalIssueFromRest(context.payload.issue),
        `issues/${eventAction}`
      );
      return;
    }
  
    if (eventName === "pull_request") {
      await syncPullRequest(projectContext, context.payload.pull_request);
      return;
    }
  
    log(`Event '${eventName}' is not supported by this automation; skipping.`);
  }
  
  await main();
};
