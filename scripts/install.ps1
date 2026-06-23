param(
    [string]$InstallDir = "C:\Program Files\NightLockGuard",
    [string]$DataDir = "C:\ProgramData\NightLockGuard",
    [string]$Runtime = "win-x64",
    [int]$OverrideMinutes = 30
)

$ErrorActionPreference = "Stop"

function Require-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Run this script from an elevated PowerShell window (Run as administrator)."
    }
}

function Ensure-Dotnet {
    if (Get-Command dotnet -ErrorAction SilentlyContinue) {
        return
    }

    Write-Host "The .NET SDK was not found. Trying to install it with winget..."
    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw "winget is not available. Install the .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0 and re-run this script."
    }

    winget install --id Microsoft.DotNet.SDK.8 --source winget --accept-package-agreements --accept-source-agreements --silent
    # winget does not refresh PATH for the current session; add the default install location.
    $defaultDotnet = "C:\Program Files\dotnet"
    if (Test-Path (Join-Path $defaultDotnet "dotnet.exe")) {
        $env:Path = "$defaultDotnet;$env:Path"
    }

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "The .NET SDK was installed but 'dotnet' is still not on PATH. Open a new elevated PowerShell window and re-run this script."
    }
}

Require-Admin
Ensure-Dotnet

$securePassword = Read-Host "Create a NightLock parent password" -AsSecureString

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
# Publish self-contained so the target machine does not need a separate .NET runtime.
$publishArgs = @("-c", "Release", "-r", $Runtime, "--self-contained", "true", "-o", $InstallDir)

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
New-Item -ItemType Directory -Force -Path $DataDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DataDir "logs") | Out-Null

dotnet publish (Join-Path $root "src\NightLock.Service\NightLock.Service.csproj") @publishArgs
dotnet publish (Join-Path $root "src\NightLock.Helper\NightLock.Helper.csproj") @publishArgs
dotnet publish (Join-Path $root "src\NightLock.Cli\NightLock.Cli.csproj") @publishArgs
# Hidden parent admin panel. Published into the install dir but deliberately given NO Start-menu
# shortcut so it does not show up in ordinary Windows search; the helper tray launches it.
dotnet publish (Join-Path $root "src\NightLock.Admin\NightLock.Admin.csproj") @publishArgs

$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
$plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
try {
    $plainPassword | & (Join-Path $InstallDir "NightLock.Cli.exe") set-password --password-stdin
    if ($LASTEXITCODE -ne 0) {
        throw "NightLock.Cli failed to configure the parent password."
    }
}
finally {
    if ($bstr -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
    $plainPassword = $null
}

& (Join-Path $InstallDir "NightLock.Cli.exe") set-override --minutes $OverrideMinutes | Out-Null

# Harden data directory: SYSTEM/Admins own it, normal users may only read config/verifier.
# Use well-known SIDs (not names) so this works on any Windows display language:
#   *S-1-5-18      = SYSTEM
#   *S-1-5-32-544  = Administrators
#   *S-1-5-32-545  = Users
# ACL hardening is best-effort: a failure here must not stop the service/task setup.
$prevEap = $ErrorActionPreference
$ErrorActionPreference = "Continue"
try {
    icacls $DataDir /inheritance:r 2>&1 | Out-Null
    icacls $DataDir /grant:r "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-544:(OI)(CI)F" "*S-1-5-32-545:(OI)(CI)RX" 2>&1 | Out-Null
    # The helper runs as the logged-on user and must append to logs, so allow Modify there only.
    icacls (Join-Path $DataDir "logs") /grant:r "*S-1-5-32-545:(OI)(CI)M" 2>&1 | Out-Null
}
catch {
    Write-Warning "Could not fully harden data-directory permissions: $($_.Exception.Message)"
}
$ErrorActionPreference = $prevEap

$serviceName = "NightLockGuard"
$serviceExe = Join-Path $InstallDir "NightLock.Service.exe"
if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 2
}

sc.exe create $serviceName binPath= "`"$serviceExe`"" start= auto DisplayName= "NightLock Guard" | Out-Null
sc.exe description $serviceName "Applies the NightLock Guard night lock policy and supervises the session helper." | Out-Null
sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
Start-Service -Name $serviceName

# Logon task that runs the helper inside each interactive user's session (not the admin context).
$taskName = "NightLockGuardHelper"
$helperExe = Join-Path $InstallDir "NightLock.Helper.exe"
$action = New-ScheduledTaskAction -Execute $helperExe
$trigger = New-ScheduledTaskTrigger -AtLogOn
# Resolve the localized "Users" group name from its SID so this works on any language.
$usersGroup = ([System.Security.Principal.SecurityIdentifier]"S-1-5-32-545").Translate([System.Security.Principal.NTAccount]).Value
$principal = New-ScheduledTaskPrincipal -GroupId $usersGroup -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -MultipleInstances IgnoreNew
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description "Starts NightLock Guard helper at user logon." -Force | Out-Null
Start-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue

# Run in Safe Mode too: register the service AND the Task Scheduler (which launches the helper at
# logon) under SafeBoot, so the night lock cannot be bypassed by booting into Safe Mode.
foreach ($mode in 'Minimal', 'Network') {
    reg add "HKLM\SYSTEM\CurrentControlSet\Control\SafeBoot\$mode\$serviceName" /ve /t REG_SZ /d "Service" /f | Out-Null
    reg add "HKLM\SYSTEM\CurrentControlSet\Control\SafeBoot\$mode\Schedule" /ve /t REG_SZ /d "Service" /f | Out-Null
}

Write-Host ""
Write-Host "NightLock Guard installed."
Write-Host "Install dir: $InstallDir"
Write-Host "Data dir:    $DataDir"
Write-Host "Lock window: 23:00 - 08:00 (warning at 22:50)"
Write-Host "Override:    $OverrideMinutes minutes per correct parent password"
Write-Host "Win key:     blocked during lock hours"
Write-Host "Stop hotkey: Left Shift + Right Shift + 6 + 7 (change it in the settings panel)"
Write-Host "Settings:    open from the NightLock tray icon -> Settings (hidden from Windows search)"
