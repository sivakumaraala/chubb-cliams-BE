param([Alias('WithSqlServer')][switch]$WithLocalDb)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    if ($WithLocalDb) {
        if (-not (Get-Command sqllocaldb -ErrorAction SilentlyContinue)) {
            throw 'SQL Server Express LocalDB is required for SQL integration tests.'
        }
        sqllocaldb start MSSQLLocalDB
        if ($LASTEXITCODE -ne 0) { throw 'LocalDB could not start; SQL integration tests were not run.' }
        $env:CHUBB_TEST_SQL = 'Server=(localdb)\MSSQLLocalDB;Database=ChubbClaimsTestBootstrap;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=5'
    }
    dotnet test Chubb.Claims.slnx --disable-build-servers --verbosity minimal -m:1
    if ($LASTEXITCODE -ne 0) { throw 'One or more backend checks failed.' }
}
finally {
    if ($WithLocalDb) { Remove-Item Env:CHUBB_TEST_SQL -ErrorAction SilentlyContinue }
    Pop-Location
}
