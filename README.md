# Claims Backend

ASP.NET Core controller API with Application CQRS, EF Core/SQL Server Persistence, and Kafka Infrastructure. This is one backend process for the local assessment. The solution and all backend code, tests, migrations, scripts, and Docker Compose configuration are in this repository.

## Start locally

Use .NET SDK 10, Docker Desktop with Linux containers, and PowerShell. Create a local `.env` from `.env.example` and replace the sample SQL password. `.env` is ignored by Git.

```powershell
Copy-Item .env.example .env
.\scripts\Start-Backend.ps1
```

The script starts SQL Server and Kafka, applies EF migrations, and runs the API on `http://localhost:5080`. Swagger is at `http://localhost:5080/swagger` in Development. Use `X-Demo-User` with `claimant-alex`, `claimant-blair`, `officer-lee`, or `officer-sam` for local requests. Demo identities are impersonation, not production authentication.

## Verify

```powershell
.\scripts\Test-Backend.ps1 -WithSqlServer
.\scripts\Smoke-Workflow.ps1
```

The SQL test script creates and removes only uniquely named test databases. The smoke script requires the running API and Kafka. More focused broker recovery and replay scripts are in `scripts/`.

Claims support motor, property, and illustrative travel submissions; officer assignment and decisions; owner-scoped tracking; information exchange; multiple recorded offline settlement payments; and a workload/exposure/decision-activity summary. An approved claim's reserve decreases as offline payments are recorded and reaches zero on settlement. This application never transfers funds. Kafka carries decision notifications through a transactional outbox and idempotent consumer.
