# FEAT-001: Night Lock Window {#root}

## Простыми словами {#plain-language}

Каждый день в 23:20 программа предупреждает, что через 10 минут компьютер будет заблокирован. В 23:30 компьютер блокируется. До 08:00, если пользователь снова входит в Windows без родительского override, программа снова блокирует сессию.

## Goal {#goal}

Реализовать основную функцию NightLock Guard: ежедневную ночную блокировку Windows-сессии с предупреждением заранее.

## Scope {#scope}

Входит:
- daily local-time schedule;
- warning at 23:20;
- lock at 23:30;
- relock until 08:00;
- behavior after reboot/logon during restricted hours;
- interaction with parent override state.

Не входит:
- настройка расписания через UI;
- разные расписания по дням недели;
- учет лимита экранного времени;
- сайты, приложения, процессы;
- parent password prompt details, кроме contract с `FEAT-002`.

## Schedule {#schedule}

Canonical schedule:
- warning time: `23:20`;
- restricted window start: `23:30` inclusive;
- restricted window end: `08:00` exclusive;
- timezone: Windows local time of the machine.

The window crosses midnight. For example:
- Thursday 23:30 to Friday 08:00 is one restricted window;
- Friday 07:59 is restricted;
- Friday 08:00 is unrestricted;
- Friday 23:29 is not yet restricted.

## Warning Behavior {#behavior.warning}

- At 23:20, if an interactive user session is active, show a visible warning.
- The warning says that the computer will lock at 23:30.
- The warning should remain understandable even if the user ignores it.
- If the helper starts between 23:20 and 23:30, it should show the warning immediately.
- If no user is logged in at 23:20, no warning is required until next active session.

## Lock Behavior {#behavior.lock-window}

- At 23:30, if an interactive user session is active and no valid parent override is active, the session helper shows a full-screen lock window that covers all monitors.
- While the lock window is shown, the only available action is entering the parent password (see `FEAT-002`); it stays on top and cannot be closed by the user.
- If the computer boots or a user logs in during the restricted window, show the lock window as soon as the helper can apply policy.
- If the helper is killed during the restricted window, the service relaunches it so the lock window returns.
- If the user dismisses or escapes the lock window (e.g. by killing the helper) the lock reappears on relaunch; the helper also re-shows the lock on session unlock/reconnect within the relock target.
- Target relock latency: within 5 seconds after policy enforcement notices an exposed session.
- At 08:00, hide the lock window and allow normal Windows use.

## Parent Override Contract {#parent-override-contract}

`FEAT-001` does not define password UI. It consumes override state from `FEAT-002`.

If a valid override is active:
- do not lock only because of the night window;
- keep showing/logging that protection is temporarily overridden;
- resume lock behavior when the override expires or is cancelled.

## Clock Behavior {#clock-behavior}

- The policy follows current Windows local time.
- If time changes into the restricted window, policy applies.
- If time changes out of the restricted window, policy stops applying unless future hardening changes this behavior.
- Detectable large clock jumps should be logged as tamper-evidence, not treated as a hard security boundary in version 1.

## Failure Behavior {#failure-behavior}

- If config is missing or invalid, use the default schedule from this spec.
- If the helper cannot show notification, locking behavior still applies.
- If locking fails, retry while inside the restricted window and log the failure.

## Depends on {#depends-on}

- `spec://modules/core/PROP-001-product-boundaries#tamper-model`
- `spec://modules/core/INFRA-001-windows-runtime-baseline#runtime-architecture`

## Related {#related}

- `spec://modules/core/FEAT-002-parent-password-override#root`

## Acceptance {#acceptance}

- At 23:20 local time, an active user sees a warning.
- At 23:30 local time, the full-screen lock window appears unless a valid parent override is active.
- Between 23:30 and 08:00, escaping the lock without a valid override results in the lock reappearing within the relock target.
- At and after 08:00, the lock window is hidden and normal use is allowed.
- Reboot/logon during restricted hours still leads to the lock window.
- The default schedule works even before schedule customization exists.

## Document Notes {#document-notes}

- 2026-06-18: Created first canonical night lock behavior.
- 2026-06-18: Lock mechanism changed from Windows workstation lock + relock loop to a helper-owned full-screen lock window, so the only interaction during restricted hours is the parent password.
