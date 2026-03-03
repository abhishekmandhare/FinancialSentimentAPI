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
            opts.UseSqlite(configuration.GetConnectionString("DefaultConnection")));

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

        services.AddHttpClient<INewsSourceService, RssNewsSourceService>();

        services.AddHostedService<SentimentIngestionWorker>();
        services.AddHostedService<SentimentAnalysisWorker>();

        // --- Health Checks ---
        services.AddHealthChecks()
            .AddCheck<DbHealthCheck>("database");

        return services;
    }
}
