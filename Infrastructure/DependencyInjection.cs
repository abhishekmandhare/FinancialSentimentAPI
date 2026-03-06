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
        services.AddScoped<ITrackedSymbolsProvider, ConfigTrackedSymbolsProvider>();

        // --- AI Service (switchable via config) ---
        var aiProvider = configuration["AI:Provider"] ?? "Mock";

        if (aiProvider.Equals("Anthropic", StringComparison.OrdinalIgnoreCase))
        {
            services.AddOptions<AnthropicOptions>()
                .Bind(configuration.GetSection(AnthropicOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddHttpClient<IAiSentimentService, AnthropicSentimentService>(client =>
            {
                var baseUrl = configuration["Anthropic:BaseUrl"] ?? "https://api.anthropic.com/";
                client.BaseAddress = new Uri(baseUrl);
            });
        }
        else
        {
            services.AddSingleton<IAiSentimentService, MockSentimentService>();
        }

        // --- Ingestion Pipeline ---
        services.Configure<IngestionOptions>(
            configuration.GetSection(IngestionOptions.SectionName));

        services.AddSingleton<IArticleQueue, InMemoryArticleQueue>();

        services.AddHttpClient<INewsSourceService, RssNewsSourceService>(client =>
        {
            // Yahoo Finance returns 429 for requests without a browser User-Agent
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        });

        services.AddHostedService<SentimentIngestionWorker>();
        services.AddHostedService<SentimentAnalysisWorker>();

        // --- Health Checks ---
        services.AddHealthChecks()
            .AddCheck<DbHealthCheck>("database");

        return services;
    }
}
