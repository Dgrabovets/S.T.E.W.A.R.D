$root = $PSScriptRoot
$exitCode = 0

# Сначала иконка: она запускает задачи из config/jobs.json, и им не нужно ждать Docker
$publish = Join-Path $root "publish\tray"
$trayExe = Join-Path $publish "UltimateParser.Tray.exe"
$traySources = @("UltimateParser.Tray", "UltimateParser.Shared", "..\Status") | ForEach-Object { Join-Path $root $_ }
$newestSource = Get-ChildItem -Path $traySources -Recurse -File -Include *.cs, *.xaml, *.csproj |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

# Иконка собирается заново только после правок её исходников: так вход в Windows не ждёт dotnet publish
$trayStale = -not (Test-Path $trayExe) -or ($newestSource -and $newestSource.LastWriteTime -gt (Get-Item $trayExe).LastWriteTime)
if ($trayStale) {
    dotnet publish (Join-Path $root "UltimateParser.Tray\UltimateParser.Tray.csproj") -c Release -o $publish
    if ($LASTEXITCODE -ne 0) {
        $exitCode = $LASTEXITCODE
    }
}

$already = Get-Process -Name "UltimateParser.Tray" -ErrorAction SilentlyContinue
if (-not $already -and (Test-Path $trayExe)) {
    Start-Process $trayExe -WorkingDirectory $root
}

# Docker нужен только расписанию пар. Если он не поднялся, иконка уже работает и покажет «Docker не запущен»
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
