# QA .NET Workflows

C# automation for Support Desk: prepare tickets through API, act in the browser,
verify saved state through API and PostgreSQL. The suite has 18 scenarios:
7 API, 3 UI and 8 mixed workflows. Chromium is the default browser; tests run sequentially without retries.

## Stack and structure

.NET 10, NUnit, Microsoft Playwright for .NET, HttpClient, PostgreSQL 17,
Npgsql, Allure, Docker Compose, GitHub Actions and GitLab CI.

| Path                                                               | Contents                                                        |
| ------------------------------------------------------------------ | --------------------------------------------------------------- |
| `app/SupportDesk/`                                                 | ASP.NET Core API, SQL persistence and a small browser interface |
| `tests/SupportDesk.Tests/Api/`, `Ui/`, `Workflows/`                | API contracts, browser behaviour and cross-layer scenarios      |
| `tests/SupportDesk.Tests/Pages/`, `Clients/`, `Models/`            | Page Objects, HttpClient wrapper and independent test models    |
| `tests/SupportDesk.Tests/Fixtures/`, `TestData/`, `Configuration/` | Lifecycle, unique data, JSON cases and settings                 |
| `tests/SupportDesk.Tests/Database/`, `Reporting/`                  | SQL assertions and diagnostic attachments                       |
| `database/`, `docker/`, `scripts/`                                 | Schema, test image and execution commands                       |

Titles contain 1–120 characters; priorities are `Low`, `Normal`, `High`.
Status changes follow `Open → InProgress → Resolved`; invalid transitions return `409`.
Invalid input returns `400` with field errors and must not insert a row.
Ticket search checks titles and descriptions. Search, priority and status filters are
case-insensitive and ignore surrounding spaces. The API can order matching tickets by
priority, placing urgent work first.
Tests own their data and remove it in teardown, including after failures.

## Run in Docker

Requires Docker with Compose. Copy `.env.example` to `.env` and replace the password.

```powershell
pwsh scripts/check.ps1
```

Linux/macOS: `sh scripts/check.sh`. The command builds the images, checks formatting,
runs all tests, saves `artifacts/`, then removes this project's containers and disposable database.
It replaces previous artifacts. No external application or account is needed by the tests.

## Run tests locally

Requires SDK **10.0.401** (or a newer patch in the same feature band) and PowerShell 7.

```powershell
docker compose up -d --build --wait app
dotnet restore --locked-mode
dotnet build
pwsh tests/SupportDesk.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
$env:QA_DatabaseConnection = 'Host=localhost;Port=5438;Database=supportdesk;Username=supportdesk;Password=<local-password>'
dotnet test --settings tests/SupportDesk.Tests/test.runsettings
```

Append `--filter TestCategory=Api`, `--filter TestCategory=Ui`,
`--filter TestCategory=Workflow` or `--filter TestCategory=Smoke` for a subset.
For a visible browser append `-- Playwright.LaunchOptions.Headless=false`.
The application is available at `http://localhost:5088`.

## Configuration

`testsettings.json` supplies the local URL; `QA_` environment variables take precedence.

| Variable                                   | Default / purpose                                            |
| ------------------------------------------ | ------------------------------------------------------------ |
| `QA_BaseUrl`                               | `http://localhost:5088`                                      |
| `QA_ApiBaseUrl`                            | Same as `QA_BaseUrl`                                         |
| `QA_DatabaseConnection`                    | Required by SQL assertions; provided automatically in Docker |
| `QA_ActionTimeoutMs`, `QA_ExpectTimeoutMs` | `10000`, `5000`                                              |
| `QA_ArtifactsDirectory`                    | Repository `artifacts/`; screenshot and trace destination    |
| `POSTGRES_PASSWORD`                        | Local `.env` or environment; CI generates an ephemeral value |

Browser options are in `test.runsettings`. Allure's destination is in `allureConfig.json`;
`ALLURE_CONFIG` can select another configuration. Keep credentials out of tracked settings.

## Reporting and checks

Docker produces Allure HTML/results, TRX and service logs under `artifacts/`.
Failed UI tests attach a screenshot and trace; API attachments include method, path,
status and synthetic payloads without headers. Use synthetic data, since bodies and traces contain application content.
NUnit assertions appear as Failed in Allure; Playwright exceptions appear as Broken. Both fail the run.

For local report generation and web/config formatting, install Node.js 22+:

```powershell
npm.cmd ci
npm.cmd run report
npm.cmd run report:open
dotnet format --verify-no-changes
npm.cmd run format:check
```

Direct `dotnet test` runs append Allure results; clear `artifacts/allure-results` between independent runs.
The Docker check starts with fresh results. Reports, browser downloads, build output and `.env` are ignored.

## CI and publication

GitHub Actions runs on pushes to `main`, pull requests and manual dispatch.
GitLab CI runs on the default branch and merge requests; its runner must support privileged Docker-in-Docker.
Both run `scripts/check.sh`, fail on check/test errors and retain diagnostics for 14 days, including failed runs.

`origin` points to GitHub and `gitlab` to GitLab. From a clean `main`,
`pwsh scripts/push.ps1` pushes the same commit to both and verifies their SHA values.
A partial push fails explicitly and can be retried without rewriting history.
