# Steward.Homework.Tasks

Создаёт задачи SingularityApp по заданиям из `data/homework/items`. Подробности потока: [Steward.Homework](../Steward.Homework/README.md).

## Настройка

```powershell
dotnet publish Steward.Homework.Tasks -c Release -o Steward.Homework.Tasks/publish
```

Скопируй `appsettings.example.json` в `publish/appsettings.json` и впиши токен SingularityApp (тот же, что у почты). Файл в `.gitignore`.

## Что учесть

Это отдельная программа: она всегда заводит задачи и не читает флаг `CreateTasks` из настроек `Steward.Lms`. В `config/jobs.json` её больше нет, приложение её не запускает и на «Статусе» не показывает; запускай её вручную и только осознанно. Задания, которые ждут подтверждения, сданные и убранные в архив задач не получают. Основной путь заведения задач (под флагом `CreateTasks`, который включает только владелец) идёт через `Steward.Lms`, шаг `publish`.
