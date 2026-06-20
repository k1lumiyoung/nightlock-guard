# INFRA-001: Windows Runtime Baseline {#root}

## Простыми словами {#plain-language}

Программа должна жить в Windows как нормальный установленный инструмент: запускаться вместе с системой, иметь фоновый сервис, показывать уведомление в сессии пользователя и хранить настройки так, чтобы их нельзя было случайно удалить или изменить простым способом.

## Goal {#goal}

Описать технический контур Windows runtime для реализации ночной блокировки и родительского override.

## Scope {#scope}

Входит:
- Windows-only runtime;
- service + user-session helper architecture;
- install location;
- config and password storage;
- startup behavior;
- minimal resource usage;
- logs and diagnostics;
- best-effort tamper-evidence.

Не входит:
- реализация Credential Provider;
- kernel driver;
- enterprise device management;
- скрытая установка;
- блокировка BIOS/UEFI или Safe Mode;
- настоящая защита от администратора, который осознанно удаляет программу.

## Runtime Architecture {#runtime-architecture}

Canonical architecture:
- `NightLock service` runs in the background, starts at boot, evaluates the schedule for logging, and supervises the helper (relaunching it during restricted hours if it is missing).
- `Session helper` runs in the active user session and owns all visible enforcement: the warning, the full-screen lock window, parent password verification, and the in-memory override.
- The service does not need a runtime IPC channel to the helper; both read the same machine-wide config, and the helper owns override state in memory.
- The helper must be restartable if it exits while the service is running; the service watches for this and relaunches it via the logon scheduled task.

Reason: Windows interactive desktop locking requires a process in the user desktop/session, so enforcement lives in the helper. The service provides startup, logging, and helper supervision. Keeping override state in the helper avoids storing tamperable override files and means killing the helper never grants access.

## Resource Budget {#resource-budget}

The runtime must be lightweight enough to stay installed permanently without making the computer feel slower.

Canonical decisions:
- prefer event-driven timers and Windows session events over constant polling;
- do not run a busy loop while waiting for 23:20, 23:30, unlock, or 08:00;
- keep the helper idle when no notification, lock, override prompt, or status UI is needed;
- avoid heavyweight UI frameworks if a small native/tray helper is enough;
- keep logging bounded and append only meaningful lifecycle/policy events;
- load config once and reload only on explicit change, service start, or controlled refresh;
- password hashing may be intentionally slow during setup/verification, but it must not run continuously in the background.

Target behavior:
- near-zero CPU usage while idle;
- small memory footprint for both service and helper;
- no noticeable disk activity except config reads, log writes, and install/update operations.

Exact numeric budgets may be added after the implementation language/runtime is selected.

## Service {#service}

- The service starts automatically with Windows.
- The service loads schedule config at startup.
- The service evaluates whether the current local time is restricted (for logging and supervision).
- During restricted hours, if the helper is missing, the service relaunches it via the logon scheduled task.
- The service reacts to session change events (logon/unlock/reconnect) so the helper is launched promptly.
- Service stop/uninstall is allowed only through explicit admin/uninstall flow in the first implementation.

## Session Helper {#session-helper}

- The helper starts at user logon.
- The helper has no normal "disable protection" button.
- The helper exposes a small status / re-lock tray menu.
- The helper shows the 10-minute warning.
- The helper shows the full-screen lock window during restricted hours and verifies the parent password to grant a temporary override.
- If the helper is manually closed and relaunched by the service, this is logged as tamper-evidence.

## Install Layout {#install-layout}

Default install path:
- `C:\Program Files\NightLockGuard\`

Config/log path:
- `C:\ProgramData\NightLockGuard\`

Rules:
- application binaries are not stored in user profile startup folders;
- config is not stored beside random user documents;
- file locations are ordinary and inspectable, not hidden as another application.

## Config {#config}

Config contains:
- lock window start: `23:30`;
- lock window end: `08:00`;
- warning offset: `10 minutes`;
- parent password verifier metadata;
- override policy defaults;
- emergency stop hotkey combination (default `LeftShift+RightShift+6+7`), see `spec://modules/core/FEAT-004-emergency-stop-hotkey#hotkey-config`;
- Windows key suppression toggle (default on), see `spec://modules/core/FEAT-005-windows-key-suppression#root`.

Schedule, override, hotkey, and the Windows-key toggle are editable through the parent admin panel (`spec://modules/core/FEAT-003-parent-admin-panel#root`) and the CLI. Editing config requires admin rights or parent password per the ACL/auth rules below; the config file itself is not writable by normal users.

