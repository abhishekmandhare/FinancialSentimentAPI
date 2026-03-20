using Application.Services;
using Domain.Interfaces;
using Infrastructure.Ingestion;
using Infrastructure.Monitoring;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- Persistence ---
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<ISentimentRepository, SentimentRepository>();
        services.AddScoped<ITrackedSymbolRepository, TrackedSymbolRepository>();
        services.AddScoped<ITrackedSymbolsProvider, DbTrackedSymbolsProvider>();
        services.AddScoped<ISymbolSnapshotRepository, SymbolSnapshotRepository>();
        services.AddScoped<ISystemStatsRepository, SystemStatsRepository>();

        // --- AI Service (switchable via config) ---
        var aiProvider = configuration["AI:Provider"] ?? "Mock";

        switch (aiProvider.ToLowerInvariant())
        {
            case "anthropic":
                services.AddOptions<AnthropicOptions>()
                    .Bind(configuration.GetSection(AnthropicOptions.SectionName))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                services.AddHttpClient<IAiSentimentService, AnthropicSentimentService>(client =>
                {
                    var baseUrl = configuration["Anthropic:BaseUrl"] ?? "https://api.anthropic.com/";
                    client.BaseAddress = new Uri(baseUrl);
                });
                break;

            case "ollama":
                services.AddOptions<OllamaOptions>()
                    .Bind(configuration.GetSection(OllamaOptions.SectionName))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                services.AddHttpClient<IAiSentimentService, OllamaSentimentService>(client =>
                {
                    var baseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
                    client.BaseAddress = new Uri(baseUrl);
                });
                break;

            case "finbert":
                services.AddOptions<FinBertOptions>()
                    .Bind(configuration.GetSection(FinBertOptions.SectionName))
                    .ValidateDataAnnotations()
                    .ValidateOnStart();

                services.AddHttpClient<IAiSentimentService, FinBertSentimentService>(client =>
                {
                    var opts = configuration.GetSection(FinBertOptions.SectionName).Get<FinBertOptions>()!;
                    client.BaseAddress = new Uri(opts.BaseUrl);
                    client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
                });
                break;

            default:
                services.AddSingleton<IAiSentimentService, MockSentimentService>();
                break;
        }

        // --- Symbol Validation ---
        services.AddHttpClient<ISymbolValidationService, YahooSymbolValidationService>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        });

        // --- Ingestion Pipeline ---
        services.Configure<IngestionOptions>(
            configuration.GetSection(IngestionOptions.SectionName));

        services.AddSingleton<IArticleQueue, InMemoryArticleQueue>();
        services.AddSingleton<IArticleDeduplicator, InMemoryArticleDeduplicator>();
        services.AddSingleton<IArticleRelevanceFilter, ArticleRelevanceFilter>();

        const string userAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

        services.AddHttpClient<RssNewsSourceService>(client =>
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent));

        services.AddHttpClient<GoogleNewsSourceService>(client =>
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent));

        services.AddHttpClient<RedditNewsSourceService>(client =>
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent));

        services.AddHttpClient<CoinDeskNewsSourceService>(client =>
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent));

        services.AddHttpClient<CoinTelegraphNewsSourceService>(client =>
            client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent));

        services.AddTransient<INewsSourceService>(sp =>
        {
            INewsSourceService[] sources =
            [
                sp.GetRequiredService<RssNewsSourceService>(),
                sp.GetRequiredService<GoogleNewsSourceService>(),
                sp.GetRequiredService<RedditNewsSourceService>(),
                sp.GetRequiredService<CoinDeskNewsSourceService>(),
                sp.GetRequiredService<CoinTelegraphNewsSourceService>(),
            ];
            var log = sp.GetRequiredService<ILogger<CompositeNewsSourceService>>();
            return new CompositeNewsSourceService(sources, log);
        });

        services.AddHostedService<SymbolSeedingWorker>();
        services.AddHostedService<SentimentIngestionWorker>();
        services.AddHostedService<SentimentAnalysisWorker>();

        // --- Health Checks ---
        services.AddHealthChecks()
            .AddCheck<DbHealthCheck>("database");

        return services;
    }
}
