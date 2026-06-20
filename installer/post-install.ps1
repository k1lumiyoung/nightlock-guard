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

# Background service.
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

Write-Host "NightLock Guard configured."