Config must not contain:
- plaintext parent password;
- Windows account password;
- captured user input unrelated to the password prompt.

The data directory is ACL-hardened by the installer: `SYSTEM` and `Administrators` get full control, normal `Users` get read/execute on the data directory (so the helper can read the verifier) and modify only on the `logs` subfolder (so the helper can append logs). Config and the password verifier are not writable by normal users.

## Input Suppression Hook {#input-suppression-hook}

The helper may install a single low-level keyboard hook (`WH_KEYBOARD_LL`) in the user session, used only for two narrow purposes:

- block the Windows key while the night lock window is active (`FEAT-005`);
- detect the configurable emergency stop combination (`FEAT-004`).

Hard rules for the hook:
- it must never record, persist, buffer, or transmit keystroke content — it only inspects key state to decide block-vs-pass and to match the stop combination;
- it suppresses the Windows key only during restricted hours with no active override; outside that, it passes the Windows key through unchanged;
- it must not block `Ctrl+Alt+Del`, the secure attention sequence, or emergency shutdown/restart (those cannot be intercepted by a user-mode hook anyway);
- if hook installation fails, the rest of the runtime keeps working (lock, warning, override) and the failure is logged;
- the hook is owned by the helper process and is removed when the helper exits, so it cannot outlive enforcement.

This is the only place where a keyboard hook is permitted, and it stays within `spec://modules/core/PROP-001-product-boundaries#safety-boundaries`.

## Parent Admin App {#admin-app}

A separate parent admin application (`FEAT-003`) edits config (schedule, override, hotkey, Windows-key toggle, password).

- it is gated by the parent password before any setting is shown or changed;
- it is intentionally not discoverable through ordinary Windows search / Start-menu listing (no Start-menu shortcut, no search-indexed entry); it is launched from the helper tray menu and/or a known install path;
- obscurity is a convenience layer, not the security boundary — the parent password and the data-directory ACLs remain the real gate (see `spec://modules/core/PROP-001-product-boundaries#safety-boundaries`);
- it writes the same machine-wide config the service and helper read; changes apply on the next controlled config reload.

## Password Storage {#password-storage}

- Store only a salted password verifier, not the parent password.
- Use a slow password hashing function available in the chosen runtime, such as PBKDF2, bcrypt, scrypt, or Argon2.
- Store password hash parameters with the verifier so they can be upgraded later.
- Do not log entered passwords.

## Logs {#logs}

Logs should include:
- service start/stop;
- helper start/exit/restart;
- warning shown;
- lock requested;
- relock requested;
- override success/failure without recording password text;
- config changes;
- suspicious clock jumps when detectable.

Logs may be stored in Windows Event Log or `C:\ProgramData\NightLockGuard\logs`.

## Tamper Evidence {#tamper-evidence}

First version must prefer evidence over stealth:
- if service/helper stops unexpectedly, record it on next start when possible;
- if config is missing or invalid, fail closed during restricted hours when safely possible;
- if the clock jumps across policy boundaries, recalculate policy and log the jump.

This is not a hard security boundary against an administrator.

## Depends on {#depends-on}

- `spec://modules/core/PROP-001-product-boundaries#tamper-model`

## Supports {#supports}

- `spec://modules/core/FEAT-001-night-lock-window#root`
- `spec://modules/core/FEAT-002-parent-password-override#root`
- `spec://modules/core/FEAT-003-parent-admin-panel#root`
- `spec://modules/core/FEAT-004-emergency-stop-hotkey#root`
- `spec://modules/core/FEAT-005-windows-key-suppression#root`

## Acceptance {#acceptance}

- Runtime can start automatically after reboot.
- Runtime can show a user-visible warning before lock time.
- Runtime can request a lock in the active user session.
- Runtime can relock during the restricted window.
- Runtime stores parent password verifier without plaintext password.
- Runtime has near-zero CPU usage while idle and does not use constant polling for schedule enforcement.
- Runtime uses ordinary install paths and does not rely on hidden files as the main protection.
- Runtime logs meaningful lifecycle and policy events.

## Document Notes {#document-notes}

- 2026-06-18: Created first Windows runtime baseline.
- 2026-06-18: Added minimal resource usage as a runtime requirement.
- 2026-06-18: Enforcement consolidated in the helper (full-screen lock + in-memory override); runtime IPC pipe removed; service reduced to schedule logging + helper supervision; data-directory ACL hardening made explicit.
- 2026-06-19: Added the helper-owned input suppression hook (Win-key block + stop combo), the hidden parent admin app, and new config fields (hotkey, Win-key toggle) to support FEAT-003/004/005.
