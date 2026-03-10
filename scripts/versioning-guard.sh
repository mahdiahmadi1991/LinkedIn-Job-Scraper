#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
cd "$repo_root"

readonly VERSION_FILE="VERSION"
readonly CHANGELOG_FILE="CHANGELOG.md"
readonly VERSION_PATTERN='^v\.[0-9]+\.[0-9]+\.[0-9]+$'

fail() {
  echo "[versioning] $1" >&2
  exit 1
}

is_valid_version() {
  local value="$1"
  [[ "$value" =~ $VERSION_PATTERN ]]
}

read_version_file() {
  [[ -f "$VERSION_FILE" ]] || fail "Missing '$VERSION_FILE'."
  tr -d '\r\n' < "$VERSION_FILE"
}

parse_version_parts() {
  local value="$1"
  local numeric="${value#v.}"
  IFS='.' read -r major minor patch <<< "$numeric"
  echo "$major" "$minor" "$patch"
}

is_version_greater() {
  local old_version="$1"
  local new_version="$2"

  read -r old_major old_minor old_patch <<< "$(parse_version_parts "$old_version")"
  read -r new_major new_minor new_patch <<< "$(parse_version_parts "$new_version")"

  if (( new_major > old_major )); then
    return 0
  fi

  if (( new_major < old_major )); then
    return 1
  fi

  if (( new_minor > old_minor )); then
    return 0
  fi

  if (( new_minor < old_minor )); then
    return 1
  fi

  if (( new_patch > old_patch )); then
    return 0
  fi

  return 1
}

validate_changelog_entry() {
  local version="$1"
  [[ -f "$CHANGELOG_FILE" ]] || fail "Missing '$CHANGELOG_FILE'."
  grep -Eq "^## \[$version\] - [0-9]{4}-[0-9]{2}-[0-9]{2}$" "$CHANGELOG_FILE" \
    || fail "'$CHANGELOG_FILE' must include a heading: ## [$version] - YYYY-MM-DD"
}

usage() {
  cat <<'USAGE'
Usage:
  scripts/versioning-guard.sh current
  scripts/versioning-guard.sh validate
  scripts/versioning-guard.sh compare <oldVersion> <newVersion>
USAGE
}

command="${1:-}"
case "$command" in
  current)
    version="$(read_version_file)"
    is_valid_version "$version" || fail "Invalid version format '$version'. Expected v.MAJOR.MINOR.PATCH."
    echo "$version"
    ;;
  validate)
    version="$(read_version_file)"
    is_valid_version "$version" || fail "Invalid version format '$version'. Expected v.MAJOR.MINOR.PATCH."
    validate_changelog_entry "$version"
    echo "[versioning] OK ($version)"
    ;;
  compare)
    old_version="${2:-}"
    new_version="${3:-}"
    [[ -n "$old_version" && -n "$new_version" ]] || fail "compare requires <oldVersion> and <newVersion>."
    is_valid_version "$old_version" || fail "Invalid old version '$old_version'."
    is_valid_version "$new_version" || fail "Invalid new version '$new_version'."
    is_version_greater "$old_version" "$new_version" \
      || fail "Version must increase. old=$old_version new=$new_version"
    echo "[versioning] compare OK ($old_version -> $new_version)"
    ;;
  *)
    usage
    exit 1
    ;;
esac
