# CLAUDE.md

Rules for Claude Code sessions in this repository. It is public: never put secrets in it.

## Project

WPF app on .NET 10, x64, shipped as MSIX. See `README.md`, `docs/` and `CONTRIBUTING.md`.

Cloud sessions run on Linux and can't build, test or package it (needs Windows). `build.yml` builds and tests on
pushes to `main` and pull requests; `sideload.yml` makes a signed package. Say when a change wasn't tested locally.

## Git

- Work on `claude/<short-task-name>` from the latest `main`. Never push to `main`.
- No pull requests unless asked; the owner merges.
- Commit first line under 72 characters; details in the body.

## Version bump

Change all of: `Directory.Build.props` (version and comment), `Package.appxmanifest` (`x.y.z.0`), `VersionTests.cs`,
`DataPortabilityTests.cs`, the top `CHANGELOG.md` heading, and the version-in-progress rows in `docs/database.md`.

## Sideload builds

**Actions > sideload > Run workflow** on any branch creates a draft release with the `.msixbundle` and `.cer`. Remind
the owner to delete old drafts. Keep `docs/building.md` in sync with the workflow.

## Secrets

Signing uses the repository secrets `SIDELOAD_PFX_BASE64` and `SIDELOAD_PFX_PASSWORD`. Never ask for or handle them.
