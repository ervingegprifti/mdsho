# CLAUDE.md

Instructions for Claude Code sessions working in this repository. The owner (Ervin Gegprifti) edits this file; follow
it in every session. This repository is **public**: never write secrets, passwords or keys here or anywhere in it.

## Project

MDSHO (Multi Desktop Shortcut Organizer) is a WPF app on .NET 10, x64, shipped only as MSIX (Microsoft Store and
sideload). Read these instead of guessing:

- `README.md`: layout of the solution and projects.
- `docs/architecture.md`, `docs/database.md`: design, data, export/import, database upgrades.
- `docs/building.md`: build, test, signed sideload MSIX, install.
- `CONTRIBUTING.md`: tests first, style (`.editorconfig`), contributor grant.

Build settings: SDK pinned exactly in `global.json`, central package versions in `Directory.Packages.props`, shared
settings in `Directory.Build.props`. Warnings are errors.

## Where things can and can't run

Cloud sessions run on Linux. WPF, the MSIX package (`.wapproj`) and the tests (they open windows) need Windows, so a
cloud session can't build, test or package MDSHO. Instead:

- `.github/workflows/build.yml` builds and tests on every push to `main` and every pull request.
- `.github/workflows/sideload.yml` builds a signed sideload package on request (see below).

Say plainly when a change could not be built or tested locally, and let these workflows be the test.

## Git and GitHub

- **Branches:** work on a branch named after the task, `claude/<short-task-name>` (for example
  `claude/note-window-search`), created from the latest `main`. Never push to `main`.
- **No pull requests** unless the owner asks. The owner merges branches themselves, usually through a pull request
  they open on GitHub, and deletes the branch afterwards.
- **Commit messages:** first line under 72 characters (aim for about 50), so GitHub doesn't cut pull request titles;
  details in the body, wrapped at about 72 characters.
- Push when work is done, so nothing is lost when the cloud session ends.

## Version bumps

The version must match in all of these places (see the bump commits in `git log` for examples):

1. `Directory.Build.props`: `<Version>x.y.z</Version>` and the `x.y.z.0` in the comment above it.
2. `src/MDSHO.Package/Package.appxmanifest`: `Version="x.y.z.0"` in `Identity` (the 4th part is reserved by the Store).
3. `tests/MDSHO.Tests/VersionTests.cs`: `Expected` and the summary comment.
4. `tests/MDSHO.Tests/DataPortabilityTests.cs`: the expected `AppVersion`.
5. `CHANGELOG.md`: the top section is the version in progress; a bump renames its heading (`## [x.y.z]`).
6. `docs/database.md`: the version column of the upgrade history rows that belong to the version in progress.

The `sideload` workflow refuses to build when 1 and 2 differ, or when that version's release is already published.

## Sideload builds (draft releases)

`.github/workflows/sideload.yml` runs only when started by hand: **Actions > sideload > Run workflow**, choosing a
branch. The workflow file from that branch is the one that runs, so a branch can be tested before it is merged. It:

1. checks the version and that `v<version>` isn't already published;
2. builds and runs the tests;
3. signs with the certificate from the repository secrets and checks its subject equals the manifest `Publisher`;
4. creates a **draft release** "MDSHO x.y.z" with only two files, `MDSHO.Package_x.y.z.0_x64.msixbundle` and
   `MDSHO.Package_x.y.z.0_x64.cer`, and the version's `CHANGELOG.md` section as notes.

Draft releases are visible only to people with write access. The tag `v<version>` is created only when the owner
publishes a draft. Each run adds a draft, so remind the owner to delete old ones. Keep `docs/building.md` in sync with
any change to the workflow.

Decisions already made (don't reopen them without being asked):

- Builds go to draft GitHub releases; no Google Drive upload, no zip, no workflow artifacts (artifacts on a public
  repository are downloadable by any signed-in GitHub user).
- MSBuild is found with `vswhere` on the runner, not with `microsoft/setup-msbuild`.
- Public releases (later) may need more work: a trusted signing certificate or the Store, and possibly auto-updates.

## Secrets

- Repository secrets: `SIDELOAD_PFX_BASE64` (the signing `.pfx`, base64) and `SIDELOAD_PFX_PASSWORD`. The owner manages
  them in **Settings > Secrets and variables > Actions**.
- Never ask for, read, copy or handle the `.pfx`, its password or any other secret. Workflows use secrets only by name.
- Only the owner has write access. If collaborators are ever added, suggest moving the secrets into a GitHub
  Environment (`sideload`) with the owner as required reviewer.
