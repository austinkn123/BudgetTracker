using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetTracker.Domain.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="PlaidAccount"/>.
/// </summary>
public class PlaidAccountConfiguration : IEntityTypeConfiguration<PlaidAccount>
{
    public void Configure(EntityTypeBuilder<PlaidAccount> builder)
    {
        builder.ToTable("PlaidAccounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.PlaidAccountId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.Mask)
            .HasMaxLength(4);

        builder.Property(a => a.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(a => a.AccountType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.AccountSubtype)
            .HasMaxLength(50);

        builder.HasIndex(a => a.PlaidAccountId)
            .IsUnique()
            .HasDatabaseName("UQ_PlaidAccounts_PlaidAccountId");
    }
}
