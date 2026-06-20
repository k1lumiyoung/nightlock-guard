# PROP-001: Product Boundaries {#root}

## Простыми словами {#plain-language}

NightLock Guard - это маленькая программа родительского контроля для домашнего компьютера. Она не следит за переписками, не крадет данные и не пытается быть скрытым вредоносным софтом. Ее задача одна: ночью не давать пользоваться компьютером без родительского пароля.

## Goal {#goal}

Зафиксировать границы продукта, актеров, модель обхода и правила безопасности, чтобы реализация оставалась простой и честной.

## Scope {#scope}

Входит:
- акторы и их права;
- ночное ограничение;
- родительский override;
- best-effort защита от простого отключения;
- минимальное потребление ресурсов в фоне;
- запрет на скрытую или вредоносную persistence-логику.

Не входит:
- мониторинг активности пользователя;
- запись экрана, клавиатуры или паролей;
- блокировка сайтов или приложений;
- enterprise MDM;
- Windows Credential Provider;
- попытка защититься от владельца компьютера с полными admin-навыками.

## Actors {#actors}

- `Parent` - человек, который знает родительский пароль NightLock Guard и управляет расписанием.
- `Child` - человек, которому ночью ограничивается доступ к компьютеру.
- `Windows account` - учетная запись Windows, в первой версии может быть администраторской.
- `NightLock service` - фоновый runtime, отвечающий за schedule policy and enforcement.
- `Session helper` - процесс в пользовательской сессии, отвечающий за уведомления и вызов блокировки интерактивного рабочего стола.

## Tamper Model {#tamper-model}

Первая версия защищает от простого обхода:
- закрыть обычное окно программы;
- перезагрузить компьютер;
- дождаться входа после 23:30;
- попробовать продолжить работу после ручной разблокировки Windows.

Первая версия не обещает защиту от администратора, который умеет:
- останавливать Windows services;
- удалять файлы из `Program Files`;
- менять права доступа;
- редактировать конфиг;
- менять системное время;
- удалять задачу автозапуска или саму программу.

Canonical decision: такое ограничение допустимо для первой версии, потому что человек явно выбрал режим "пока на пользователе с админкой".

## Safety Boundaries {#safety-boundaries}

- Программа не должна маскироваться под системный процесс.
- Программа не должна прятать файлы в неожиданных местах.
- Программа не должна перехватывать или хранить Windows password.
- Программа не должна устанавливать keyboard hooks для логирования или хранения нажатий (keylogging запрещен).
- Допускается узкий low-level keyboard hook ТОЛЬКО для блокировки конкретных клавиш (Windows key во время ночного окна) и для распознавания родительской комбинации остановки. Такой hook не записывает, не сохраняет и не передает наружу содержимое ввода; его границы заданы в `spec://modules/core/INFRA-001-windows-runtime-baseline#input-suppression-hook`.
- Программа не должна препятствовать emergency shutdown/restart.
- Uninstall/configuration may require administrator rights or parent password, but the mechanism must be explicit.

## Resource Boundary {#resource-boundary}

- Программа не должна заметно замедлять компьютер ради одной ночной блокировки.
- В обычном дневном режиме background runtime должен быть почти полностью idle.
- Нельзя реализовывать расписание через постоянный CPU polling-loop.
- Более точные resource budgets задает `spec://modules/core/INFRA-001-windows-runtime-baseline#resource-budget`.

## Related {#related}

- `spec://modules/core/INFRA-001-windows-runtime-baseline#root`
- `spec://modules/core/FEAT-001-night-lock-window#root`
- `spec://modules/core/FEAT-002-parent-password-override#root`
- `spec://modules/core/FEAT-003-parent-admin-panel#root`
- `spec://modules/core/FEAT-004-emergency-stop-hotkey#root`
- `spec://modules/core/FEAT-005-windows-key-suppression#root`

## Acceptance {#acceptance}

- Реализация делает только ночную блокировку и parent override.
- Любые hardening-механизмы остаются best-effort and transparent.
- Фоновая работа остается легкой и не создает заметной нагрузки в idle.
- Из acceptance будущих `FEAT`/`INFRA` нельзя вывести скрытый surveillance или вредоносную persistence.

## Document Notes {#document-notes}

- 2026-06-18: Зафиксирована первая модель продукта и ограничение по admin-пользователю.
- 2026-06-18: Добавлена граница по минимальному потреблению ресурсов.
- 2026-06-19: Safety boundary уточнен: keylogging по-прежнему запрещен, но разрешен узкий non-logging keyboard hook для блокировки Windows key в ночном окне и для распознавания родительской комбинации остановки (см. FEAT-004, FEAT-005, INFRA-001#input-suppression-hook).
