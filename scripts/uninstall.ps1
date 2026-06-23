param(
    [string]$InstallDir = "C:\Program Files\NightLockGuard",
    [string]$DataDir = "C:\ProgramData\NightLockGuard",
    [switch]$RemoveData
)

$ErrorActionPreference = "Stop"

function Require-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Run this script from an elevated PowerShell window."
    }
}

Require-Admin

$serviceName = "NightLockGuard"
if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $serviceName | Out-Null
}

if (Get-ScheduledTask -TaskName "NightLockGuardHelper" -ErrorAction SilentlyContinue) {
    Stop-ScheduledTask -TaskName "NightLockGuardHelper" -ErrorAction SilentlyContinue
    Unregister-ScheduledTask -TaskName "NightLockGuardHelper" -Confirm:$false
}

# Remove the Safe Mode registration (leave the Task Scheduler's, which is harmless to keep).
foreach ($mode in "Minimal", "Network") {
    reg delete "HKLM\SYSTEM\CurrentControlSet\Control\SafeBoot\$mode\NightLockGuard" /f 2>$null | Out-Null
}

Get-Process -Name "NightLock.Helper" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "NightLock.Service" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "NightLock.Admin" -ErrorAction SilentlyContinue | Stop-Process -Force

if (Test-Path $InstallDir) {
    Remove-Item $InstallDir -Recurse -Force
}

if ($RemoveData -and (Test-Path $DataDir)) {
    Remove-Item $DataDir -Recurse -Force
}

Write-Host "NightLock Guard uninstalled."
