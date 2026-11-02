# Claims Backend

ASP.NET Core controller API with Application CQRS, EF Core/SQL Server Persistence, and Kafka Infrastructure. This is one backend process for the local assessment. The solution and all backend code, tests, migrations, scripts, and Docker Compose configuration are in this repository.

## Start locally

Use .NET SDK 10, SQL Server Express LocalDB on Windows, Docker Desktop with Linux containers for Kafka, and PowerShell. The API connects as your Windows user to `(localdb)\MSSQLLocalDB`; no SQL password is needed.

```powershell
.\scripts\Start-Backend.ps1
```

The script starts Kafka, applies EF migrations to the LocalDB `ChubbClaims` database, and runs the API on `http://localhost:5080`. LocalDB starts on demand. Swagger is at `http://localhost:5080/swagger` in Development. Use `X-Demo-User` with `claimant-alex`, `claimant-blair`, `officer-lee`, or `officer-sam` for local requests. Demo identities are impersonation, not production authentication.

## Verify

```powershell
.\scripts\Test-Backend.ps1 -WithLocalDb
.\scripts\Smoke-Workflow.ps1
```

The SQL test script creates and removes only uniquely named LocalDB test databases. The smoke script requires the running API and Kafka. More focused broker recovery and replay scripts are in `scripts/`. To use another SQL Server instance, override `ConnectionStrings__ClaimsDatabase` for the API and `CHUBB_TEST_SQL` for direct integration tests.

The previous Docker SQL Server data is not copied into LocalDB by this configuration change. Its Docker volume is preserved locally; when LocalDB starts successfully, the new database receives demo seed claims.

If LocalDB cannot start, inspect the Windows Application event log and `%LOCALAPPDATA%\Microsoft\Microsoft SQL Server Local DB\Instances\MSSQLLocalDB\error.log`. Do not delete the existing shared `MSSQLLocalDB` instance; it may contain other databases.

Claims support motor, property, and illustrative travel submissions; officer assignment and decisions; owner-scoped tracking; information exchange; multiple recorded offline settlement payments; and a workload/exposure/decision-activity summary. An approved claim's reserve decreases as offline payments are recorded and reaches zero on settlement. This application never transfers funds. Kafka carries decision notifications through a transactional outbox and idempotent consumer.
