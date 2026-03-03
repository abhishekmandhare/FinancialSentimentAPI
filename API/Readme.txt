Financial Sentiment API — Plan & System Design                                                                           
 Context

 Starting from a .NET 10 Web API template (WeatherForecast placeholder) + a partial Domain project.
 Goal: build a real Financial Sentiment API that teaches Clean Architecture, EF Core, and AI integration through hands-on
  code.

 ---
 System Design

 ┌─────────────────────────────────────────────────────────────────┐
 │                          HTTP Client                            │
 └──────────────────────────────┬──────────────────────────────────┘
                                │
 ┌──────────────────────────────▼──────────────────────────────────┐
 │  API Layer  (ASP.NET Core)                                      │
 │  ┌─────────────────────┐   ┌───────────────────────────────┐   │
 │  │ SentimentController │   │ ExceptionHandlingMiddleware   │   │
 │  │  POST /analyze      │   │ (ProblemDetails / RFC 7807)   │   │
 │  │  GET /{sym}/history │   └───────────────────────────────┘   │
 │  │  GET /{sym}/stats   │                                        │
 │  └──────────┬──────────┘                                        │
 └─────────────┼───────────────────────────────────────────────────┘
               │ ISender (MediatR)
 ┌─────────────▼───────────────────────────────────────────────────┐
 │  Application Layer  (Use Cases)                                 │
 │                                                                 │
 │  Commands                      Queries                          │
 │  ┌───────────────────────┐    ┌──────────────────────────────┐  │
 │  │ AnalyzeSentiment      │    │ GetSentimentHistory          │  │
 │  │  Command + Validator  │    │ GetSentimentStats            │  │
 │  │  + Handler            │    │  (each: Query + Handler)     │  │
 │  └───────────────────────┘    └──────────────────────────────┘  │
 │                                                                 │
 │  MediatR Pipeline Behaviors (cross-cutting):                    │
 │    ValidationBehavior → LoggingBehavior → Handler               │
 │                                                                 │
 │  Interfaces defined here (implemented in Infrastructure):       │
 │    IAiSentimentService   ISentimentRepository                   │
 └─────────┬──────────────────────────────────────┬───────────────┘
           │                                      │
           ▼ implements                           ▼ implements
 ┌─────────────────────────┐    ┌────────────────────────────────┐
 │  Infrastructure Layer   │    │  Infrastructure Layer          │
 │  (AI)                   │    │  (Persistence)                 │
 │                         │    │                                │
 │  AnthropicSentiment     │    │  AppDbContext (EF Core/SQLite) │
 │  Service (Typed HTTP)   │    │  SentimentRepository           │
 │   OR                    │    │  SentimentAnalysis             │
 │  MockSentimentService   │    │    Configuration (owned VOs)   │
 │  (switchable via config)│    │                                │
 └─────────────────────────┘    └────────────────────────────────┘
           │                                      │
           └──────────────┬───────────────────────┘
                          ▼
 ┌────────────────────────────────────────────────────────────────┐
 │  Domain Layer  (zero external dependencies)                    │
 │                                                                │
 │  Entities           Value Objects       Interfaces             │
 │  SentimentAnalysis  StockSymbol         ISentimentRepository   │
 │   .Create(...)      SentimentScore                             │
 │                                                                │
 │  Enums              Exceptions                                 │
 │  SentimentLabel     DomainException                            │
 │  (Positive/         (base for all domain rule violations)      │
 │   Negative/Neutral)                                            │
 └────────────────────────────────────────────────────────────────┘

 Dependency Rule: Arrows point inward only. Domain knows nothing. Application knows Domain. Infrastructure knows Domain +
  Application. API knows Application + Infrastructure (composition root only).

 ---
 What the API Does

 ┌─────────────────────────────────────┬─────────────────────────────────────────────────────────────────┐
 │              Endpoint               │                           Description                           │
 ├─────────────────────────────────────┼─────────────────────────────────────────────────────────────────┤
 │ POST /api/sentiment/analyze         │ Submit text + stock symbol → AI analysis → persisted + returned │
 ├─────────────────────────────────────┼─────────────────────────────────────────────────────────────────┤
 │ GET /api/sentiment/{symbol}/history │ Paginated history of analyses for a symbol                      │
 ├─────────────────────────────────────┼─────────────────────────────────────────────────────────────────┤
 │ GET /api/sentiment/{symbol}/stats   │ Aggregated stats: avg score, pos/neg/neutral %, trend           │
 └─────────────────────────────────────┴─────────────────────────────────────────────────────────────────┘

 ---
 Project Structure

 FinancialSentimentAPI/
 ├── Domain/          ← fix existing
 ├── Application/     ← new project
 ├── Infrastructure/  ← new project
 └── API/             ← replace template, wire up

 Project references:
 - Application.csproj → Domain
 - Infrastructure.csproj → Domain, Application
 - API.csproj → Application, Infrastructure

 ---
 Key Files

 Domain (fix existing)

 ┌─────────────────────────────────────────┬──────────────────────────────────────────────────┬──────────────────────┐
 │                  File                   │                      Action                      │       Teaches        │
 ├─────────────────────────────────────────┼──────────────────────────────────────────────────┼──────────────────────┤
 │                                         │ Fix: wrong exception type (ArgumentNullException │ Primitive obsession; │
 │ Domain/ValueObjects/StockSymbol.cs      │  → ArgumentException), remove unused usings, fix │  fail-fast           │
 │                                         │  typo                                            │ validation           │
 ├─────────────────────────────────────────┼──────────────────────────────────────────────────┼──────────────────────┤
 │                                         │ Rewrite: syntax error, add range validation      │ Value object         │
 │ Domain/ValueObjects/SentimentScore.cs   │ (-1.0→1.0), factory methods                      │ invariants; factory  │
 │                                         │                                                  │ methods              │
 ├─────────────────────────────────────────┼──────────────────────────────────────────────────┼──────────────────────┤
 │ Domain/Exceptions/DomainException.cs.cs │ Rename to .cs; add (string, Exception)           │ Typed exceptions     │
 │                                         │ constructor                                      │ communicate intent   │
 ├─────────────────────────────────────────┼──────────────────────────────────────────────────┼──────────────────────┤
 │ Domain/Class1.cs                        │ Delete                                           │ —                    │
 └─────────────────────────────────────────┴──────────────────────────────────────────────────┴──────────────────────┘

 Domain (new files)

 ┌───────────────────────────────────────────┬────────────────────────────────────────────────────────────────────────┐
 │                   File                    │                                Teaches                                 │
 ├───────────────────────────────────────────┼────────────────────────────────────────────────────────────────────────┤
 │ Domain/Enums/SentimentLabel.cs            │ Enums for closed business concepts                                     │
 ├───────────────────────────────────────────┼────────────────────────────────────────────────────────────────────────┤
 │ Domain/Entities/SentimentAnalysis.cs      │ Rich domain model; private constructor + static Create() factory;      │
 │                                           │ entities have identity                                                 │
 ├───────────────────────────────────────────┼────────────────────────────────────────────────────────────────────────┤
 │ Domain/Interfaces/ISentimentRepository.cs │ Domain defines what it needs; infrastructure decides how               │
 └───────────────────────────────────────────┴────────────────────────────────────────────────────────────────────────┘

 Application (new project)

 File: Application/Application.csproj
 Teaches: MediatR, FluentValidation NuGet packages
 ────────────────────────────────────────
 File: Application/DependencyInjection.cs
 Teaches: Each layer owns its DI registration
 ────────────────────────────────────────
 File: Application/Services/IAiSentimentService.cs
 Teaches: Depend on abstractions, not implementations
 ────────────────────────────────────────
 File: Application/Exceptions/ValidationException.cs
 Teaches: Different exception types → different HTTP status codes
 ────────────────────────────────────────
 File: Application/Exceptions/NotFoundException.cs
 Teaches: —
 ────────────────────────────────────────
 File: Application/Features/Sentiment/Commands/AnalyzeSentiment/AnalyzeSentimentCommand.cs
 Teaches: CQRS: commands express intent; records as immutable DTOs
 ────────────────────────────────────────
 File: Application/Features/Sentiment/Commands/AnalyzeSentiment/AnalyzeSentimentCommandValidator.cs
 Teaches: FluentValidation; input validation at boundary
 ────────────────────────────────────────
 File: Application/Features/Sentiment/Commands/AnalyzeSentiment/AnalyzeSentimentCommandHandler.cs
 Teaches: Use case: orchestrates domain + infrastructure via interfaces
 ────────────────────────────────────────
 File: Application/Features/Sentiment/Queries/GetSentimentHistory/...
 Teaches: CQRS: queries never change state
 ────────────────────────────────────────
 File: Application/Features/Sentiment/Queries/GetSentimentStats/...
 Teaches: Aggregation at application layer
 ────────────────────────────────────────
 File: Application/Behaviors/ValidationBehavior.cs
 Teaches: Open/Closed Principle; cross-cutting concerns in pipeline
 ────────────────────────────────────────
 File: Application/Behaviors/LoggingBehavior.cs
 Teaches: Structured logging without repetition

 Infrastructure (new project)

 ┌─────────────────────────────────────────────────────────────────────────────┬──────────────────────────────────────┐
 │                                    File                                     │               Teaches                │
 ├─────────────────────────────────────────────────────────────────────────────┼──────────────────────────────────────┤
 │ Infrastructure/Infrastructure.csproj                                        │ EF Core SQLite, EF Design tools      │
 ├─────────────────────────────────────────────────────────────────────────────┼──────────────────────────────────────┤
 │ Infrastructure/DependencyInjection.cs                                       │ Options pattern; conditional         │
 │                                                                             │ registration (mock vs real AI)       │
 ├─────────────────────────────────────────────────────────────────────────────┼──────────────────────────────────────┤
 │ Infrastructure/Persistence/AppDbContext.cs                                  │ DbContext = Unit of Work;            │
 │                                                                             │ ApplyConfigurationsFromAssembly      │
 ├─────────────────────────────────────────────────────────────────────────────┼──────────────────────────────────────┤
 │                                                                             │ IEntityTypeConfiguration<T>;         │
 │ Infrastructure/Persistence/Configurations/SentimentAnalysisConfiguration.cs │ OwnsOne() for value objects as       │
 │                                                                             │ columns                              │
 ├─────────────────────────────────────────────────────────────────────────────┼──────────────────────────────────────┤
 │ Infrastructure/Persistence/Repositories/SentimentRepository.cs              │ Repository implementation; EF LINQ   │
 │                                                                             │ queries; pagination                  │
 ├─────────────────────────────────────────────────────────────────────────────┼──────────────────────────────────────┤
 │ Infrastructure/Services/AnthropicOptions.cs                                 │ Options pattern; type-safe config;   │
 │                                                                             │ ValidateOnStart                      │
 ├─────────────────────────────────────────────────────────────────────────────┼──────────────────────────────────────┤
 │ Infrastructure/Services/AnthropicSentimentService.cs                        │ Typed HTTP client; prompt            │
 │                                                                             │ engineering; JSON response parsing   │
 ├─────────────────────────────────────────────────────────────────────────────┼──────────────────────────────────────┤
 │ Infrastructure/Services/MockSentimentService.cs                             │ Design for testability; switchable   │
 │                                                                             │ via config flag                      │
 └─────────────────────────────────────────────────────────────────────────────┴──────────────────────────────────────┘

 API (update existing)

 ┌─────────────────────────────────────────────────┬────────────────────────────────────────┬────────────────────────┐
 │                      File                       │                 Action                 │        Teaches         │
 ├─────────────────────────────────────────────────┼────────────────────────────────────────┼────────────────────────┤
 │ API/API.csproj                                  │ Add project refs to Application +      │ —                      │
 │                                                 │ Infrastructure                         │                        │
 ├─────────────────────────────────────────────────┼────────────────────────────────────────┼────────────────────────┤
 │ API/API.slnx                                    │ Add all 4 projects                     │ —                      │
 ├─────────────────────────────────────────────────┼────────────────────────────────────────┼────────────────────────┤
 │                                                 │ Replace: AddApplication(),             │ Composition root;      │
 │ API/Program.cs                                  │ AddInfrastructure(), middleware,       │ middleware order       │
 │                                                 │ auto-migrate in dev                    │ matters                │
 ├─────────────────────────────────────────────────┼────────────────────────────────────────┼────────────────────────┤
 │                                                 │ New: thin controller, maps HTTP ↔      │ Controllers            │
 │ API/Controllers/SentimentController.cs          │ MediatR                                │ orchestrate, not       │
 │                                                 │                                        │ compute                │
 ├─────────────────────────────────────────────────┼────────────────────────────────────────┼────────────────────────┤
 │                                                 │                                        │ API surface decoupled  │
 │ API/Controllers/DTOs/AnalyzeSentimentRequest.cs │ New: API input record                  │ from Application       │
 │                                                 │                                        │ commands               │
 ├─────────────────────────────────────────────────┼────────────────────────────────────────┼────────────────────────┤
 │                                                 │ New: catches all exceptions, returns   │ One place for all      │
 │ API/Middleware/ExceptionHandlingMiddleware.cs   │ ProblemDetails                         │ error handling; RFC    │
 │                                                 │                                        │ 7807                   │
 ├─────────────────────────────────────────────────┼────────────────────────────────────────┼────────────────────────┤
 │ API/Controllers/WeatherForecastController.cs    │ Delete                                 │ —                      │
 ├─────────────────────────────────────────────────┼────────────────────────────────────────┼────────────────────────┤
 │ API/WeatherForecast.cs                          │ Delete                                 │ —                      │
 └─────────────────────────────────────────────────┴────────────────────────────────────────┴────────────────────────┘

 ---
 NuGet Packages to Install

 ┌────────────────┬────────────────────────────────────────────────────────────────────────────────────┐
 │    Project     │                                      Package                                       │
 ├────────────────┼────────────────────────────────────────────────────────────────────────────────────┤
 │ Application    │ MediatR v12, FluentValidation v11                                                  │
 ├────────────────┼────────────────────────────────────────────────────────────────────────────────────┤
 │ Infrastructure │ Microsoft.EntityFrameworkCore.Sqlite v10, Microsoft.EntityFrameworkCore.Design v10 │
 ├────────────────┼────────────────────────────────────────────────────────────────────────────────────┤
 │ API            │ Microsoft.EntityFrameworkCore.Design v10 (for EF migrations CLI)                   │
 └────────────────┴────────────────────────────────────────────────────────────────────────────────────┘

 ---
 Configuration (appsettings.json additions)

 {
   "ConnectionStrings": { "DefaultConnection": "Data Source=sentiment.db" },
   "AI": { "UseMock": true },
   "Anthropic": {
     "BaseUrl": "https://api.anthropic.com/",
     "Model": "claude-haiku-4-5-20251001",
     "MaxTokens": 512
   }
 }

 API key goes in User Secrets, never in appsettings:
 dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..."

 ---
 EF Core Migration Commands

 dotnet ef migrations add InitialCreate --project Infrastructure --startup-project API
 dotnet ef database update --project Infrastructure --startup-project API

 ---
 Implementation Order

 0. Create README.md at solution root (C:\code\net\FinancialSentimentAPI\README.md) with:
   - Project overview and goals
   - Architecture diagram (ASCII)
   - API endpoint table
   - Setup instructions (prerequisites, clone, user secrets, migration, run)
   - Project structure reference
 1. Fix Domain — compile-clean with zero warnings before moving on
 2. Create Application project — interfaces + commands/queries + behaviors
 3. Create Infrastructure project — EF Core + repositories + AI service
 4. Wire up API — controller + middleware + Program.cs + delete template files
 5. Run first migration — generate sentiment.db
 6. Smoke test — POST /api/sentiment/analyze with AI:UseMock: true

 ---
 Clean Code Principles Demonstrated

 ┌───────────────────────┬────────────────────────────────────────────────────────────────────────┐
 │       Principle       │                                 Where                                  │
 ├───────────────────────┼────────────────────────────────────────────────────────────────────────┤
 │ Single Responsibility │ Each class has one reason to change (handler ≠ validator ≠ repository) │
 ├───────────────────────┼────────────────────────────────────────────────────────────────────────┤
 │ Open/Closed           │ Add new commands/validators without touching existing handlers         │
 ├───────────────────────┼────────────────────────────────────────────────────────────────────────┤
 │ Dependency Inversion  │ Application depends on interfaces; Infrastructure implements them      │
 ├───────────────────────┼────────────────────────────────────────────────────────────────────────┤
 │ Repository Pattern    │ Decouple persistence from business logic                               │
 ├───────────────────────┼────────────────────────────────────────────────────────────────────────┤
 │ Fail Fast             │ Value objects validate in constructor; invalid state unrepresentable   │
 ├───────────────────────┼────────────────────────────────────────────────────────────────────────┤
 │ Options Pattern       │ Type-safe config; validation at startup not at runtime                 │
 ├───────────────────────┼────────────────────────────────────────────────────────────────────────┤
 │ ProblemDetails        │ Consistent, standard error format across all endpoints                 │
 ├───────────────────────┼────────────────────────────────────────────────────────────────────────┤
 │ Thin Controllers      │ Controllers translate HTTP ↔ use case; zero business logic             │
 └───────────────────────┴────────────────────────────────────────────────────────────────────────┘