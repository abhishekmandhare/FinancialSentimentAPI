using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class SymbolSnapshotConfiguration : IEntityTypeConfiguration<SymbolSnapshot>
{
    public void Configure(EntityTypeBuilder<SymbolSnapshot> builder)
    {
        builder.ToTable("SymbolSnapshots");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Symbol)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(s => s.Score).IsRequired();
        builder.Property(s => s.PreviousScore).IsRequired();
        builder.Property(s => s.Delta).IsRequired();

        builder.Property(s => s.Direction)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(s => s.Trend)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.Dispersion).IsRequired();
        builder.Property(s => s.ArticleCount).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        builder.HasIndex(s => s.Symbol)
            .IsUnique()
            .HasDatabaseName("IX_SymbolSnapshots_Symbol");
    }
}
