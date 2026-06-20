# FEAT-002: Parent Password Override {#root}

## Простыми словами {#plain-language}

После 23:30 ребенок может увидеть, что компьютер заблокирован ночным правилом. Чтобы временно разрешить использование, взрослый вводит отдельный родительский пароль NightLock Guard. Это не пароль Windows и не замена входа в Windows.

## Goal {#goal}

Добавить отдельный parent-control пароль, который временно отключает ночную relock-политику без изменения Windows account password.

## Scope {#scope}

Входит:
- initial parent password setup;
- password verification;
- temporary override;
- override expiration;
- audit/logging of success/failure;
- safe password storage requirements.

Не входит:
- восстановление забытого пароля через email/cloud;
- синхронизация между компьютерами;
- разные роли пользователей;
- замена Windows lock screen;
- хранение или проверка Windows account password.

## Password Setup {#password-setup}

- A parent password must be set during install or first launch before override can be used.
- Password confirmation is required during setup.
- The password must not be stored in plaintext.
- The implementation may enforce a minimum length, but version 1 should avoid complex password rules unless needed.

## Override Prompt {#override-prompt}

- During restricted hours, the parent password is entered directly in the full-screen lock window (`FEAT-001`).
- The prompt must clearly say it is the NightLock Guard parent password, not the Windows password.
- Password input must be masked.
- Failed attempts do not reveal which part was wrong.
- Entered password text is never logged.

## Override Behavior {#override-behavior}

On successful password verification:
- create an active override for a bounded duration;
- duration is configurable (default 30 minutes, clamped to 1-240 minutes);
- the override is held in the session helper and is not persisted to disk, so killing the helper does not grant access;
- during active override, `FEAT-001` must not show the lock window because of the night window;
- when override expires, normal restricted-window policy resumes.

Out of current scope:
- unlimited override;
- per-application override;
- remote approval.

## Failed Attempts {#failed-attempts}

- Failed attempts are logged without password text.
- After repeated failures, the UI may add a short delay before another attempt.
- Version 1 does not need account lockout, because the first threat model is casual misuse and local admin remains an accepted limitation.

## Cancellation {#cancellation}

- A parent may cancel an active override from the helper UI if that UI exists in the implementation.
- If no cancel UI exists in version 1, override naturally expires after the configured duration.

## Depends on {#depends-on}

- `spec://modules/core/PROP-001-product-boundaries#safety-boundaries`
- `spec://modules/core/INFRA-001-windows-runtime-baseline#password-storage`

## Supports {#supports}

- `spec://modules/core/FEAT-001-night-lock-window#parent-override-contract`

## Acceptance {#acceptance}

- Parent password can be set without storing plaintext.
- During restricted hours, valid parent password creates a temporary override.
- While override is active, night relock is paused.
- After override expiration, relock policy resumes if still inside 23:30-08:00.
- Failed password attempts do not leak or log the entered password.
- The feature never asks for or stores the Windows account password.

## Document Notes {#document-notes}

- 2026-06-18: Created first parent override spec.
- 2026-06-18: Override duration promoted to configurable (1-240 min); override is entered in the full-screen lock window and held in-memory by the helper.
