---
title: Building MDSHO
permalink: /building/
---

# Building MDSHO

This page explains how to build and test MDSHO from source, run it from Visual Studio, and make your own signed MSIX
package to install without the Microsoft Store ("sideloading"). All paths are relative to the root of the repository.

## Prerequisites

- Windows 10 or Windows 11, x64.
- **Visual Studio 2026** with these workloads:
  - **.NET desktop development**;
  - **Windows application development** with the **MSIX Packaging Tools** component (needed only for the package).
- The **.NET 10 SDK** version pinned in `global.json`, exactly (`rollForward` is `disable`). The SDK decides which .NET
  and WPF runtime the app ships, and the third-party notices list that runtime's version, so every build uses the same SDK.
- A **Windows SDK** (10.0.19041 or later), installed with the workloads above.

Only the .NET SDK is needed to build and test from the command line. Visual Studio is needed to build the MSIX package.

## Build and test from the command line

From the repository root:

```powershell
dotnet build MDSHO.Cli.slnf -c Release
dotnet test MDSHO.Cli.slnf -c Release
```

`MDSHO.Cli.slnf` contains every project except the MSIX packaging project, which the `dotnet` command line can't build.
Compiler warnings are treated as errors, so a build with warnings fails.

The tests never touch your real MDSHO data: they use temporary databases and open windows off screen.

## Run from Visual Studio

1. Open `MDSHO.slnx` in Visual Studio.
2. Right-click **MDSHO.App** (in the `src` folder) and choose **Set as Startup Project**.
3. Choose **Debug** and **x64** in the toolbar and press **F5**.

