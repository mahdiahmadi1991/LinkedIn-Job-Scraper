#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage:
  scripts/project-intake.sh \
    --title "..." \
    --summary "..." \
    [--scope "..."] \
    [--acceptance "..."] \
    [--execution-intent capture-only|execute-now] \
    [--type feature|improvement|bugfix|hotfix|ops] \
    [--priority p0|p1|p2|p3] \
    [--area linkedin|jobs|ai|persistence|security|docs|devex] \
    [--risk low|medium|high] \
    [--effort xs|s|m|l|xl] \
    [--state inbox|approved|in-progress|user-test-gate|conformance-gate|integration-sync-gate|ready-for-develop-merge|done|dropped] \
    [--idea-doc-path "docs/...md"] \
    [--out-of-scope "..."] \
    [--assignee @me|<github-login>|none] \
    [--repo owner/name] \
    [--project-owner owner] \
    [--project-number number]

Notes:
- Creates a task issue with standard intake sections and managed labels.
- Assigns issue to the authenticated GitHub user by default (`@me`).
- Adds the created issue to the canonical GitHub Project.
- Project field normalization is completed by workflow `project-task-ops.yml`.
USAGE
}

normalize_multiline() {
  local value="$1"
  # Convert literal \n tokens into real newlines for clean issue markdown output.
  value="${value//\\n/$'\n'}"
  printf '%s' "$value"
}

normalize_type() {
  case "${1,,}" in
    feature) echo "Feature" ;;
    improvement|improve) echo "Improvement" ;;
    bugfix|bug) echo "Bugfix" ;;
    hotfix|hot) echo "Hotfix" ;;
    ops|operation|operations) echo "Ops" ;;
    *) echo "" ;;
  esac
}

type_label() {
  case "$1" in
    Feature) echo "type/feature" ;;
    Improvement) echo "type/improvement" ;;
    Bugfix) echo "type/bugfix" ;;
    Hotfix) echo "type/hotfix" ;;
    Ops) echo "type/ops" ;;
    *) return 1 ;;
  esac
}

normalize_priority() {
  case "${1,,}" in
    p0) echo "P0" ;;
    p1) echo "P1" ;;
    p2) echo "P2" ;;
    p3) echo "P3" ;;
    *) echo "" ;;
  esac
}

normalize_area() {
  case "${1,,}" in
    linkedin) echo "LinkedIn" ;;
    jobs) echo "Jobs" ;;
    ai) echo "AI" ;;
    persistence) echo "Persistence" ;;
    security) echo "Security" ;;
    docs) echo "Docs" ;;
    devex) echo "DevEx" ;;
    *) echo "" ;;
  esac
}

area_label() {
  echo "area/${1,,}"
}

normalize_risk() {
  case "${1,,}" in
    low) echo "Low" ;;
    medium) echo "Medium" ;;
    high) echo "High" ;;
    *) echo "" ;;
  esac
}

normalize_effort() {
  case "${1,,}" in
    xs) echo "XS" ;;
    s) echo "S" ;;
    m) echo "M" ;;
    l) echo "L" ;;
    xl) echo "XL" ;;
    *) echo "" ;;
  esac
}

normalize_state_label() {
  case "${1,,}" in
    inbox) echo "inbox" ;;
    approved) echo "approved" ;;
    in-progress|inprogress) echo "in-progress" ;;
    user-test-gate|usertestgate) echo "user-test-gate" ;;
    conformance-gate|conformancegate) echo "conformance-gate" ;;
    integration-sync-gate|integrationsyncgate) echo "integration-sync-gate" ;;
    ready-for-develop-merge|readyfordevelopmerge) echo "ready-for-develop-merge" ;;
    done) echo "done" ;;
    dropped) echo "dropped" ;;
    *) echo "" ;;
  esac
}

