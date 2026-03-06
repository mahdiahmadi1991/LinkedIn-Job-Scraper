#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel 2>/dev/null || pwd)"
cd "$repo_root"

dotnet restore LinkedIn.JobScraper.sln
dotnet format LinkedIn.JobScraper.sln --verify-no-changes --no-restore
dotnet build LinkedIn.JobScraper.sln --configuration Release --no-restore -warnaserror
dotnet test LinkedIn.JobScraper.sln --configuration Release --no-build
