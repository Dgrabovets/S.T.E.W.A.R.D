# УСТАРЕЛО. Проверку почты теперь запускает приложение в трее по config/jobs.json (при старте, в 09:00 и в 19:00).
# Если поставить ещё и задачу в Планировщик, проверка пойдёт дважды. Скрипт оставлен на случай, если приложение не нужно.
# Регистрирует задачу в Планировщике Windows: проверка почты в 09:00 и 19:00
# Запуск: powershell -ExecutionPolicy Bypass -File install-schedule.ps1 -Force из папки с Steward.Mail.exe
param([switch]$Force)

if (-not $Force) {
    Write-Error "Скрипт устарел: проверку почты запускает приложение в трее. Если всё же нужна задача Планировщика, запустите с ключом -Force"
    exit 1
}

$exe = Join-Path $PSScriptRoot "Steward.Mail.exe"
if (-not (Test-Path $exe)) {
    Write-Error "Не найден $exe — сначала соберите проект (dotnet publish) и положите скрипт рядом с exe"
    exit 1
}

$action   = New-ScheduledTaskAction -Execute $exe -WorkingDirectory $PSScriptRoot
$triggers = @(
    New-ScheduledTaskTrigger -Daily -At 09:00
    New-ScheduledTaskTrigger -Daily -At 19:00
)
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries

Register-ScheduledTask -TaskName "Учебная почта → SingularityApp" `
    -Action $action -Trigger $triggers -Settings $settings `
    -Description "Проверяет dgrabovets-26@ranepa.ru и ставит задачи в SingularityApp" -Force

Write-Host "Готово. Задача запустится в 09:00 и 19:00; если ПК был выключен — при следующем включении."
