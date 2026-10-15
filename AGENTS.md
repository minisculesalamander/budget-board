# AGENTS.md

This repository is a personal fork of [teelur/budget-board](https://github.com/teelur/budget-board) for self-hosting and later custom features. Upstream product docs live at [budgetboard.net](https://budgetboard.net/). Fork-only notes are this file, [SELF_HOSTING.md](SELF_HOSTING.md), and [ROADMAP.md](ROADMAP.md).

Do not implement roadmap items unless the task asks for them.

## Architecture

The app is a React single-page client and an ASP.NET Core API, with PostgreSQL as the only required datastore.

| Path | Role |
| --- | --- |
| `client/` | React 19, TypeScript, Vite 8. UI kit `@teelur/budget-board-ui`, Mantine, TanStack Query, React Router, i18next. |
| `server/BudgetBoard.WebAPI` | ASP.NET Core `net10.0` host. Cookie identity, controllers, Quartz jobs, composition in `Program.cs`. |
| `server/BudgetBoard.Service` | Business logic. Request and response types live in `Models/`, interfaces in `Interfaces/`. |
| `server/BudgetBoard.Database` | EF Core models, migrations, and `UserDataContext`. Provider is Npgsql. |
| `server/BudgetBoard.Tests` | xUnit project. Namespace is `BudgetBoard.IntegrationTests`. |
| `compose.yml` | Self-host stack: nginx client, API, Postgres 16. See SELF_HOSTING.md. |

The API does not use a single connection-string setting. `Program.cs` reads `CLIENT_ADDRESS`, `POSTGRES_HOST`, `POSTGRES_DATABASE`, `POSTGRES_USER`, optional `POSTGRES_PASSWORD`, and `POSTGRES_PORT` (default `5432`). `AUTO_UPDATE_DB=true` applies EF migrations during startup. Health is `GET /health`. Swagger is enabled only when `ASPNETCORE_ENVIRONMENT=Development`.

In Vite dev (`yarn dev` on port 5173), the browser calls `http://${VITE_BUDGET_BOARD_DOMAIN}`. Point that at the API, normally `localhost:5293` (`launchSettings.json` profile `http`). The production client is nginx and proxies `/api/` to the API, so the built app uses relative URLs.

Tests build an EF Core InMemory database in `TestHelper`. They do not need Postgres. The project name says integration tests; they are in-process service tests.

## Cloud agent environment

`.cursor/environment.json` runs:

- `bash .cursor/install.sh` — .NET 10 SDK under `/usr/local/share/dotnet`, PostgreSQL packages, Yarn 4.18.0 via Corepack, `dotnet restore`, `yarn install --immutable`. Writes `/etc/profile.d/budget-board-dev.sh` and, if missing, `client/.env.development` (gitignored).
- `bash .cursor/start.sh` — starts PostgreSQL and ensures the local database exists.

Local-only defaults, for the agent VM, not for a host you expose:

| Variable | Value |
| --- | --- |
| `CLIENT_ADDRESS` | `http://localhost:5173` |
| `POSTGRES_HOST` | `localhost` |
| `POSTGRES_PORT` | `5432` |
| `POSTGRES_DATABASE` | `budgetboard` |
| `POSTGRES_USER` | `budgetboard` |
| `POSTGRES_PASSWORD` | `budgetboard` |
| `AUTO_UPDATE_DB` | `true` |
| `VITE_BUDGET_BOARD_DOMAIN` | `localhost:5293` |

`bash -lc` loads the profile snippet. A shell that does not can `source /etc/profile.d/budget-board-dev.sh`.

## Build, test, and run

Backend, from `server/`:

```bash
dotnet build BudgetBoard.sln
dotnet test BudgetBoard.sln
```

Frontend, from `client/`:

```bash
yarn install --immutable
yarn build
yarn lint
```

There is no `yarn test` script.

`yarn lint` currently crashes before it reports file diagnostics. ESLint 10 loads `ts-api-utils`, which reads `typescript.TypeFlags`, and that export is missing under the pinned TypeScript 7.0.2. The failure reproduces on Node.js 22.14 and 22.23. `yarn build` still runs `tsc -b` and Vite successfully. Treat a lint crash of this shape as an upstream incompatibility, not as a regression from an unrelated edit. `dotnet test` also prints NuGet warnings NU1902 and NU1903 for `System.Security.Cryptography.Pkcs` 10.0.0; the suite still passes.

Dev servers, after `bash .cursor/start.sh` and with the variables above exported:

```bash
cd server/BudgetBoard.WebAPI && dotnet run --launch-profile http
cd client && yarn dev
```

API: `http://localhost:5293`. Client: `http://localhost:5173`. Register at the welcome screen, or `POST /api/register` with `email` and `password` (minimum length 3; email confirmation is off unless SMTP is configured).

Self-hosting with Docker Compose is a separate path. Use SELF_HOSTING.md. Do not point Compose at the agent database password above; `compose.env` has its own placeholder. `.cursor/start.sh` starts local PostgreSQL only. It does not start Docker.

## Conventions

C#:

- `net10.0`, nullable reference types, implicit usings, file-scoped namespaces, 4-space indent.
- Controllers stay thin: primary constructors, `[Authorize]`, `HandleRequestAsync`, and the user id from `UserManager`.
- Services own behavior. Entity types carry XML documentation.
- Schema changes go through EF migrations (`dotnet ef` against `BudgetBoard.Database`). Do not hand-edit `UserDataContextModelSnapshot.cs` apart from what the tool generates.
- Tests use `[Fact]`, Arrange/Act/Assert comments, FluentAssertions, Moq, and `TestHelper.CreateMockLocalizer`.

TypeScript:

- Match the surrounding file: 2-space indent, double quotes, semicolons.
- Import app code through the `~` alias (`client/src`).
- Pages live in `client/src/app/Authorized/PageContent/<Feature>`.
- Server calls live in `client/src/hooks`.
- Use `@teelur/budget-board-ui` where the page you are editing already does.
- User-visible strings go through i18next. Add keys to `client/public/locales/en-us/translation.json`. `yarn format:locales` rewrites locale JSON.
- ESLint is strict (`curly`, hooks, jsx-a11y). There is no frontend unit-test runner.

## Where features live

| Area | Server | Client |
| --- | --- | --- |
| Accounts and institutions | `AccountService`, `InstitutionService`, matching controllers | `PageContent/Accounts` |
| Assets | `AssetService`, `ValueService` | `PageContent/Assets` |
| Transactions, import, tags | `TransactionService`, `TransactionImportService` | `PageContent/Transactions` |
| Budgets | `BudgetService`. One limit per category per month, optional `RolloverStartMonth` | `PageContent/Budgets` |
| Goals | `GoalService` | `PageContent/Goals` |
| Trends | read models on the existing services | `PageContent/Trends`, `components/Charts` |
| Recurring rules | `RecurringRuleService`. Matches transactions that already exist | Transactions settings, route `recurring` |
| Automatic rules | `AutomaticRuleService` and helpers under `Service/Helpers` | Transactions settings, route `rules` |
| Auto-categorization | `AutomaticTransactionCategorizerService` (ML.NET) | route `auto-categorizer` |
| SimpleFIN and LunchFlow | `SimpleFin*`, `LunchFlow*`, `SyncService`, Quartz `SyncBackgroundJob` | `PageContent` external accounts |
| Auth | Identity overrides, `OidcController` | `app/Unauthorized`, `providers/AuthProvider` |
| Large objects | `ILargeObjectStore` on the DbContext | unused by the UI |

## Staying mergeable with upstream

Add the upstream remote once:

```bash
git remote add upstream https://github.com/teelur/budget-board.git
git fetch upstream
```

`origin` is this fork. Rebase fork work onto `upstream/main` rather than rewriting shared files to match a one-off style.

- Prefer a new service, controller, page, and migration over editing an existing one.
- When `Program.cs`, `PageContent.tsx`, or `UserDataContext` must change, keep the hunk limited to registration or routing.
- Do not reformat untouched code, rename public API routes, or upgrade NuGet or Yarn packages unless that is the task.
- Leave Docker image tags, Identity options, and the OIDC flow alone when the change is unrelated.
- Fork-only docs stay at the repo root and under `.cursor/`, where upstream has no counterparts.
- `compose.env` is upstream's tracked placeholder. Replace `POSTGRES_PASSWORD` before any non-local use, and do not commit a real password.
