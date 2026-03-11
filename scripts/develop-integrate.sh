#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage:
  scripts/develop-integrate.sh \
    --work-branch <feature|fix|bugfix>/<issue-number>-<slug> \
    [--merge-message "Merge <work-branch> into develop"] \
    [--delete-work-branch]

Purpose:
- Standardize develop integration with policy checks.
- Enforces single-commit work branch before merge.
- Merges into develop with --no-ff.

Notes:
- Run only after explicit user approval for develop merge.
- This script does not push and does not bump VERSION/CHANGELOG automatically.
USAGE
}

WORK_BRANCH=""
MERGE_MESSAGE=""
DELETE_WORK_BRANCH="0"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --work-branch)
      WORK_BRANCH="${2:-}"
      shift 2
      ;;
    --merge-message)
      MERGE_MESSAGE="${2:-}"
      shift 2
      ;;
    --delete-work-branch)
      DELETE_WORK_BRANCH="1"
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

if [[ -z "$WORK_BRANCH" ]]; then
  echo "--work-branch is required." >&2
  usage >&2
  exit 1
fi

if [[ ! "$WORK_BRANCH" =~ ^(feature|fix|bugfix)/[0-9]+-[a-z0-9][a-z0-9-]*$ ]]; then
  echo "Invalid work branch name: '$WORK_BRANCH'." >&2
  echo "Expected: feature|fix|bugfix/<issue-number>-<slug>" >&2
  exit 1
fi

if [[ -n "$(git status --porcelain)" ]]; then
  echo "Working tree is not clean. Commit/stash changes before integration." >&2
  exit 1
fi

if ! git show-ref --verify --quiet "refs/heads/$WORK_BRANCH"; then
  echo "Local branch not found: $WORK_BRANCH" >&2
  exit 1
fi

if ! git show-ref --verify --quiet "refs/heads/develop"; then
  echo "Local branch 'develop' not found." >&2
  exit 1
fi

AHEAD_COUNT="$(git rev-list --count "develop..$WORK_BRANCH")"
if [[ "$AHEAD_COUNT" -eq 0 ]]; then
  echo "Branch '$WORK_BRANCH' has no commits ahead of develop." >&2
  exit 1
fi

if [[ "$AHEAD_COUNT" -ne 1 ]]; then
  echo "Branch '$WORK_BRANCH' must be squashed to exactly one commit before integration." >&2
  echo "Current commit count ahead of develop: $AHEAD_COUNT" >&2
  exit 1
fi

WORK_COMMIT_SHA="$(git rev-list --max-count=1 "develop..$WORK_BRANCH")"
WORK_COMMIT_SUBJECT="$(git show -s --format='%s' "$WORK_COMMIT_SHA")"
CONVENTIONAL_REGEX='^[a-z]+(\([^)]+\))?(!)?: .+'
if [[ ! "$WORK_COMMIT_SUBJECT" =~ $CONVENTIONAL_REGEX ]]; then
  echo "Work commit must follow Conventional Commits: type(scope)!: summary" >&2
  echo "Found: $WORK_COMMIT_SUBJECT" >&2
  exit 1
fi

if [[ -z "$MERGE_MESSAGE" ]]; then
  MERGE_MESSAGE="Merge $WORK_BRANCH into develop"
fi

CURRENT_BRANCH="$(git rev-parse --abbrev-ref HEAD)"
if [[ "$CURRENT_BRANCH" != "develop" ]]; then
  git switch develop
fi

git merge --no-ff "$WORK_BRANCH" -m "$MERGE_MESSAGE"

if [[ "$DELETE_WORK_BRANCH" == "1" ]]; then
  git branch -d "$WORK_BRANCH"
fi

echo "Develop integration merge commit created successfully."
echo "Next: apply VERSION/CHANGELOG/tag per policy, then push when approved."
