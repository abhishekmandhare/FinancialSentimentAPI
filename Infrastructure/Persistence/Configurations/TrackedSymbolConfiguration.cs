using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class TrackedSymbolConfiguration : IEntityTypeConfiguration<TrackedSymbol>
{
    public void Configure(EntityTypeBuilder<TrackedSymbol> builder)
    {
        builder.ToTable("TrackedSymbols");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .HasColumnName("Id");

        builder.Property(s => s.Symbol)
            .HasColumnName("Symbol")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(s => s.AddedAt)
            .HasColumnName("AddedAt")
            .IsRequired();

        // Unique index — the same symbol cannot appear twice.
        builder.HasIndex(s => s.Symbol)
            .IsUnique()
            .HasDatabaseName("IX_TrackedSymbols_Symbol");
    }
}
