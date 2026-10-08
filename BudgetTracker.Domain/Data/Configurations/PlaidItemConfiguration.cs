using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetTracker.Domain.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="PlaidItem"/>.
/// A user may link many institutions, so <c>UserId</c> is a plain lookup index; Plaid's own item id stays globally unique.
/// </summary>
public class PlaidItemConfiguration : IEntityTypeConfiguration<PlaidItem>
{
    public void Configure(EntityTypeBuilder<PlaidItem> builder)
    {
        builder.ToTable("PlaidItems");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.PlaidItemId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.InstitutionId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.InstitutionName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(p => p.AccessTokenEncrypted)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(p => p.SyncCursor)
            .HasMaxLength(500);

        builder.Property(p => p.IsActive)
            .HasDefaultValue(true);

        builder.Property(p => p.ConsentExpiresAt)
            .HasColumnType("timestamp without time zone");

        builder.Property(p => p.LastSyncedAt)
            .HasColumnType("timestamp without time zone");

        builder.Property(p => p.CreatedAt)
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("(now() AT TIME ZONE 'utc')");

        builder.HasIndex(p => p.PlaidItemId)
            .IsUnique()
            .HasDatabaseName("UQ_PlaidItems_PlaidItemId");

        builder.HasIndex(p => p.UserId)
            .HasDatabaseName("IX_PlaidItems_UserId");

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(p => p.Accounts)
            .WithOne(a => a.Item)
            .HasForeignKey(a => a.PlaidItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
