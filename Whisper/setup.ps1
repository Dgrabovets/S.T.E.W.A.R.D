# Создаёт виртуальное окружение рядом со скриптом, ставит faster-whisper с библиотеками CUDA и скачивает модель
# Запуск: powershell -ExecutionPolicy Bypass -File setup.ps1 [-Model large-v3]
param([string]$Model = 'large-v3')

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# Кэши и временные файлы остаются рядом со скриптом, а не на системном диске
$cache = Join-Path $root '.cache'
foreach ($name in 'tmp', 'pip', 'hf') { New-Item -ItemType Directory -Force (Join-Path $cache $name) | Out-Null }
$env:TEMP = Join-Path $cache 'tmp'
$env:TMP = $env:TEMP
$env:PIP_CACHE_DIR = Join-Path $cache 'pip'
$env:HF_HOME = Join-Path $cache 'hf'

$python = Join-Path $env:LOCALAPPDATA 'Programs\Python\Python312\python.exe'
if (-not (Test-Path $python)) {
    $python = (Get-Command python).Source
}

$venv = Join-Path $root '.venv'
if (-not (Test-Path $venv)) {
    & $python -m venv $venv
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось создать виртуальное окружение' }
}

$py = Join-Path $venv 'Scripts\python.exe'
& $py -m pip install --upgrade pip
if ($LASTEXITCODE -ne 0) { throw 'pip не обновился' }

& $py -m pip install -r (Join-Path $root 'requirements.txt')
if ($LASTEXITCODE -ne 0) { throw 'Не удалось поставить зависимости' }

& $py -c "import sys; from faster_whisper.utils import download_model; print(download_model(sys.argv[1], cache_dir=sys.argv[2]))" $Model (Join-Path $root 'models')
if ($LASTEXITCODE -ne 0) { throw 'Не удалось скачать модель' }

Write-Host "Готово. Проверка: $py transcribe.py <файл>"
