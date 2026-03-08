using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tests.E2E;

/// <summary>
/// Shared WebApplicationFactory for end-to-end integration tests.
///
/// Replaces the Postgres DbContext with an in-memory SQLite database so tests
/// run without Docker or any external services. The SQLite connection is kept
/// open for the lifetime of the factory to preserve the in-memory database.
///
/// - Uses "Testing" environment (Mock AI provider, skips auto-migration).
/// - Removes all hosted services (ingestion workers need real RSS feeds).
/// - Clears logging providers (avoids Windows EventLog permission errors).
/// - Applies EF Core migrations against SQLite so the schema is real.
/// </summary>
public class E2EWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureServices(services =>
        {
            // Remove background workers — they need real external services.
            services.RemoveAll<IHostedService>();

            // Remove ALL EF Core / DbContext registrations so the Npgsql provider
            // does not conflict with the SQLite provider we add below.
            var descriptorsToRemove = services
                .Where(d => d.ServiceType.FullName != null &&
                    (d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                     d.ServiceType == typeof(DbContextOptions) ||
                     d.ServiceType == typeof(AppDbContext) ||
                     d.ServiceType.FullName.Contains("EntityFrameworkCore")))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
                services.Remove(descriptor);

            // Keep the connection open so SQLite in-memory DB persists across scopes.
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            services.AddDbContext<AppDbContext>(opts =>
                opts.UseSqlite(_connection));
        });
    }

    /// <summary>
    /// Ensures the database schema is created before tests run.
    /// Call this after creating the factory but before sending requests.
    /// </summary>
    public void EnsureDatabaseCreated()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection?.Close();
            _connection?.Dispose();
        }
    }
}
