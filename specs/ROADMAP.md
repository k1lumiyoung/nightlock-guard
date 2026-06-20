# ROADMAP

## Wave 1 - Spec Canon

- Define product boundaries and safety limits.
- Define Windows runtime architecture.
- Define night lock behavior.
- Define parent password override behavior.

## Wave 2 - Minimal Implementation

- Build installable Windows runtime.
- Show warning at 23:20.
- Lock and relock from 23:30 until 08:00.
- Add parent password override.

## Wave 3 - Hardening

- Add logs and diagnostics.
- Add basic tamper-evidence for stopped service, killed helper, config edits, and system clock jumps.
- Improve installer/uninstaller UX.

## Wave 4 - Parent UX & Input Control

- Add the hidden, password-gated parent admin panel (schedule, override, hotkey, Win-key toggle, password) — FEAT-003.
- Add the configurable emergency stop hotkey (default LeftShift+RightShift+6+7) — FEAT-004.
- Add Windows key suppression during restricted hours via the helper input hook — FEAT-005.
- Carries the PROP-001 amendment allowing a narrow, non-logging keyboard hook.
