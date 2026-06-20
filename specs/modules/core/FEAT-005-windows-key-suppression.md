# FEAT-005: Windows Key Suppression {#root}

## Простыми словами {#plain-language}

Когда ночью на экране висит замок, ребенок не должен иметь возможность нажать клавишу Windows и открыть меню Пуск или свернуть замок. Поэтому во время ночной блокировки клавиша Windows временно отключается. Днем и вне блокировки она работает как обычно.

## Goal {#goal}

Не дать обойти ночной lock через клавишу Windows (меню Пуск, Win+D, Win+Tab и т.п.), блокируя ее только на время restricted-окна.

## Scope {#scope}

Входит:
- блокировка левой и правой клавиши Windows и связанных Win-комбинаций;
- активна только во время restricted-окна без активного override;
- тумблер вкл/выкл в родительской панели (`FEAT-003`), по умолчанию вкл;
- снятие блокировки при выходе из restricted-окна, при активном override и при остановке через `FEAT-004`.

Не входит:
- блокировка `Ctrl+Alt+Del` / secure attention sequence;
- блокировка Alt+Tab, Task Manager и прочего (это TD-002, вне scope);
- блокировка Win key вне ночного окна;
- логирование нажатий.

## Suppression Window {#suppression-window}

- Win key подавляется только когда: текущее время в restricted-окне (`FEAT-001#schedule`) И нет активного override (`FEAT-002`) И тумблер включен.
- Как только условие перестает выполняться (08:00, override, остановка, выключенный тумблер), Win key снова проходит нормально.
- Подавление реализуется helper-owned hook из `spec://modules/core/INFRA-001-windows-runtime-baseline#input-suppression-hook`.

## Suppressed Keys {#suppressed-keys}

- Левый и правый Windows key (LWin / RWin) и их комбинации (Win+R, Win+D, Win+E, Win+Tab и т.п.) не доходят до системы во время окна подавления.
- Hook только решает «пропустить/заблокировать» по коду клавиши; он не записывает и не хранит ввод.

## Boundaries {#boundaries}

- Не блокируется `Ctrl+Alt+Del` (user-mode hook этого и не может) и не нарушается emergency shutdown/restart.
- Если hook не установился, lock-окно все равно работает; невозможность подавить Win key логируется и считается известным ограничением, а не падением (`FEAT-001` enforcement продолжается).
- Подавление снимается вместе с выходом helper, чтобы не «залипнуть» отключенной клавишей после остановки enforcement.

## Failure Behavior {#failure-behavior}

- Сбой установки/снятия hook логируется.
- Lock-окно остается первичной мерой; блокировка Win key — усиление, а не единственная защита.
- При остановке через `FEAT-004` или истечении override hook снимается, Win key восстанавливается.

## Depends on {#depends-on}

- `spec://modules/core/INFRA-001-windows-runtime-baseline#input-suppression-hook`
- `spec://modules/core/FEAT-001-night-lock-window#schedule`
- `spec://modules/core/FEAT-002-parent-password-override#override-behavior`

## Related {#related}

- `spec://modules/core/FEAT-003-parent-admin-panel#editable-settings`
- `spec://modules/core/FEAT-004-emergency-stop-hotkey#stop-behavior`

## Acceptance {#acceptance}

- Во время restricted-окна без override нажатие Windows key не открывает меню Пуск и не сворачивает lock.
- Вне restricted-окна, при активном override или выключенном тумблере Win key работает нормально.
- `Ctrl+Alt+Del` и emergency shutdown/restart не блокируются.
- Сбой hook не ломает lock-окно и логируется.
- При остановке enforcement или выходе helper Win key восстанавливается.

## Document Notes {#document-notes}

- 2026-06-19: Создан первый спек подавления Windows key во время ночного окна.
