#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage:
  scripts/project-work-branch.sh \
    --type feature|fix|bugfix|hotfix \
    --issue <number> \
    --slug "short-description" \
    [--base develop|main]

Behavior:
- Default base is `develop` for feature/fix/bugfix.
- Default base is `main` for hotfix.
- Current branch must match base branch.
- Creates and switches to branch: <type>/<issue-number>-<slug>
USAGE
}

TYPE=""
ISSUE=""
SLUG_RAW=""
BASE=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --type)
      TYPE="${2:-}"
      shift 2
      ;;
    --issue)
      ISSUE="${2:-}"
      shift 2
      ;;
    --slug)
      SLUG_RAW="${2:-}"
      shift 2
      ;;
    --base)
      BASE="${2:-}"
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

case "$TYPE" in
  feature|fix|bugfix|hotfix) ;;
  *)
    echo "--type must be one of: feature|fix|bugfix|hotfix" >&2
    exit 1
    ;;
esac

if [[ ! "$ISSUE" =~ ^[0-9]+$ ]] || [[ "$ISSUE" -le 0 ]]; then
  echo "--issue must be a positive integer." >&2
  exit 1
fi

if [[ -z "$SLUG_RAW" ]]; then
  echo "--slug is required." >&2
  exit 1
fi

SLUG="$(echo "$SLUG_RAW" | tr '[:upper:]' '[:lower:]' | sed -E 's/[^a-z0-9]+/-/g; s/^-+//; s/-+$//; s/-{2,}/-/g')"
if [[ -z "$SLUG" ]]; then
  echo "--slug must contain at least one alphanumeric character after normalization." >&2
  exit 1
fi

if [[ -z "$BASE" ]]; then
  if [[ "$TYPE" == "hotfix" ]]; then
    BASE="main"
  else
    BASE="develop"
  fi
fi

if [[ "$BASE" != "develop" && "$BASE" != "main" ]]; then
  echo "--base must be develop or main." >&2
  exit 1
fi

CURRENT_BRANCH="$(git rev-parse --abbrev-ref HEAD)"
if [[ "$CURRENT_BRANCH" != "$BASE" ]]; then
  echo "Current branch is '$CURRENT_BRANCH'. Switch to '$BASE' first." >&2
  exit 1
fi

BRANCH_NAME="${TYPE}/${ISSUE}-${SLUG}"

if git show-ref --verify --quiet "refs/heads/$BRANCH_NAME"; then
  echo "Branch already exists: $BRANCH_NAME" >&2
  exit 1
fi

git switch -c "$BRANCH_NAME"
echo "$BRANCH_NAME"