A Debug build keeps its data apart from the installed app (`mdsho-debug.sqlite3`, `shortcuts-debug`); see
[Architecture](architecture.md#where-the-data-lives), and it can run next to an installed MDSHO. A Release build run
from Visual Studio uses the real data in `%LOCALAPPDATA%\MDSHO` and the same single-instance lock as the installed app,
so close the installed MDSHO first.

## Build a signed sideload MSIX

MDSHO ships only as MSIX; there is no `setup.exe`. The `.msixbundle` file is the installer, and it includes the .NET
runtime, so the target PC needs nothing else.

### Before you start

- **The package must be signed** with a certificate the target PC trusts. You create a self-signed certificate once
  (step 1).
- **It can't be installed next to the Microsoft Store version.** Both use the same app identity, so the Store version
  must be uninstalled first.
- **Uninstalling MDSHO can delete its data.** An installed package may keep its data in the package's private folder,
  which Windows deletes on uninstall (see [Architecture](architecture.md#where-the-data-lives)). Always use
  **Export data...** in the tray menu first, and keep the `.zip` somewhere safe.
- **Keep the certificate private.** Store the `.pfx` file outside the repository and never commit it.

If you share a changed version with others, follow [TRADEMARKS.md](https://github.com/ervingegprifti/mdsho/blob/main/TRADEMARKS.md):
give it your own name, logo and package identity.

### 1. Create a signing certificate (once)

The certificate's subject must match the `Publisher` of the package manifest exactly. This PowerShell script reads it
from `src/MDSHO.Package/Package.appxmanifest`, so you don't need to type it. Run it from the repository root in a normal
(non-admin) PowerShell, and replace `<private folder>` with a folder outside the repository:

```powershell
[xml]$manifest = Get-Content "src\MDSHO.Package\Package.appxmanifest"
$publisher = $manifest.Package.Identity.Publisher
$cert = New-SelfSignedCertificate -Type Custom `
  -Subject $publisher `
  -FriendlyName "MDSHO sideload signing" `
  -KeyUsage DigitalSignature `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -NotAfter (Get-Date).AddYears(3) `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
Export-Certificate -Cert $cert -FilePath "<private folder>\MDSHO-sideload.cer"
$pw = Read-Host -AsSecureString "Choose a password for the certificate"
Export-PfxCertificate -Cert $cert -FilePath "<private folder>\MDSHO-sideload.pfx" -Password $pw
```

This creates:

- `MDSHO-sideload.cer`: the public certificate. Testers install it so their PC trusts your package. Safe to share.
- `MDSHO-sideload.pfx`: the private key, used to sign. Never share it, and keep a backup.

The certificate also stays in your personal certificate store (**MDSHO sideload signing**) and is valid for 3 years.

### 2. Check the version

The version must be the same in two files:

- `Directory.Build.props`: `<Version>x.y.z</Version>` (three parts);
- `src/MDSHO.Package/Package.appxmanifest`: `Version="x.y.z.0"` in the `Identity` element (four parts, the last one
  `0`).

### 3. Build the package in Visual Studio

1. Open `MDSHO.slnx` and choose **Release** and **x64** in the toolbar.
2. In Solution Explorer, right-click **MDSHO.Package** (in the `src` folder) → **Publish** → **Create App Packages...**
3. **Select distribution method:** choose **Sideloading** and untick **Enable automatic updates**. Click **Next**.
4. **Select signing method:** choose **Yes, select a certificate**, then either **Select From Store...** and pick
   **MDSHO sideload signing**, or **Select From File...** and pick your `.pfx` (with its password). Click **Next**.
5. **Select and configure packages:**
   - leave the output location as it is;
   - set the version to the one from step 2 and untick **Automatically increment**;
   - set **Generate app bundle** to **Always**;
   - tick only **x64**, with **Release (x64)**.
6. Click **Create** and wait. When it finishes, open the output location:
   `src/MDSHO.Package/AppPackages/MDSHO.Package_<version>_Sideload/`. It contains:
   - `MDSHO.Package_<version>_x64.msixbundle`: the installer;
   - `MDSHO.Package_<version>_x64.cer`: the public certificate;
   - `Install.ps1` (called `Add-AppDevPackage.ps1` in some Visual Studio versions): a helper that installs both.

   To give the package to a tester, zip the whole `_Sideload` folder.

**Keep the certificate out of the project file.** Choosing a certificate makes Visual Studio write the signing settings
(`AppxPackageSigningEnabled`, `PackageCertificateThumbprint` or `PackageCertificateKeyFile`) into
`src/MDSHO.Package/MDSHO.Package.wapproj`, which is shared. Move them into
`src/MDSHO.Package/MDSHO.Package.wapproj.user` (same `PropertyGroup`), which stays on your PC: `*.user`, `*.pfx` and
`AppPackages/` are in `.gitignore`.

### Alternative: build from the command line

This gives the same result without the dialogs. It needs the certificate in your personal certificate store (step 1
puts it there) and the MSBuild that comes with Visual Studio; `dotnet build` can't build the package.

1. Find the certificate's thumbprint:
   ```powershell
   Get-ChildItem Cert:\CurrentUser\My | Where-Object FriendlyName -eq "MDSHO sideload signing" | Select-Object Thumbprint
   ```
2. From the repository root, run (replace `<THUMBPRINT>` and `<output folder>`; the MSBuild path depends on your Visual
   Studio edition):
   ```powershell
   & "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
     src\MDSHO.Package\MDSHO.Package.wapproj -restore `
     -p:Configuration=Release -p:Platform=x64 `
     -p:UapAppxPackageBuildMode=SideloadOnly `
     -p:AppxBundle=Always -p:AppxBundlePlatforms=x64 `
     -p:AppxPackageSigningEnabled=true `
     -p:PackageCertificateThumbprint=<THUMBPRINT> `
     -p:AppxPackageDir="<output folder>\"
   ```
3. The package appears in `<output folder>\MDSHO.Package_<version>_Sideload\`.

### Alternative: build on GitHub (draft release)

The `sideload` workflow (`.github/workflows/sideload.yml`) builds the same signed package on a GitHub Windows runner
and attaches it to a **draft release**, so you don't need Visual Studio on your PC. Draft releases are visible only to
people with write access to the repository.

Set-up (once): in the repository's **Settings** → **Secrets and variables** → **Actions**, add two repository secrets:

- `SIDELOAD_PFX_BASE64`: the `.pfx` from step 1, base64-encoded. This PowerShell line copies it to the clipboard:
  ```powershell
  [Convert]::ToBase64String([IO.File]::ReadAllBytes("<private folder>\MDSHO-sideload.pfx")) | Set-Clipboard
  ```
- `SIDELOAD_PFX_PASSWORD`: its password.

To build: **Actions** → **sideload** → **Run workflow**, choose the branch and start it. The workflow:

1. checks that the version in `Directory.Build.props` and `Package.appxmanifest` match (step 2), and stops if a release
   for that version is already published;
2. builds and runs the tests;
3. checks that the certificate's subject equals the manifest's `Publisher` and that it hasn't expired;
4. sets the 4th part of the version to the workflow's run number (on the build machine only; the repository keeps
   `x.y.z.0`, as the Microsoft Store requires), so every build has its own, higher version, for example `1.5.1.7`;
5. builds and signs the bundle;
6. creates a draft release **MDSHO &lt;version&gt; (build &lt;run number&gt;)** with the `.msixbundle` and the public
   `.cer`, and the version's section of `CHANGELOG.md` as notes.

Download both files from the draft on the repository's **Releases** page and install them as below. Each build installs
over the previous one as an upgrade, and shows its build number in the About window and, with **Show version in title**,
in the panel titles (`v1.5.1.7`). The tag (`v<version>`) is created only when you publish the draft. Every run adds a
draft, so delete the drafts you no longer need.

A build that adds a database version upgrades your data, and older builds can't open it afterwards: export your data
before installing such a build if you may want to go back.

## Install a sideload package

The target PC needs Windows 10 version 1809 or later, or Windows 11, on x64.

1. **Export your data** (tray menu → **Export data...**) if MDSHO is already installed.
2. **Uninstall the Microsoft Store version**, if installed: **Settings** → **Apps** → **MDSHO** → **Uninstall**.
3. **Trust the certificate** (once per PC):
   1. Double-click the `.cer` file → **Install Certificate...**
   2. Choose **Local Machine** → **Next**, and accept the admin prompt.
   3. Choose **Place all certificates in the following store** → **Browse...** → **Trusted People** → **OK** →
      **Next** → **Finish**.
4. **Install:** double-click the `.msixbundle` file → **Install**.
5. Start **MDSHO** from the Start menu, and import your data if needed.

Instead of steps 3 and 4 you can right-click `Install.ps1` → **Run with PowerShell** and accept the admin prompt.

On Windows 10 versions before 2004, if Windows says sideloading is blocked, turn on **Settings** → **Update & Security**
→ **For developers** → **Sideload apps**.

### What to check in an installed package

Some features only work for real in an installed package:

- **Start with Windows:** turn it on, sign out and back in, and check that MDSHO starts.
- **Import with restart:** export, then import that file, and check that MDSHO restarts with your data.
- **Single instance:** starting MDSHO a second time does not open a second copy.

### If it installs but doesn't start

On a PC that had other MDSHO versions installed before, Windows can occasionally keep stale package records that stop
the new package from starting. Uninstall MDSHO, restart Windows and install again. If that doesn't help, see Microsoft's
guidance on troubleshooting app package installs.

## Go back to the Store version

1. Export your data.
2. **Settings** → **Apps** → **MDSHO** → **Uninstall**.
3. Install MDSHO from the [Microsoft Store](https://www.microsoft.com/store/productId/9P8DNCD0PPG8), and import your
   data if needed.
