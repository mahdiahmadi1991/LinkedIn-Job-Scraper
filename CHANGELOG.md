# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [v.3.14.29] - 2026-03-10
### Changed
- Workflow governance now explicitly records that `develop` has no CI by design and must not be blocked waiting for CI checks.
- Local user-validation defaults are now standardized in agent policy (`dotnet run --launch-profile http`, URLs `5058/7145`, and Firefox restricted-port caution).

## [v.3.14.28] - 2026-03-10
### Fixed
- Version source resolution now supports source-layout execution (`src/LinkedIn.JobScraper.Web`), so login/layout do not fall back to `v.0.0.0` when repository root `VERSION` is valid.
- Login page version badge was reduced to a minimal bottom-corner label; shared layout version display was simplified to low-emphasis footer text.
- Hamburger menu now includes a compact version label at the end for quick in-app reference.

### Changed
- Documented recommended local run command with launch profile and recorded launch/port lessons learned in troubleshooting guidance.
- Workflow guardrails now require explicit user instruction for `main` merge and mandatory lessons-learned capture after meaningful failures.

## [v.3.14.27] - 2026-03-10
### Added
- Mandatory project versioning with `VERSION` source-of-truth (`v.MAJOR.MINOR.PATCH`).
- Standardized `CHANGELOG.md` release tracking policy.
- UI version visibility on login and shared layout footer.
- Local workflow enforcement for version/changelog updates on `develop` integrations.
- Main pipeline version/changelog validation and automatic git tag creation for release versions.
