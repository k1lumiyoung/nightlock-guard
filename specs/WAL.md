# WAL

## Active Sessions

### INFRA-001: Windows runtime baseline (@codex)
- Started: 2026-06-18
- Spec: `spec://modules/core/INFRA-001-windows-runtime-baseline#root`
- Status: In Progress
- Summary: Service reduced to schedule-logging + helper supervision (session-change aware); runtime IPC pipe removed; installer now bootstraps the .NET SDK, publishes self-contained, hardens ProgramData ACLs, and registers the helper task for interactive Users. Needs Windows/.NET SDK build + run verification.

### FEAT-001: Night lock window (@codex)
- Started: 2026-06-18
- Spec: `spec://modules/core/FEAT-001-night-lock-window#root`
- Status: In Progress
- Summary: 23:20 warning + 23:30-08:00 schedule; lock mechanism reworked to a helper-owned full-screen lock window (multi-monitor, topmost, non-closable) instead of LockWorkStation. Midnight-crossing schedule guard added + tested. Needs Windows run verification.

### FEAT-002: Parent password override (@codex)
- Started: 2026-06-18
- Spec: `spec://modules/core/FEAT-002-parent-password-override#root`
- Status: In Progress
- Summary: PBKDF2 verifier; password entered in the lock window; override now in-memory in the helper for a configurable duration (1-240 min, default 30) set via installer/CLI. Override file + named pipe removed. Needs Windows run verification.

### FEAT-003: Parent admin panel (@claude)
- Started: 2026-06-19
- Spec: `spec://modules/core/FEAT-003-parent-admin-panel#root`
- Status: In Progress
- Summary: New `NightLock.Admin` WinForms app (elevated via app.manifest), password-gated by `AdminAuthForm`, single screen (`AdminSettingsForm`) editing schedule/override/Win-key/hotkey/password, writing the shared `ConfigStore`. Launched from the helper tray ("Settings…"); no Start-menu shortcut (installer publishes it without one). Added to the solution and `install.ps1`. Needs Windows build/run verification.

### FEAT-004: Emergency stop hotkey (@claude)
- Started: 2026-06-19
- Spec: `spec://modules/core/FEAT-004-emergency-stop-hotkey#root`
- Status: In Progress
- Summary: `Hotkey` core model (VK codes, validation, parse/describe/round-trip, default LShift+RShift+6+7) + `StopHotkeyKeys` config field. Helper `KeyboardHook` (WH_KEYBOARD_LL) detects the combo and posts a stop that hides the lock, removes the hook, and parks the helper until restart. CLI `set-hotkey`; editable in the admin panel. Core tests added. Needs Windows build/run verification.

### FEAT-005: Windows key suppression (@claude)
- Started: 2026-06-19
- Spec: `spec://modules/core/FEAT-005-windows-key-suppression#root`
- Status: In Progress
- Summary: Same `KeyboardHook` swallows LWin/RWin while `SuppressWindowsKey` is true; the helper enables it only in the Restricted phase (no override) when the `SuppressWindowsKey` setting is on, and clears it otherwise / on stop / on exit. CLI `set-winkey --on|--off`; toggle in the admin panel. Needs Windows build/run verification.

### FEAT-006: Trusted time source (@codex)
- Started: 2026-06-25
- Spec: `spec://modules/core/FEAT-006-trusted-time-source#root`
- Status: Done (deployed + verified live 2026-06-26)
- Summary: Hardening after the night lock was bypassed by setting the system clock forward and leaving it. Policy now derives `now` from a trusted clock (`anchorUtc` from SNTP + monotonic `Environment.TickCount64` offset) instead of `DateTimeOffset.Now`, so changing the system clock no longer moves the window. Added Core `SntpClient` + `TrustedClock` (anchor + monotonic model) + `IMonotonicClock`/`SystemMonotonicClock`, config fields (`useTrustedTime`, `ntpServers`, `ntpResyncMinutes`, `ntpTimeoutSeconds`), service + helper integration (sync at startup + every `ntpResyncMinutes` off the existing timer), tamper-evidence drift logging, and Core unit tests with injected monotonic/NTP (incl. a test proving a year-2099 system clock is ignored). Offline-cold-start fallback + time-zone vector recorded as TD-008/TD-009. Implemented by Codex, reviewed by Claude.
- Verified 2026-06-25 on the user's Windows 11 PC over SSH: `dotnet build NightLockGuard.sln -c Release` = 0 warnings / 0 errors (all 6 projects); `dotnet run --project tests\NightLock.Core.Tests` = passed (incl. new trusted-time tests). Build + unit-test verification DONE.
- Deployed + verified live 2026-06-26 on the user's Windows 11 PC over SSH: stopped the service + helper, `dotnet publish` (self-contained win-x64) of all 4 apps over `C:\Program Files\NightLockGuard` (config.json in ProgramData preserved), restarted the service. Confirmed under real attack — with the system clock set ~13h wrong (reading 10:34 daytime), the new build logged `Trusted time differs from system clock by 46806 seconds; continuing with trusted policy time` and held `Policy state: Restricted` + `Lock shown (restricted window)`. The clock-change bypass no longer works.
- Known follow-up: helper SNTP sync runs on the WinForms UI thread and can block up to ~9s at startup when fully offline (online path unaffected); TD-008/TD-009 (offline cold-start fallback + time-zone vector) remain open.

## Completed

## Cross-module

- 2026-06-18: First spec set created for `core`: product boundaries, Windows runtime baseline, night lock behavior, and parent password override.
- 2026-06-19: Verified on the user's Windows 11 PC over SSH — `dotnet build NightLockGuard.sln -c Release` = 0 warnings / 0 errors for all 6 projects (incl. new NightLock.Admin); `dotnet run --project tests\NightLock.Core.Tests` = passed (incl. new hotkey + settings-defaults tests). Build + unit-test verification is DONE. Still pending and only checkable at the physical screen: the lock window, Win-key suppression, the stop combo, and the elevated admin panel actually saving config.
- 2026-06-19: Repo prepared for portfolio publish — YouTube Shorts blocker moved out of the repo to a sibling folder (`../youtube-shorts-blocker`). Authored FEAT-003 (hidden parent admin panel), FEAT-004 (configurable emergency stop hotkey, default LeftShift+RightShift+6+7), FEAT-005 (Win-key suppression in restricted hours). Amended PROP-001 to allow a narrow non-logging keyboard hook; extended INFRA-001 with #input-suppression-hook, #admin-app, and new config fields. Added TD-005/TD-006 and Roadmap Wave 4. Specs only — no implementation yet.

## Decisions Pending

- Build verification can only run on Windows (this dev host is macOS with no .NET SDK). On Windows run `dotnet build .\NightLockGuard.sln -c Release` and `dotnet run --project .\tests\NightLock.Core.Tests`, then `.\scripts\install.ps1` from an elevated prompt and confirm the lock window appears at 23:30 and the parent password grants the configured override.
- Product decisions confirmed with user (2026-06-18): full-screen lock window; configurable override minutes; install on Windows with no separate dev tooling.
