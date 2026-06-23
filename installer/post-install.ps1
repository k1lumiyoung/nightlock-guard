param(
    [Parameter(Mandatory = $true)][string]$InstallDir,
    [Parameter(Mandatory = $true)][string]$DataDir,
    [Parameter(Mandatory = $true)][string]$PasswordFile,
    [Parameter(Mandatory = $true)][string]$Start,
    [Parameter(Mandatory = $true)][string]$End,
    [int]$Minutes = 30
)

# Configures NightLock Guard after the installer has copied the binaries into $InstallDir.
# Mirrors the tail of scripts/install.ps1 (no build/publish here). Run elevated by the installer.
$ErrorActionPreference = "Stop"
$cli = Join-Path $InstallDir "NightLock.Cli.exe"

New-Item -ItemType Directory -Force -Path $DataDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DataDir "logs") | Out-Null

# Parent password: read from the temp file the installer wrote, hand to the CLI (which hashes it),
# then shred the temp file so the plaintext never lingers.
try {
    $plain = Get-Content -Raw -Path $PasswordFile
    $plain = $plain.TrimEnd("`r", "`n")
    $plain | & $cli set-password --password-stdin
    if ($LASTEXITCODE -ne 0) { throw "Failed to set the parent password." }
}
finally {
    Remove-Item $PasswordFile -Force -ErrorAction SilentlyContinue
    $plain = $null
}

& $cli set-schedule --start $Start --end $End | Out-Null
& $cli set-override --minutes $Minutes | Out-Null

# Harden the data directory (well-known SIDs so it works on any Windows display language).
$prevEap = $ErrorActionPreference
$ErrorActionPreference = "Continue"
icacls $DataDir /inheritance:r 2>&1 | Out-Null
icacls $DataDir /grant:r "*S-1-5-18:(OI)(CI)F" "*S-1-5-32-544:(OI)(CI)F" "*S-1-5-32-545:(OI)(CI)RX" 2>&1 | Out-Null
icacls (Join-Path $DataDir "logs") /grant:r "*S-1-5-32-545:(OI)(CI)M" 2>&1 | Out-Null
$ErrorActionPreference = $prevEap

# Background service. Make a re-install over a running copy safe: stop everything that could
# hold the old service, then wait for the SCM to actually remove it before recreating.
Get-Process NightLock.Helper, NightLock.Admin, NightLock.Service -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue

$serviceName = "NightLockGuard"
$serviceExe = Join-Path $InstallDir "NightLock.Service.exe"
if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $serviceName | Out-Null
    # A delete can stay "pending" while handles close; wait until the service is really gone.
    for ($i = 0; $i -lt 30; $i++) {
        if (-not (Get-Service -Name $serviceName -ErrorAction SilentlyContinue)) { break }
        Start-Sleep -Milliseconds 500
    }
}
sc.exe create $serviceName binPath= "`"$serviceExe`"" start= auto DisplayName= "NightLock Guard" | Out-Null
sc.exe description $serviceName "Applies the NightLock Guard night lock policy and supervises the session helper." | Out-Null
sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
Start-Sleep -Milliseconds 500
# Don't fail the whole install if the service is briefly busy; the failure-actions retry it, and
# the lock helper is started by the logon task below regardless.
Start-Service -Name $serviceName -ErrorAction SilentlyContinue

# Logon task that runs the helper inside each interactive user's session.
$taskName = "NightLockGuardHelper"
$helperExe = Join-Path $InstallDir "NightLock.Helper.exe"
$action = New-ScheduledTaskAction -Execute $helperExe
$trigger = New-ScheduledTaskTrigger -AtLogOn
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

Write-Host "NightLock Guard configured."
