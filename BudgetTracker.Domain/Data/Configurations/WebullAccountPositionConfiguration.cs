using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetTracker.Domain.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="WebullAccountPosition"/>. Prices and quantities keep 10 decimals for
/// fractional crypto. No unique index: positions are replaced wholesale per fetch.
/// </summary>
public class WebullAccountPositionConfiguration : IEntityTypeConfiguration<WebullAccountPosition>
{
    public void Configure(EntityTypeBuilder<WebullAccountPosition> builder)
    {
        builder.ToTable("WebullPositions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Symbol)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.InstrumentType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Quantity)
            .HasColumnType("numeric(28,10)");

        builder.Property(p => p.LastPrice)
            .HasColumnType("numeric(28,10)");

        builder.Property(p => p.CostPrice)
            .HasColumnType("numeric(28,10)");

        builder.Property(p => p.UnrealizedProfitLoss)
            .HasColumnType("numeric(18,4)");

        builder.Property(p => p.Currency)
            .HasMaxLength(10)
            .IsRequired();
    }
}