TITLE=""
SUMMARY=""
SCOPE=""
ACCEPTANCE=""
TYPE="Improvement"
PRIORITY="P2"
AREA="DevEx"
RISK="Medium"
EFFORT="M"
STATE_LABEL="inbox"
EXECUTION_INTENT=""
IDEA_DOC_PATH=""
OUT_OF_SCOPE=""
PROJECT_OWNER="mahdiahmadi1991"
PROJECT_NUMBER="1"
ASSIGNEE="@me"
REPO=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --title)
      TITLE="${2:-}"
      shift 2
      ;;
    --summary)
      SUMMARY="${2:-}"
      shift 2
      ;;
    --scope)
      SCOPE="${2:-}"
      shift 2
      ;;
    --acceptance)
      ACCEPTANCE="${2:-}"
      shift 2
      ;;
    --execution-intent)
      case "${2,,}" in
        capture-only|captureonly)
          EXECUTION_INTENT="Capture-Only"
          ;;
        execute-now|executenow)
          EXECUTION_INTENT="Execute-Now"
          ;;
        *)
          echo "Invalid --execution-intent value: ${2:-}" >&2
          exit 1
          ;;
      esac
      shift 2
      ;;
    --type)
      TYPE_RAW="$(normalize_type "${2:-}")"
      if [[ -z "$TYPE_RAW" ]]; then
        echo "Invalid --type value: ${2:-}" >&2
        exit 1
      fi
      TYPE="$TYPE_RAW"
      shift 2
      ;;
    --priority)
      PRIORITY_RAW="$(normalize_priority "${2:-}")"
      if [[ -z "$PRIORITY_RAW" ]]; then
        echo "Invalid --priority value: ${2:-}" >&2
        exit 1
      fi
      PRIORITY="$PRIORITY_RAW"
      shift 2
      ;;
    --area)
      AREA_RAW="$(normalize_area "${2:-}")"
      if [[ -z "$AREA_RAW" ]]; then
        echo "Invalid --area value: ${2:-}" >&2
        exit 1
      fi
      AREA="$AREA_RAW"
      shift 2
      ;;
    --risk)
      RISK_RAW="$(normalize_risk "${2:-}")"
      if [[ -z "$RISK_RAW" ]]; then
        echo "Invalid --risk value: ${2:-}" >&2
        exit 1
      fi
      RISK="$RISK_RAW"
      shift 2
      ;;
    --effort)
      EFFORT_RAW="$(normalize_effort "${2:-}")"
      if [[ -z "$EFFORT_RAW" ]]; then
        echo "Invalid --effort value: ${2:-}" >&2
        exit 1
      fi
      EFFORT="$EFFORT_RAW"
      shift 2
      ;;
    --state)
      STATE_RAW="$(normalize_state_label "${2:-}")"
      if [[ -z "$STATE_RAW" ]]; then
        echo "Invalid --state value: ${2:-}" >&2
        exit 1
      fi
      STATE_LABEL="$STATE_RAW"
      shift 2
      ;;
    --idea-doc-path)
      IDEA_DOC_PATH="${2:-}"
      shift 2
      ;;
    --out-of-scope)
      OUT_OF_SCOPE="${2:-}"
      shift 2
      ;;
    --assignee)
      ASSIGNEE="${2:-}"
      shift 2
      ;;
    --project-owner)
      PROJECT_OWNER="${2:-}"
      shift 2
      ;;
    --project-number)
      PROJECT_NUMBER="${2:-}"
      shift 2
      ;;
    --repo)
      REPO="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

if [[ -z "$TITLE" || -z "$SUMMARY" ]]; then
  echo "--title and --summary are required." >&2
  usage >&2
  exit 1
fi

if [[ -z "$SCOPE" ]]; then
  SCOPE="$SUMMARY"
fi

if [[ -z "$ACCEPTANCE" ]]; then
  ACCEPTANCE="1. Scope is clear and approved.\n2. Implementation/result is validated."
fi

if [[ -z "$OUT_OF_SCOPE" ]]; then
  OUT_OF_SCOPE="None."
fi

if [[ "$ASSIGNEE" != "@me" && "$ASSIGNEE" != "none" && ! "$ASSIGNEE" =~ ^[A-Za-z0-9-]+$ ]]; then
  echo "Invalid --assignee value: $ASSIGNEE" >&2
  echo "Allowed values: @me | <github-login> | none" >&2
  exit 1
fi

if [[ -z "$EXECUTION_INTENT" ]]; then
  if [[ "$STATE_LABEL" == "inbox" ]]; then
    EXECUTION_INTENT="Capture-Only"
  else
    EXECUTION_INTENT="Execute-Now"
  fi
fi

if [[ -z "$REPO" ]]; then
  REPO="$(gh repo view --json nameWithOwner --jq .nameWithOwner)"
fi

TYPE_LABEL="$(type_label "$TYPE")"
PRIORITY_LABEL="priority/${PRIORITY,,}"
AREA_LABEL="$(area_label "$AREA")"
RISK_LABEL="risk/${RISK,,}"
EFFORT_LABEL="effort/${EFFORT,,}"

SUMMARY="$(normalize_multiline "$SUMMARY")"
SCOPE="$(normalize_multiline "$SCOPE")"
ACCEPTANCE="$(normalize_multiline "$ACCEPTANCE")"
OUT_OF_SCOPE="$(normalize_multiline "$OUT_OF_SCOPE")"

BODY_FILE="$(mktemp)"
cat > "$BODY_FILE" <<EOF_BODY
### Summary
$SUMMARY

### Scope
$SCOPE

### Acceptance Criteria
$ACCEPTANCE

### Execution Intent
$EXECUTION_INTENT

### Type
$TYPE

### Priority
$PRIORITY

### Area
$AREA

### Risk
$RISK

### Effort
$EFFORT

### IdeaDocPath
$IDEA_DOC_PATH

### Out Of Scope
$OUT_OF_SCOPE
EOF_BODY

ISSUE_CREATE_CMD=(
  gh issue create
  --repo "$REPO"
  --title "$TITLE"
  --body-file "$BODY_FILE"
  --label intake
  --label "$STATE_LABEL"
  --label "$TYPE_LABEL"
  --label "$PRIORITY_LABEL"
  --label "$AREA_LABEL"
  --label "$RISK_LABEL"
  --label "$EFFORT_LABEL"
)

if [[ "$ASSIGNEE" != "none" ]]; then
  ISSUE_CREATE_CMD+=(--assignee "$ASSIGNEE")
fi

ISSUE_URL="$("${ISSUE_CREATE_CMD[@]}")"

rm -f "$BODY_FILE"

gh project item-add "$PROJECT_NUMBER" --owner "$PROJECT_OWNER" --url "$ISSUE_URL" >/dev/null

echo "$ISSUE_URL"
