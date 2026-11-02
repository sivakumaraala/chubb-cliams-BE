param([switch]$WithSqlServer)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    if ($WithSqlServer) {
        $passwordLine = Get-Content -LiteralPath '.env' | Where-Object { $_ -match '^MSSQL_SA_PASSWORD=' } | Select-Object -First 1
        if (-not $passwordLine) { throw 'Local SQL password is missing from .env.' }
        $testPassword = $passwordLine.Substring('MSSQL_SA_PASSWORD='.Length)
        $env:CHUBB_TEST_SQL = "Server=127.0.0.1,14333;Database=ChubbClaimsTestBootstrap;User Id=sa;Password=$testPassword;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5"
    }
    dotnet test Chubb.Claims.slnx --disable-build-servers --verbosity minimal -m:1
    if ($LASTEXITCODE -ne 0) { throw 'One or more backend checks failed.' }
}
finally {
    if ($WithSqlServer) { Remove-Item Env:CHUBB_TEST_SQL -ErrorAction SilentlyContinue }
    Pop-Location
}
