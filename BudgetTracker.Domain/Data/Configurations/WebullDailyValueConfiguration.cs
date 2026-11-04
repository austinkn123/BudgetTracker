using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetTracker.Domain.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="WebullDailyValue"/>. Keyed (connection, Eastern date, account); the key
/// order serves the per-connection date-range read. Deliberately no foreign key to <c>WebullAccounts</c>, so replacing the
/// snapshot never deletes history.
/// </summary>
public class WebullDailyValueConfiguration : IEntityTypeConfiguration<WebullDailyValue>
{
    public void Configure(EntityTypeBuilder<WebullDailyValue> builder)
    {
        builder.ToTable("WebullDailyValues");

        builder.HasKey(d => new { d.WebullConnectionId, d.EasternDate, d.AccountId });

        builder.Property(d => d.EasternDate)
            .HasColumnType("date");

        builder.Property(d => d.AccountId)
            .HasMaxLength(100);

        builder.Property(d => d.TotalValue)
            .HasColumnType("numeric(18,4)");

        builder.Property(d => d.Cash)
            .HasColumnType("numeric(18,4)");

        builder.Property(d => d.MarketValue)
            .HasColumnType("numeric(18,4)");

        builder.Property(d => d.DayProfitLoss)
            .HasColumnType("numeric(18,4)");

        builder.Property(d => d.UnrealizedProfitLoss)
            .HasColumnType("numeric(18,4)");

        builder.Property(d => d.Currency)
            .HasMaxLength(10)
            .IsRequired();
    }
}
