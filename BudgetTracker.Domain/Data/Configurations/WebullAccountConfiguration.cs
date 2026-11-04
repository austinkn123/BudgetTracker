using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetTracker.Domain.Data.Configurations;

/// <summary>EF Core Fluent API configuration for <see cref="WebullAccount"/>.</summary>
public class WebullAccountConfiguration : IEntityTypeConfiguration<WebullAccount>
{
    public void Configure(EntityTypeBuilder<WebullAccount> builder)
    {
        builder.ToTable("WebullAccounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.AccountId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.AccountLabel)
            .HasMaxLength(200);

        builder.Property(a => a.AccountType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.TotalNetLiquidationValue)
            .HasColumnType("numeric(18,4)");

        builder.Property(a => a.TotalCashBalance)
            .HasColumnType("numeric(18,4)");

        builder.Property(a => a.Currency)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(a => a.TotalMarketValue)
            .HasColumnType("numeric(18,4)");

        builder.Property(a => a.TotalDayProfitLoss)
            .HasColumnType("numeric(18,4)");

        builder.Property(a => a.TotalUnrealizedProfitLoss)
            .HasColumnType("numeric(18,4)");

        builder.HasMany(a => a.Positions)
            .WithOne()
            .HasForeignKey(p => p.WebullAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.WebullConnectionId, a.AccountId })
            .IsUnique()
            .HasDatabaseName("UQ_WebullAccounts_WebullConnectionId_AccountId");
    }
}
