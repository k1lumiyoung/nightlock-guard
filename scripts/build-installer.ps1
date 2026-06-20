param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

# Builds the single-file NightLockGuard-Setup.exe:
#   1. publishes the apps (self-contained) into installer\payload,
#   2. installs Inno Setup via winget if it is missing,
#   3. compiles installer\NightLockGuard.iss into dist\NightLockGuard-Setup.exe.
# Run on a Windows machine. The .NET 8 SDK must be available (winget install Microsoft.DotNet.SDK.8).
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$payload = Join-Path $root "installer\payload"
$dist = Join-Path $root "dist"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET 8 SDK was not found. Install it first: winget install Microsoft.DotNet.SDK.8"
}

if (Test-Path $payload) { Remove-Item $payload -Recurse -Force }
New-Item -ItemType Directory -Force -Path $payload | Out-Null
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$publishArgs = @("-c", $Configuration, "-r", $Runtime, "--self-contained", "true", "-o", $payload)
foreach ($proj in @("NightLock.Service", "NightLock.Helper", "NightLock.Cli", "NightLock.Admin")) {
    Write-Host "Publishing $proj..."
    dotnet publish (Join-Path $root "src\$proj\$proj.csproj") @publishArgs
}

$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    Write-Host "Inno Setup not found. Installing via winget..."
    winget install --id JRSoftware.InnoSetup --accept-package-agreements --accept-source-agreements --silent
}
if (-not (Test-Path $iscc)) {
    $found = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($found) { $iscc = $found.Source }
}
if (-not (Test-Path $iscc)) {
    throw "Inno Setup (ISCC.exe) not found. Install it from https://jrsoftware.org/isdl.php and re-run."
}

& $iscc (Join-Path $root "installer\NightLockGuard.iss")
if ($LASTEXITCODE -ne 0) { throw "ISCC failed with exit code $LASTEXITCODE." }

Write-Host ""
Write-Host "Installer built: $(Join-Path $dist 'NightLockGuard-Setup.exe')"
