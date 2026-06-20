# BOARD

> Operational source of truth for active work items.

## Backlog

| ID | Title | Module | Owner | Priority | Scope | Plain words |
|----|-------|--------|-------|----------|-------|-------------|

## In Progress

| ID | Title | Module | Owner | Started | Blockers | Scope | Plain words |
|----|-------|--------|-------|---------|----------|-------|-------------|
| INFRA-001 | Windows runtime baseline | core | @codex | 2026-06-18 | Windows run/build verification pending (dev host is macOS, no SDK) | service supervises helper; self-bootstrapping installer; hardened ACLs; Windows-only | Техническая основа, чтобы программа запускалась вместе с Windows, почти не ела ресурсы в фоне и могла блокировать сессию. |
| FEAT-001 | Night lock window | core | @codex | 2026-06-18 | Windows run verification pending | warning, full-screen lock window, local time rules | Основная функция: предупредить в 23:20 и блокировать компьютер ночью полноэкранным замком. |
| FEAT-002 | Parent password override | core | @codex | 2026-06-18 | Windows run verification pending | parent password in lock window, configurable in-memory override, safe verifier storage | Отдельный родительский пароль, чтобы временно разрешить доступ ночью. |
| FEAT-003 | Parent admin panel | core | @claude | 2026-06-19 | Build + unit tests green on Windows 2026-06-19; interactive run (elevated panel saving config) pending | hidden, password-gated NightLock.Admin app; edits schedule/override/Win-key/hotkey/password; elevated; writes shared config | Простой защищенный паролем экран настроек для родителя, не видимый в обычном поиске Windows. |
| FEAT-004 | Emergency stop hotkey | core | @claude | 2026-06-19 | Build + unit tests green on Windows 2026-06-19; interactive run (combo stops enforcement) pending | configurable combo (default LShift+RShift+6+7) stops enforcement until restart; non-logging hook | Секретная настраиваемая комбинация клавиш, которой родитель быстро останавливает программу. |
| FEAT-005 | Windows key suppression | core | @claude | 2026-06-19 | Build + unit tests green on Windows 2026-06-19; interactive run (Win key blocked during lock) pending | block Win key only during restricted hours via helper hook; toggle in admin panel/CLI | Ночью во время замка клавиша Windows отключается, чтобы нельзя было открыть Пуск; днем работает. |

## Blocked

| ID | Reason | Waiting for | Scope | Plain words |
|----|--------|-------------|-------|-------------|

## Done

| ID | Title | Owner | Date |
|----|-------|-------|------|
