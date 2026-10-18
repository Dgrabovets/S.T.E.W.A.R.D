$root = $PSScriptRoot
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

$publish = Join-Path $root "publish\tray"
dotnet publish (Join-Path $root "UltimateParser.Tray\UltimateParser.Tray.csproj") -c Release -o $publish
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$already = Get-Process -Name "UltimateParser.Tray" -ErrorAction SilentlyContinue
if (-not $already) {
    Start-Process (Join-Path $publish "UltimateParser.Tray.exe") -WorkingDirectory $root
}

exit 0
