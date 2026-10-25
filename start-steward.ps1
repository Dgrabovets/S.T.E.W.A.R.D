$root = $PSScriptRoot
$exitCode = 0

# Сначала приложение: оно запускает задачи из config/jobs.json, и им не нужно ждать Docker
$publish = Join-Path $root "publish\app"
$appExe = Join-Path $publish "Steward.exe"
$appSources = @("Steward", "Steward.Status", "Steward.Schedule.Shared") | ForEach-Object { Join-Path $root $_ }
$newestSource = Get-ChildItem -Path $appSources -Recurse -File -Include *.cs, *.xaml, *.csproj |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

# Приложение собирается заново только после правок его исходников: так вход в Windows не ждёт dotnet publish
$appStale = -not (Test-Path $appExe) -or ($newestSource -and $newestSource.LastWriteTime -gt (Get-Item $appExe).LastWriteTime)
if ($appStale) {
    dotnet publish (Join-Path $root "Steward\Steward.csproj") -c Release -o $publish
    if ($LASTEXITCODE -ne 0) {
        $exitCode = $LASTEXITCODE
    }
}

$already = Get-Process -Name "Steward" -ErrorAction SilentlyContinue
if (-not $already -and (Test-Path $appExe)) {
    Start-Process $appExe -WorkingDirectory $root
}

# Docker нужен только расписанию пар. Если он не поднялся, приложение уже работает и покажет «Docker не запущен»
$deadline = (Get-Date).AddMinutes(5)
$dockerReady = $false

while ((Get-Date) -lt $deadline) {
    docker info --format "{{.ServerVersion}}" *> $null
    if ($LASTEXITCODE -eq 0) {
        $dockerReady = $true
        break
    }

    Start-Sleep -Seconds 5
}

if (-not $dockerReady) {
    exit 1
}

Set-Location $root
docker compose up -d --force-recreate --no-build
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

exit $exitCode
