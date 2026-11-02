param([switch]$SkipContainers, [switch]$NoBuild)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    if (-not (Test-Path -LiteralPath '.env')) {
        throw 'Create .env from .env.example and set a strong local SQL password first. See README.md.'
    }
    $passwordLine = Get-Content -LiteralPath '.env' | Where-Object { $_ -match '^MSSQL_SA_PASSWORD=' } | Select-Object -First 1
    if (-not $passwordLine) { throw 'MSSQL_SA_PASSWORD is missing from .env.' }
    $env:MSSQL_SA_PASSWORD = $passwordLine.Substring('MSSQL_SA_PASSWORD='.Length)
    if ($env:MSSQL_SA_PASSWORD -match 'CHANGE_ME|REPLACE|YOUR_' -or $env:MSSQL_SA_PASSWORD.Length -lt 12) {
        throw 'Replace the sample SQL password with a strong local value.'
    }
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    if (-not $SkipContainers) {
        docker compose up -d --wait --wait-timeout 240
        if ($LASTEXITCODE -ne 0) { throw 'Local services did not become healthy.' }
    }
    if ($NoBuild) { dotnet run --project src/Chubb.Claims.Api --launch-profile http --no-build }
    else { dotnet run --project src/Chubb.Claims.Api --launch-profile http }
    if ($LASTEXITCODE -ne 0) { throw 'Backend stopped with an error.' }
}
finally { Pop-Location }
