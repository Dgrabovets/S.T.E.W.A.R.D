---
name: new-job
description: Заготовка новой фоновой задачи Steward: проект, запись статуса, строка в config/jobs.json, тесты и README
disable-model-invocation: true
argument-hint: <id задачи> <название> <что делает>
---

# new-job

Создаёт заготовку новой задачи, которую запускает приложение в трее. Образец: `Steward.Mail` (проект), `tests/Steward.Mail.Tests` (тесты), `Steward.Mail/README.md` (описание). Формат статуса описан в `Steward.Status/README.md`.

Если из аргументов неясны `id`, название или что делает задача, спроси.

## Что сделать

1. **Проект** `Steward.<Имя>/Steward.<Имя>.csproj` по образцу `Steward.Mail.csproj`: `net10.0-windows10.0.17763.0`, `Nullable`, `ImplicitUsings`, `GenerateDocumentationFile`, ссылка на `Steward.Status`. Добавь проект в `Steward.slnx`.
2. **Program.cs**: `Main` возвращает 0 при успехе и 1 при ошибке, в начале `Console.OutputEncoding = new UTF8Encoding(false)`, статус пишется через `StatusStore(DataLocation.StatusDirectory())`. Идентификатор вынеси в константу `STATUS_ID` (латиница в нижнем регистре, цифры, дефис, подчёркивание, до 64 символов). Поддержи `--dry-run`: статус не меняется.
3. **Настройки**: `appsettings.example.json` в git, настоящий `appsettings.json` в `.gitignore` проекта (как у `Steward.Mail/.gitignore`). Секреты в примере не пишутся.
4. **config/jobs.json**: новая запись с `id`, `title`, `enabled` (по умолчанию `false`, пока нет настроек), `command` вида `{repo}\Steward.<Имя>\publish\Steward.<Имя>.exe`, `triggers` (`start`, `daily` с `at`, `interval` с `everyMinutes`), `timeoutMinutes`, `staleAfterMinutes`. Комментарий над записью, как у соседних: что нужно для запуска и команда `dotnet publish`.
5. **Тесты** `tests/Steward.<Имя>.Tests` по образцу `Steward.Mail.Tests` (общая подготовка в одном вспомогательном методе, без повторов). Добавь в `Steward.slnx`.
6. **README.md** проекта в стиле `Steward.Mail/README.md`: что делает, кто запускает, куда пишет статус, настройка, сборка, тесты. Добавь строку в таблицу корневого `README.md`.
7. Следуй правилам из `.cursor/rules/csharp-style.mdc` (XML-комментарии у всех объявлений, один тип в файле, `cancellationToken`, `// ignore` в пустом `catch`, перечисления с суффиксом `Enum`).
8. Проверка: `dotnet build Steward.slnx` и `dotnet test tests/Steward.<Имя>.Tests`.

Не публикуй проект и не включай задачу (`enabled: true`) без просьбы пользователя.
