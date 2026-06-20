# TECHDEBT

## Accepted Constraints

### TD-001: First version may run on an administrator Windows account

- Related: `spec://modules/core/PROP-001-product-boundaries#tamper-model`
- Accepted: 2026-06-18
- Reason: The first target user is not expected to know Windows admin-level bypasses. The goal is to prevent casual late-night use, not defeat a determined administrator.
- Consequence: A user with administrator rights may stop services, delete files, modify scheduled tasks, change the clock, or uninstall the program.
- Follow-up: If stronger enforcement becomes necessary, move the sibling to a standard Windows account and require administrator credentials for install/uninstall/config changes.

### TD-002: Full-screen lock is best-effort, not a secure desktop

- Related: `spec://modules/core/FEAT-001-night-lock-window#behavior.lock-window`
- Accepted: 2026-06-18
- Reason: The lock is a normal top-most window, not a Windows Credential Provider or secure desktop. It installs only the narrow non-logging keyboard hook of `spec://modules/core/INFRA-001-windows-runtime-baseline#input-suppression-hook` (Win-key block + stop combo) and still does not block `Ctrl+Alt+Del` or Task Manager (excluded by `spec://modules/core/PROP-001-product-boundaries#safety-boundaries`).
- Consequence: A user who opens Task Manager and kills `NightLock.Helper` exposes the desktop until the service relaunches the helper (a few seconds during restricted hours), and killing the helper also drops Win-key suppression until it restarts. Alt+Tab focus stealing is mitigated by re-asserting top-most, not prevented.
- Follow-up: A Credential Provider or secure-desktop approach would be required for hard enforcement; out of scope for v1.

### TD-003: Override is in-memory and lost on helper restart

- Related: `spec://modules/core/FEAT-002-parent-password-override#override-behavior`
- Accepted: 2026-06-18
- Reason: Keeping the override only in the helper process avoids a tamperable override file and ensures killing the helper never grants access.
- Consequence: If the helper restarts (crash, logoff/logon) while an override is active, the override is lost and the parent must re-enter the password.
- Follow-up: Acceptable for v1; a signed, service-owned override store could persist it later if needed.

### TD-005: Emergency stop hotkey is a shared secret, not a credential

- Related: `spec://modules/core/FEAT-004-emergency-stop-hotkey#security-note`
- Accepted: 2026-06-19
- Reason: The parent wants a fast, no-password way to stop enforcement. The combo (default LeftShift+RightShift+6+7, configurable) gates on secrecy, not on the parent password.
- Consequence: Anyone who learns or guesses the combo can stop enforcement without the password. Mitigated by making the combo configurable so the parent can pick a non-obvious one.
- Follow-up: If casual-misuse threat grows, require the parent password after the combo, or rate-limit/log repeated stop attempts.

### TD-006: Hidden admin panel is obscurity, not a security boundary

- Related: `spec://modules/core/FEAT-003-parent-admin-panel#discoverability`
- Accepted: 2026-06-19
- Reason: The panel is kept out of Windows search/Start-menu so a child does not stumble onto it, but the real gate is the parent password plus the data-directory ACLs.
- Consequence: A user who finds the executable path can still launch it; they just cannot read or change settings without the parent password.
- Follow-up: None required for v1; the ACL + password gate is the actual protection.

### TD-004: Build requires the .NET SDK on the Windows machine

- Related: `spec://modules/core/INFRA-001-windows-runtime-baseline#runtime-architecture`
- Accepted: 2026-06-18
- Reason: No prebuilt binaries are shipped; the installer compiles from source. It auto-installs the .NET 8 SDK via winget when missing and publishes self-contained so the runtime is not separately required afterward.
- Consequence: First install needs internet access (for winget/SDK) and a one-time SDK install (~hundreds of MB).
- Follow-up: A CI pipeline could publish ready-to-run self-contained binaries to skip the SDK requirement entirely.
