# Financial Sentiment API

A .NET 10 Web API that automatically monitors financial news and performs AI-powered sentiment analysis on tracked stocks. Built to demonstrate Clean Architecture, Domain-Driven Design, CQRS, and AI integration patterns.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                          HTTP Client                                │
└──────────────────────────────┬──────────────────────────────────────┘
                               │
┌──────────────────────────────▼──────────────────────────────────────┐
│  API Layer  (ASP.NET Core)                                          │
│  SentimentController   ExceptionHandlingMiddleware   HealthChecks   │
└──────────────────────────────┬──────────────────────────────────────┘
                               │ ISender (MediatR)
┌──────────────────────────────▼──────────────────────────────────────┐
│  Application Layer  (Use Cases / CQRS)                              │
│  Commands: AnalyzeSentiment                                         │
│  Queries:  GetSentimentHistory  GetSentimentStats                   │
│  Pipeline: LoggingBehavior → ValidationBehavior → Handler           │
└──────────┬──────────────────────────────────────────┬───────────────┘
           │ IAiSentimentService                      │ ISentimentRepository
┌──────────▼──────────────┐              ┌────────────▼───────────────┐
│  Infrastructure (AI)    │              │  Infrastructure (DB)       │
│  AnthropicSentiment     │              │  AppDbContext (EF/SQLite)  │
│  Service | Mock         │              │  SentimentRepository       │
└─────────────────────────┘              └────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────────────┐
│  Infrastructure (Ingestion Pipeline)                                │
│  SentimentIngestionWorker  →  IArticleQueue  →  SentimentAnalysis   │
│  (polls news sources)          (Channel<T>)       Worker            │
└─────────────────────────────────────────────────────────────────────┘
           ↓ all layers depend on ↓
┌────────────────────────────────────────────────────────────────────┐
│  Domain Layer  (zero external dependencies)                        │
│  SentimentAnalysis (entity)   StockSymbol, SentimentScore (VOs)    │
│  SentimentLabel (enum)        IDomainEvent, DomainException        │
└────────────────────────────────────────────────────────────────────┘
```

**Dependency rule:** arrows point inward only. Domain has no external dependencies.

---

## API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/sentiment/analyze` | Manually submit text for analysis (dev/testing) |
| `GET`  | `/api/sentiment/{symbol}/history` | Paginated analysis history for a symbol |
| `GET`  | `/api/sentiment/{symbol}/stats` | Aggregated stats: avg score, trend, distribution |
| `GET`  | `/health/live` | Liveness probe |
| `GET`  | `/health/ready` | Readiness probe (checks DB) |

The primary flow is automatic: the ingestion worker polls Yahoo Finance RSS for tracked symbols and feeds articles through the pipeline without any user action.

---

## Setup

### Prerequisites
- .NET 10 SDK
- `dotnet-ef` global tool: `dotnet tool install --global dotnet-ef`

### 1. Clone and restore
```bash
git clone <repo>
cd FinancialSentimentAPI
dotnet restore API/API.csproj
```

### 2. Set API key (for real Anthropic analysis)
```bash
cd API
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."
```
The mock AI service is enabled by default (`"AI": { "Provider": "Mock" }` in appsettings.json). No API key needed for development.

### 3. Run migrations
```bash
cd ..   # solution root
dotnet ef database update --project Infrastructure --startup-project API
```

### 4. Run
```bash
dotnet run --project API
```

To switch to real Anthropic analysis, change `appsettings.json`:
```json
"AI": { "Provider": "Anthropic" }
```

---

## Project Structure

```
FinancialSentimentAPI/
├── Domain/                  ← zero external deps; entities, VOs, domain events
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Enums/
│   ├── Events/
│   ├── Exceptions/
│   └── Interfaces/
│
├── Application/             ← use cases; MediatR + FluentValidation
│   ├── Features/Sentiment/
│   │   ├── Commands/AnalyzeSentiment/
│   │   └── Queries/GetSentimentHistory | GetSentimentStats/
│   ├── Behaviors/
│   ├── Services/            ← IAiSentimentService, IArticleQueue
│   └── Exceptions/
│
├── Infrastructure/          ← EF Core, Anthropic API, ingestion pipeline
│   ├── Persistence/
│   ├── Services/            ← AnthropicSentimentService, MockSentimentService
│   ├── Ingestion/           ← BackgroundService workers, RSS feed, Channel queue
│   └── Monitoring/          ← Health checks
│
├── API/                     ← composition root; controllers, middleware
│   ├── Controllers/
│   └── Middleware/
│
└── Tests/                   ← xUnit; Domain + Application unit tests
    ├── Domain/
    └── Application/
```

---

## Key Design Decisions

| Decision | Rationale |
|----------|-----------|
| Clean Architecture | Dependencies point inward; business logic has no infrastructure coupling |
| CQRS via MediatR | Commands change state; queries never do. Handlers are self-contained use cases |
| Domain events on entity | Entity raises events on `Create()`; Application dispatches after persistence |
| `IArticleQueue` interface | In-memory `Channel<T>` now; swap to GCP Pub/Sub with one DI change |
| `ITrackedSymbolsProvider` | Config file now; swap to DB-backed admin endpoint with one DI change |
| Label derived by domain | AI returns raw score; domain decides what score means (configurable thresholds) |
| `AI:Provider` string config | Extensible provider switching: Mock → Anthropic → OpenAI without code changes |
| Value comparers on EF lists | Ensures EF Core detects changes to JSON-stored collections |

---

## Future Roadmap

- `Alert` aggregate — notify when sentiment crosses a threshold
- `Watchlist` aggregate — user-managed list of tracked symbols
- `TrackedSymbol` aggregate — DB-backed symbol management with admin endpoint
- Top 10 trending — symbols with largest sentiment shift in 24h
- GCP deployment — Cloud Run + Cloud Pub/Sub + Cloud SQL
- OpenTelemetry — structured traces exported to GCP Cloud Trace
