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
│  Queries:  GetSentimentHistory  GetSentimentStats  GetTrendingSymbols│
│  Pipeline: LoggingBehavior → ValidationBehavior → Handler           │
└──────────┬──────────────────────────────────────────┬───────────────┘
           │ IAiSentimentService                      │ ISentimentRepository
┌──────────▼──────────────┐              ┌────────────▼───────────────┐
│  Infrastructure (AI)    │              │  Infrastructure (DB)       │
│  AnthropicSentiment     │              │  AppDbContext (EF/Postgres)│
│  OllamaSentiment | Mock │              │  SentimentRepository       │
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
| `POST` | `/api/sentiment/analyze` | Manually submit text for sentiment analysis. Rate-limited: 10 req/min per IP. |
| `GET`  | `/api/sentiment/trending` | Top symbols by sentiment score shift in a rolling window (`?hours=24&limit=10`) |
| `GET`  | `/api/sentiment/{symbol}/history` | Paginated analysis history for a symbol |
| `GET`  | `/api/sentiment/{symbol}/stats` | Aggregated stats: avg score, trend, distribution |
| `GET`  | `/health/live` | Liveness probe |
| `GET`  | `/health/ready` | Readiness probe (checks DB) |

The primary flow is **automatic**: `SentimentIngestionWorker` polls Yahoo Finance RSS every 5 minutes for all tracked symbols and feeds articles through the AI analysis pipeline without any user action. The `/trending` endpoint reflects this live data.

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

### 2. Configure AI provider

The mock AI service is enabled by default — no setup needed for development.

**Ollama (recommended, free, self-hosted):**
```bash
# In .env or appsettings.Development.json
AI__Provider=Ollama
Ollama__BaseUrl=http://your-ollama-host:11434
Ollama__Model=llama3
```

**Anthropic:**
```bash
cd API
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."
dotnet user-secrets set "AI:Provider" "Anthropic"
```

### 3. Run with Docker (recommended)
```bash
cp .env.example .env   # fill in DB_PASSWORD, AI_PROVIDER, OLLAMA_BASE_URL
docker compose up -d
```

### 4. Run locally
```bash
dotnet ef database update --project Infrastructure --startup-project API
dotnet run --project API
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
│   │   └── Queries/GetSentimentHistory | GetSentimentStats | GetTrendingSymbols/
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

## Documentation

- [Scoring Model](docs/scoring-model.md) — how sentiment scores are computed (decay weighting, trend regression, dispersion, signal strength)
- [Decision Log](docs/decision-log.md) — architectural decisions and past incidents

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
| `AI:Provider` string config | Extensible provider switching: Mock → Anthropic → Ollama without code changes |
| Value comparers on EF lists | Ensures EF Core detects changes to JSON-stored collections |

---

## Ingestion Pipeline

The system automatically populates data without manual API calls:

```
Yahoo Finance RSS
        ↓  (every 5 min per symbol)
SentimentIngestionWorker
        ↓  deduplicates by URL hash
IArticleQueue (Channel<T>)
        ↓  up to 3 concurrent
SentimentAnalysisWorker
        ↓  dispatches via MediatR
AnalyzeSentimentCommand → AI Provider → PostgreSQL
```

**Tracked symbols** (configured in `appsettings.json` / `docker-compose.yml`):
AAPL, MSFT, GOOGL, TSLA, NVDA, AMZN, META, NFLX, AMD, INTC, JPM, BAC, SPY, QQQ, BTC-USD

**To add symbols**: Edit `Ingestion:TrackedSymbols` in `appsettings.json` or set `Ingestion__TrackedSymbols__N` env vars — no restart required (uses `IOptionsMonitor`).

---

## Future Roadmap

- Additional news sources (Google News RSS, Reddit) via `CompositeNewsSourceService`
- DB-backed symbol management with admin API (`POST /api/symbols`)
- On-demand backfill endpoint (`POST /api/sentiment/backfill?symbol=AAPL&hours=48`)
- `Alert` aggregate — notify when sentiment crosses a threshold
- GCP deployment — Cloud Run + Cloud Pub/Sub + Cloud SQL
- OpenTelemetry — structured traces exported to GCP Cloud Trace
