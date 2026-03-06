using API.Middleware;
using Application;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Services ────────────────────────────────────────────────────────────────

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Each layer owns its own DI registration.
// API only calls AddApplication() and AddInfrastructure() — knows nothing about internals.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ── App pipeline ─────────────────────────────────────────────────────────────

var app = builder.Build();

// Auto-migrate on every startup — idempotent, safe for single-instance deployments
// (home server, Cloud Run). For multi-instance HA, extract to a separate migration job.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(opts =>
    {
        opts.Title = "Financial Sentiment API";
        opts.Theme = ScalarTheme.DeepSpace;
    });
}

// Middleware order matters:
// 1. Correlation ID — tag every request first so all subsequent logs carry the ID
// 2. Exception handling — catch anything that bubbles up from the pipeline
// 3. HTTPS redirect
// 4. Auth (placeholder — not yet implemented)
// 5. Controllers
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Health checks — required for Cloud Run readiness probes
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

app.Run();
