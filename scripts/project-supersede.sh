#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage:
  scripts/project-supersede.sh \
    --superseded-issue <old-number> \
    --replacement-issue <new-number> \
    [--repo owner/name] \
    [--no-local-cleanup]

Purpose:
- Close a superseded issue in a policy-compliant way.
- Mark superseded issue as dropped and cross-link both issues.
- Optionally remove obsolete local operational doc referenced by IdeaDocPath.
USAGE
}

SUPERSEDED_ISSUE=""
REPLACEMENT_ISSUE=""
REPO=""
LOCAL_CLEANUP="1"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --superseded-issue)
      SUPERSEDED_ISSUE="${2:-}"
      shift 2
      ;;
    --replacement-issue)
      REPLACEMENT_ISSUE="${2:-}"
      shift 2
      ;;
    --repo)
      REPO="${2:-}"
      shift 2
      ;;
    --no-local-cleanup)
      LOCAL_CLEANUP="0"
      shift
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

if [[ ! "$SUPERSEDED_ISSUE" =~ ^[0-9]+$ ]] || [[ ! "$REPLACEMENT_ISSUE" =~ ^[0-9]+$ ]]; then
  echo "--superseded-issue and --replacement-issue must be positive integers." >&2
  exit 1
fi

if [[ "$SUPERSEDED_ISSUE" -le 0 || "$REPLACEMENT_ISSUE" -le 0 ]]; then
  echo "Issue numbers must be greater than zero." >&2
  exit 1
fi

if [[ "$SUPERSEDED_ISSUE" == "$REPLACEMENT_ISSUE" ]]; then
  echo "Superseded and replacement issues must be different." >&2
  exit 1
fi

if [[ -z "$REPO" ]]; then
  REPO="$(gh repo view --json nameWithOwner --jq .nameWithOwner)"
fi

validate_issue_exists() {
  local issue_number="$1"
  local issue_type
  issue_type="$(gh issue view "$issue_number" --repo "$REPO" --json number --jq '.number' 2>/dev/null || true)"
  if [[ -z "$issue_type" ]]; then
    echo "Issue #$issue_number not found in $REPO." >&2
    exit 1
  fi
}

extract_idea_doc_path() {
  local issue_number="$1"
  local body
  body="$(gh issue view "$issue_number" --repo "$REPO" --json body --jq '.body')"
  awk '
    BEGIN { in_section=0 }
    /^### IdeaDocPath[[:space:]]*$/ { in_section=1; next }
    /^###[[:space:]]/ { if (in_section) exit }
    {
      if (in_section) {
        line=$0
        gsub(/^[ \t]+|[ \t]+$/, "", line)
        if (line != "") {
          print line
          exit
        }
      }
    }
  ' <<<"$body"
}

cleanup_local_doc() {
  local issue_number="$1"
  local doc_path
  doc_path="$(extract_idea_doc_path "$issue_number")"

  if [[ -z "$doc_path" ]]; then
    echo "No IdeaDocPath found on issue #$issue_number; skipping local cleanup."
    return
  fi

  case "$doc_path" in
    docs/ideas/*.md|docs/archive/ideas/*.md|docs/tmp/*.md) ;;
    *)
      echo "IdeaDocPath '$doc_path' is outside managed operational paths; skipping delete."
      return
      ;;
  esac

  if [[ -f "$doc_path" ]]; then
    rm -f "$doc_path"
    echo "Deleted obsolete local operational doc: $doc_path"
  else
    echo "IdeaDocPath '$doc_path' not found locally; nothing to delete."
  fi
}

validate_issue_exists "$SUPERSEDED_ISSUE"
validate_issue_exists "$REPLACEMENT_ISSUE"

# Keep only dropped state among execution-state labels on superseded issue.
for label in inbox approved in-progress user-test-gate conformance-gate integration-sync-gate ready-for-develop-merge done; do
  gh issue edit "$SUPERSEDED_ISSUE" --repo "$REPO" --remove-label "$label" >/dev/null 2>&1 || true
done

gh issue edit "$SUPERSEDED_ISSUE" --repo "$REPO" --add-label dropped >/dev/null

SUPERSEDED_COMMENT_FILE="$(mktemp)"
cat > "$SUPERSEDED_COMMENT_FILE" <<EOF_SUPERSEDED
Superseded by #$REPLACEMENT_ISSUE.

Cleanup contract applied:
- Marked this issue as \`dropped\`
- Closed this superseded issue
- Linked replacement issue for canonical tracking
EOF_SUPERSEDED

gh issue comment "$SUPERSEDED_ISSUE" --repo "$REPO" --body-file "$SUPERSEDED_COMMENT_FILE" >/dev/null
rm -f "$SUPERSEDED_COMMENT_FILE"

gh issue close "$SUPERSEDED_ISSUE" --repo "$REPO" --reason "not planned" >/dev/null

REPLACEMENT_COMMENT_FILE="$(mktemp)"
cat > "$REPLACEMENT_COMMENT_FILE" <<EOF_REPLACEMENT
Replaces superseded issue #$SUPERSEDED_ISSUE.
EOF_REPLACEMENT

gh issue comment "$REPLACEMENT_ISSUE" --repo "$REPO" --body-file "$REPLACEMENT_COMMENT_FILE" >/dev/null
rm -f "$REPLACEMENT_COMMENT_FILE"

if [[ "$LOCAL_CLEANUP" == "1" ]]; then
  cleanup_local_doc "$SUPERSEDED_ISSUE"
fi

echo "Supersede cleanup completed: #$SUPERSEDED_ISSUE -> #$REPLACEMENT_ISSUE"
