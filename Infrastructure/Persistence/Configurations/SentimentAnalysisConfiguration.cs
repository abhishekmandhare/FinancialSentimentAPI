using System.Text.Json;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps SentimentAnalysis to the DB schema.
///
/// Value objects (StockSymbol, SentimentScore) are stored as scalar columns
/// using HasConversion — cleaner than OwnsOne for single-column VOs.
///
/// KeyReasons (List&lt;string&gt;) is stored as a JSON column — simplest approach
/// for a small list that doesn't need to be queried individually.
///
/// The composite index on (Symbol, AnalyzedAt DESC) is the most important
/// performance decision: every history and stats query filters by symbol
/// and orders/aggregates by date.
/// </summary>
public class SentimentAnalysisConfiguration : IEntityTypeConfiguration<SentimentAnalysis>
{
    public void Configure(EntityTypeBuilder<SentimentAnalysis> builder)
    {
        builder.ToTable("SentimentAnalyses");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("Id");

        builder.Property(a => a.Symbol)
            .HasColumnName("Symbol")
            .HasMaxLength(10)
            .IsRequired()
            .HasConversion(
                s => s.Value,
                v => new StockSymbol(v));

        builder.Property(a => a.OriginalText)
            .HasColumnName("OriginalText")
            .IsRequired();

        builder.Property(a => a.SourceUrl)
            .HasColumnName("SourceUrl");

        builder.Property(a => a.Score)
            .HasColumnName("Score")
            .IsRequired()
            .HasConversion(
                s => s.Value,
                v => new SentimentScore(v));

        builder.Property(a => a.Label)
            .HasColumnName("Label")
            .IsRequired()
            .HasConversion(
                l => l.ToString(),
                v => Enum.Parse<SentimentLabel>(v));

        builder.Property(a => a.Confidence)
            .HasColumnName("Confidence")
            .IsRequired();

        builder.Property(a => a.KeyReasons)
            .HasColumnName("KeyReasons")
            .IsRequired()
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default)!)
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (a, b) => a != null && b != null && a.SequenceEqual(b),
                v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
                v => v.ToList()));

        builder.Property(a => a.ModelVersion)
            .HasColumnName("ModelVersion")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.AnalyzedAt)
            .HasColumnName("AnalyzedAt")
            .IsRequired();

        builder.Property(a => a.DurationMs)
            .HasColumnName("DurationMs");

        // Critical for query performance — all history + stats queries hit this index.
        builder.HasIndex(a => new { a.Symbol, a.AnalyzedAt })
            .HasDatabaseName("IX_SentimentAnalyses_Symbol_AnalyzedAt");

        // Domain events are not persisted — they're in-memory only.
        builder.Ignore(a => a.DomainEvents);
    }
}
