# QA .NET Workflows

UI and API automation for Support Desk, a local ticket application backed by PostgreSQL.
Tests use C#, .NET 10, NUnit and HttpClient.

## Structure

- `app/SupportDesk` — HTTP application and persistence.
- `tests/SupportDesk.Tests` — API tests, clients, models, fixtures and test data.
- `database/schema.sql` — PostgreSQL schema.

## Run

Requires .NET 10 SDK and Docker Compose. Copy `.env.example` to `.env` and set a local password.

```powershell
docker compose up -d --wait db
$env:ConnectionStrings__SupportDesk = 'Host=localhost;Port=5438;Database=supportdesk;Username=supportdesk;Password=<local-password>'
dotnet run --project app/SupportDesk --urls http://localhost:5088
```

In another terminal:

```powershell
$env:QA_DatabaseConnection = 'Host=localhost;Port=5438;Database=supportdesk;Username=supportdesk;Password=<local-password>'
dotnet test
dotnet format --verify-no-changes
```

`QA_BaseUrl` and `QA_ApiBaseUrl` override `testsettings.json`. Each test removes its own tickets.

The API accepts priorities `Low`, `Normal`, `High`. Titles contain 1–120 characters.
Allowed status changes are `Open → InProgress → Resolved`; other transitions return `409`.
Invalid input returns `400` with field errors and must not create a database row.
