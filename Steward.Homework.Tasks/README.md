# Steward.Homework.Tasks

Создаёт задачи SingularityApp по заданиям из `data/homework/items`. Подробности потока: [Steward.Homework](../Steward.Homework/README.md).

## Настройка

```powershell
dotnet publish Steward.Homework.Tasks -c Release -o Steward.Homework.Tasks/publish
```

Скопируй `appsettings.example.json` в `publish/appsettings.json` и впиши токен SingularityApp (тот же, что у почты). Файл в `.gitignore`. Потом в `config/jobs.json` поставь у `homework` `"enabled": true`.