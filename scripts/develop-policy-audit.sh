#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage:
  scripts/develop-policy-audit.sh \
    --base-sha <before> \
    --head-sha <after> \
    [--main-ref <ref>] \
    [--skip-versioning]

Purpose:
- Server-side audit for develop-branch integration policy.
- Detects drift: direct commits, reverse merges, unsquashed merge side.
USAGE
}

BASE_SHA=""
HEAD_SHA=""
MAIN_REF=""
SKIP_VERSIONING="0"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --base-sha)
      BASE_SHA="${2:-}"
      shift 2
      ;;
    --head-sha)
      HEAD_SHA="${2:-}"
      shift 2
      ;;
    --main-ref)
      MAIN_REF="${2:-}"
      shift 2
      ;;
    --skip-versioning)
      SKIP_VERSIONING="1"
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

if [[ -z "$BASE_SHA" || -z "$HEAD_SHA" ]]; then
  echo "--base-sha and --head-sha are required." >&2
  usage >&2
  exit 1
fi

resolve_main_ref() {
  local preferred="$1"
  if [[ -n "$preferred" ]] && git rev-parse --verify --quiet "$preferred" >/dev/null; then
    echo "$preferred"
    return
  fi

  if git rev-parse --verify --quiet "main" >/dev/null; then
    echo "main"
    return
  fi

  if git rev-parse --verify --quiet "origin/main" >/dev/null; then
    echo "origin/main"
    return
  fi

  echo ""
}

MAIN_REF="$(resolve_main_ref "$MAIN_REF")"
if [[ -z "$MAIN_REF" ]]; then
  echo "Unable to resolve a valid main reference (main/origin/main)." >&2
  exit 1
fi

ZERO_SHA='0000000000000000000000000000000000000000'
if [[ "$BASE_SHA" == "$ZERO_SHA" ]]; then
  BASE_SHA="$(git rev-parse "$HEAD_SHA^" 2>/dev/null || true)"
  if [[ -z "$BASE_SHA" ]]; then
    echo "No comparable base for initial push; skipping audit."
    exit 0
  fi
fi

if ! git rev-parse --verify --quiet "$BASE_SHA^{commit}" >/dev/null; then
  echo "Base SHA not found: $BASE_SHA" >&2
  exit 1
fi

if ! git rev-parse --verify --quiet "$HEAD_SHA^{commit}" >/dev/null; then
  echo "Head SHA not found: $HEAD_SHA" >&2
  exit 1
fi

RANGE="$BASE_SHA..$HEAD_SHA"
if [[ -z "$(git rev-list "$RANGE")" ]]; then
  echo "No commits in range $RANGE; skipping audit."
  exit 0
fi

NON_MERGE_FIRST_PARENT="$(git rev-list --first-parent --no-merges "$RANGE")"
if [[ -n "$NON_MERGE_FIRST_PARENT" ]]; then
  echo "Direct non-merge commits detected on develop first-parent history:" >&2
  git log --oneline --decorate --first-parent --no-merges "$RANGE" >&2
  exit 1
fi

while read -r merge_commit; do
  [[ -z "$merge_commit" ]] && continue

  parent_line="$(git show -s --format='%P' "$merge_commit")"
  parent_one="$(awk '{print $1}' <<<"$parent_line")"
  parent_two="$(awk '{print $2}' <<<"$parent_line")"

  if [[ -z "$parent_two" ]]; then
    continue
  fi

  if git merge-base --is-ancestor "$parent_two" "$MAIN_REF"; then
    echo "Reverse merge detected: merge '$merge_commit' pulls '$MAIN_REF' into develop." >&2
    echo "Policy requires hotfix sync via cherry-pick only." >&2
    exit 1
  fi

  feature_side_commit_count="$(git rev-list --count "$parent_one..$parent_two")"
  if [[ "$feature_side_commit_count" -ne 1 ]]; then
    echo "Merge '$merge_commit' violates squash-before-merge policy; work side commits=$feature_side_commit_count." >&2
    exit 1
  fi

done < <(git rev-list --first-parent --merges "$RANGE")

if [[ "$SKIP_VERSIONING" != "1" ]]; then
  if [[ -f scripts/versioning-guard.sh ]]; then
    git fetch --tags --force >/dev/null 2>&1 || true
    bash scripts/versioning-guard.sh validate
  else
    echo "versioning-guard script not found; skipping versioning validation." >&2
  fi
fi

echo "Develop policy audit passed for range $RANGE"
