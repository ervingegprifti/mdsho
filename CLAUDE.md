# CLAUDE.md

Rules for Claude Code sessions in this repository. It is public: never put secrets in it.

Keep this file current: when a change alters how work is done here (workflows, build or version rules, conventions the
owner sets), update this file in the same branch. Keep it short and describe the current state, not history.

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
The repository's 4th version part stays `0` (reserved by the Microsoft Store); never bump it in commits.

## Sideload builds

Sessions on the owner's Windows machine (desktop app) build sideloads locally, as the steps in `docs/building.md`
describe and only when the owner asks, not with the workflow, and don't zip the package. A local build keeps `0` as
the 4th part, so before each installed local build raise the patch version above every build already installed,
workflow builds included (after `1.5.1.N` from the workflow, the next local build is `1.5.2`).

Cloud or browser sessions, which can't build, use the workflow: **Actions > sideload > Run workflow** on any branch creates a draft release "MDSHO x.y.z (build N)" with the
`.msixbundle` and `.cer`. The workflow stamps its run number N as the 4th version part on the build machine only, so
every build installs over the previous one. Remind the owner to delete old drafts, and to export data before installing
a build that adds a database version. Keep `docs/building.md` in sync with the workflow.

## Secrets

Signing uses the repository secrets `SIDELOAD_PFX_BASE64` and `SIDELOAD_PFX_PASSWORD`. Never ask for or handle them.
