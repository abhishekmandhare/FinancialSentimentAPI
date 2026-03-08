# CLAUDE.md — Project Instructions for Claude Code

## Project

Financial Sentiment API — .NET 10, Clean Architecture, CQRS via MediatR, EF Core + Postgres.

Solution file: `API/API.slnx` (5 projects: Domain, Application, Infrastructure, API, Tests)

## Build & Test

```bash
dotnet build API/API.csproj                    # builds all layers
dotnet test Tests/Tests.csproj                 # all tests must pass before committing
dotnet ef migrations add <Name> --project Infrastructure --startup-project API
dotnet ef database update --project Infrastructure --startup-project API
```

gh CLI is at `/c/Program Files/GitHub CLI/gh.exe` — add to PATH if `gh` is not found:
```bash
export PATH="$PATH:/c/Program Files/GitHub CLI"
```

## Git Workflow

- **NEVER commit directly to `main`** — always create a feature branch and open a PR.
- Branch naming: `feature/<short-description>`, `fix/<short-description>`, `chore/<short-description>`
- **Pre-commit checklist (MANDATORY — do ALL of these before every commit):**
  1. `dotnet build API/API.csproj` — must compile with zero errors.
  2. `dotnet test Tests/Tests.csproj` — ALL tests must pass. Do not commit if any test fails.
  3. Manually verify the changed functionality works as intended (run the app, hit the endpoint, check the output). A passing build is not enough — confirm the feature/fix actually works.
  4. If you wrote new code, write or update tests to cover it. Do not commit untested code.
- Keep commits focused — one logical change per commit.
- Use `gh pr create` to open PRs. Include a summary and test plan.

## Architecture Rules

Dependency rule — **arrows point inward only**:

```
API → Application → Domain
API → Infrastructure → Application → Domain
```

- **Domain** has ZERO external package dependencies. No MediatR, no EF Core, no ASP.NET.
- **Application** depends on Domain only. Defines interfaces (e.g. `IAiSentimentService`, `ISentimentRepository`).
- **Infrastructure** implements those interfaces. Owns EF Core, HTTP clients, background workers.
- **API** is the composition root. Thin controllers — no business logic, just map HTTP ↔ MediatR.

Do not add cross-layer references that violate this rule.

## Code Conventions

- Use `record` for DTOs, commands, queries, and value objects.
- Value objects validate in the constructor — invalid state is unrepresentable.
- Entity creation via static `Create()` factory methods, not public constructors.
- One class per file. File name must match class name.
- No `// TODO` without a linked issue. Prefer fixing now over deferring.

## Security & Sanitisation

- **No credentials in committed files.** Connection strings, API keys, and passwords go in:
  - `appsettings.Development.json` (gitignored) for local dev
  - Environment variables / `.env` (gitignored) for Docker / production
  - Never in `appsettings.json` or `appsettings.Production.json`
- Validate all user input at the API boundary (FluentValidation on commands).
- Use parameterised queries only — EF Core handles this, but never interpolate user input into raw SQL.
- Do not log sensitive data (API keys, passwords, PII). Mask or omit from structured logs.
- Do not disable HTTPS redirection or authentication middleware.
- Do not use `[AllowAnonymous]` without explicit justification.
- Sanitise any user-provided text before passing to external APIs (AI service).
- Return `ProblemDetails` (RFC 7807) for all error responses — never leak stack traces in production.

## Environment Configuration

| File | Committed | Purpose |
|------|-----------|---------|
| `appsettings.json` | Yes | Base config, no credentials |
| `appsettings.Development.json` | No | Local Postgres + verbose logging |
| `appsettings.Testing.json` | No | Test DB + Mock AI + ingestion off |
| `appsettings.Production.json` | Yes | Warning-level logging only |
| `.env` | No | Docker secrets (DB_PASSWORD, ANTHROPIC_API_KEY) |

New developers: copy `*.example` files and fill in local values.

## Testing

- Domain and Application layers must have unit tests.
- Use `FluentAssertions` for assertions, `NSubstitute` for mocks.
- Test naming: `MethodName_Scenario_ExpectedResult` (e.g. `Create_WithValidInputs_RaisesCreatedEvent`).
- Do not skip or comment out failing tests — fix them.

## EF Core / Database

- Postgres 17 in Docker (`docker-compose.yml`).
- Always generate a migration when changing entity configuration — do not rely on auto-migration alone.
- Migration names should be descriptive: `AddAlertEntity`, not `Update1`.
- The composite index `IX_SentimentAnalyses_Symbol_AnalyzedAt` is performance-critical — do not remove it.

## Docker

```bash
docker compose build          # build image locally
docker compose up -d          # start api + postgres
docker compose logs -f api    # tail logs
```

Image: `financial-sentiment-api:latest`. Future: push to GCR/ECR.

## Known Issues & Decision Log

Before debugging any issue, check `docs/decision-log.md` for prior incidents and solutions.
This log contains root cause analyses, deployment gotchas, and architectural decisions.
