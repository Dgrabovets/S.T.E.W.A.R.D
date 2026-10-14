$root = $PSScriptRoot
$deadline = (Get-Date).AddMinutes(5)

while ((Get-Date) -lt $deadline) {
    docker info --format "{{.ServerVersion}}" *> $null
    if ($LASTEXITCODE -eq 0) {
        break
    }

    Start-Sleep -Seconds 5
}

Set-Location $root
docker compose up -d
exit $LASTEXITCODE
