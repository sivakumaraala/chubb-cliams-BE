# API Verification

Run from the repository root:

```powershell
dotnet test tests/Chubb.Claims.Api.Tests/Chubb.Claims.Api.Tests.csproj
```

The metadata, authentication/role, and invalid-input tests run without a database. They disable startup migrations and Kafka workers. Their success is not evidence that SQL Server or Kafka works.

Set `CHUBB_TEST_SQL` to a local SQL Server connection string before running to enable the real database tests. Use an account allowed to create and drop test databases. Keep credentials in an environment variable rather than committed files or command output.

The fixture replaces the supplied database name with a unique `ChubbClaimsApiTests_<guid>` name. It migrates that isolated database and deletes only that exact generated name during cleanup. If the variable is absent, SQL tests are explicitly reported as skipped.

The SQL tests exercise the HTTP claim workflow, ownership, competing assignments, stale updates, actual SQL rowversion/transaction rollback, decision/outbox persistence, and repeated notification-handler processing. Kafka workers remain disabled: broker publication, consumption, outage recovery, and offset handling require a separate live Kafka demonstration.
