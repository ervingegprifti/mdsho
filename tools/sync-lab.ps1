<#
.SYNOPSIS
  Sync lab: runs several copies of a Debug MDSHO on this PC that sync with each other through a lab folder.

.DESCRIPTION
  Builds the app in Debug once, makes the lab sync folder %LOCALAPPDATA%\MDSHO-lab\sync and starts one copy per profile
  (default PC-A and PC-B) with sync on to that folder and a short sync interval. Each copy has its own data folder
  (%LOCALAPPDATA%\MDSHO-lab\<profile>), mutex, device name and title, so the real MDSHO data is never touched.
  See docs/building.md, "Test sync on one PC".

.EXAMPLE
  .\tools\sync-lab.ps1                      # build, then start PC-A and PC-B, syncing every 3 seconds
.EXAMPLE
  .\tools\sync-lab.ps1 -Count 3 -Every 5    # PC-A, PC-B and PC-C, every 5 seconds
.EXAMPLE
  .\tools\sync-lab.ps1 -Stop                # close every lab copy
.EXAMPLE
  .\tools\sync-lab.ps1 -Reset               # close every lab copy and delete all lab data and the lab sync folder
#>
[CmdletBinding()]
param(
  [ValidateRange(1, 26)] [int] $Count = 2,
  [string[]] $Profiles,
  [ValidateRange(1, 3600)] [int] $Every = 3,
  [switch] $NoBuild,
  [switch] $Stop,
  [switch] $Reset
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$labRoot = Join-Path $env:LOCALAPPDATA 'MDSHO-lab'
$syncFolder = Join-Path $labRoot 'sync'
$exe = Join-Path $repo 'src\MDSHO.App\bin\Debug\net10.0-windows10.0.19041.0\MDSHO.exe'

# Only copies started with --profile are lab copies; the normal MDSHO (installed or Debug) is never closed.
function Get-LabProcesses {
  Get-CimInstance Win32_Process -Filter "Name = 'MDSHO.exe'" |
    Where-Object { $_.CommandLine -match '--profile\s' }
}

function Stop-Lab {
  $labs = @(Get-LabProcesses)
  foreach ($p in $labs) {
    Write-Host "Ending lab copy $($p.ProcessId): $($p.CommandLine)"
    # MDSHO has no main window to close (it lives in the tray), so the copy is ended: there is no exit sync, but with
    # the short lab interval its changes have already synced. Use the tray's Exit to test the exit sync.
    Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
  }
  if ($labs.Count -eq 0) { Write-Host 'No lab copies are running.' }
}

if ($Stop -or $Reset) {
  Stop-Lab
  if ($Reset) {
    if (Test-Path $labRoot) {
      Remove-Item -LiteralPath $labRoot -Recurse -Force
      Write-Host "Deleted $labRoot"
    }
  }
  return
}

if (-not $Profiles) {
  $Profiles = 0..($Count - 1) | ForEach-Object { 'PC-' + [char](65 + $_) }
}
foreach ($name in $Profiles) {
  if ($name -notmatch '^[A-Za-z0-9-]{1,32}$') { throw "A profile name has only letters, digits and dashes: $name" }
}

if (-not $NoBuild) {
  Write-Host 'Building MDSHO (Debug)...'
  & dotnet build (Join-Path $repo 'src\MDSHO.App\MDSHO.App.csproj') -c Debug -nologo -v quiet
  if ($LASTEXITCODE -ne 0) { throw 'The Debug build failed.' }
}
if (-not (Test-Path $exe)) { throw "Not found: $exe (build it first, or leave out -NoBuild)" }

New-Item -ItemType Directory -Force -Path $syncFolder | Out-Null
$running = @(Get-LabProcesses)
foreach ($name in $Profiles) {
  if ($running | Where-Object { $_.CommandLine -match "--profile\s+$name(\s|$)" }) {
    Write-Host "$name is already running."
    continue
  }
  Write-Host "Starting $name (data: $(Join-Path $labRoot $name))"
  Start-Process -FilePath $exe -ArgumentList @('--profile', $name, '--sync-every', $Every, '--sync-folder', "`"$syncFolder`"")
}
Write-Host "Lab sync folder: $syncFolder"
Write-Host 'Stop them with: .\tools\sync-lab.ps1 -Stop    Start over with: .\tools\sync-lab.ps1 -Reset'
