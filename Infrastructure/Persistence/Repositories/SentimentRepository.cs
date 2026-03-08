using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class SentimentRepository(AppDbContext context) : ISentimentRepository
{
    public async Task AddAsync(SentimentAnalysis analysis, CancellationToken ct = default)
    {
        await context.SentimentAnalyses.AddAsync(analysis, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<SentimentAnalysis> Items, int TotalCount)> GetHistoryAsync(
        StockSymbol symbol,
        int page,
        int pageSize,
        DateTime? from,
        DateTime? to,
        bool excludeNeutral = false,
        CancellationToken ct = default)
    {
        var query = context.SentimentAnalyses
            .Where(a => a.Symbol == symbol.Value);

        if (from.HasValue)
            query = query.Where(a => a.AnalyzedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(a => a.AnalyzedAt <= to.Value);

        if (excludeNeutral)
            query = query.Where(a => a.Label != SentimentLabel.Neutral);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(a => a.AnalyzedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<SentimentAnalysis>> GetForStatsAsync(
        StockSymbol symbol,
        int days,
        CancellationToken ct = default)
    {
        var from = DateTime.UtcNow.AddDays(-days);

        return await context.SentimentAnalyses
            .Where(a => a.Symbol == symbol.Value && a.AnalyzedAt >= from)
            .OrderBy(a => a.AnalyzedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SentimentAnalysis>> GetRecentAsync(
        DateTime from,
        CancellationToken ct = default)
    {
        return await context.SentimentAnalyses
            .Where(a => a.AnalyzedAt >= from)
            .OrderBy(a => a.Symbol)
            .ThenBy(a => a.AnalyzedAt)
            .ToListAsync(ct);
    }
}
