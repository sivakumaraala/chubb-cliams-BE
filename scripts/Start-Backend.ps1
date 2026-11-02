param([switch]$SkipContainers, [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    if (-not (Get-Command sqllocaldb -ErrorAction SilentlyContinue)) {
        throw 'SQL Server Express LocalDB is required. Install it and check README.md.'
    }
    sqllocaldb start MSSQLLocalDB
    if ($LASTEXITCODE -ne 0) {
        throw 'LocalDB could not start. Check the Windows Application event log and the LocalDB error.log path in README.md.'
    }
    if (-not $SkipContainers) {
        docker compose up -d --wait --wait-timeout 240 kafka
        if ($LASTEXITCODE -ne 0) { throw 'Local Kafka did not become healthy.' }
    }
    if ($NoBuild) { dotnet run --project src/Chubb.Claims.Api --launch-profile http --no-build }
    else { dotnet run --project src/Chubb.Claims.Api --launch-profile http }
    if ($LASTEXITCODE -ne 0) { throw 'Backend stopped with an error.' }
}
finally { Pop-Location }
