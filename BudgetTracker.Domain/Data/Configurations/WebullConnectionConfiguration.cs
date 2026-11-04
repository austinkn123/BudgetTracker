using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetTracker.Domain.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="WebullConnection"/>. One row per user; enums stored as names; the three
/// token columns are all null or all set.
/// </summary>
public class WebullConnectionConfiguration : IEntityTypeConfiguration<WebullConnection>
{
    public void Configure(EntityTypeBuilder<WebullConnection> builder)
    {
        builder.ToTable("WebullConnections", t =>
        {
            t.HasCheckConstraint("CK_WebullConnections_TokenStatus",
                "\"TokenStatus\" IN ('Pending', 'Normal', 'Expired', 'Invalid')");
            t.HasCheckConstraint("CK_WebullConnections_LastError",
                "\"LastError\" IN ('Unreachable', 'KeyRejected', 'TokenRejected')");
            t.HasCheckConstraint("CK_WebullConnections_Token",
                "(\"AccessTokenEncrypted\" IS NULL AND \"TokenStatus\" IS NULL AND \"TokenCreatedAt\" IS NULL) OR " +
                "(\"AccessTokenEncrypted\" IS NOT NULL AND \"TokenStatus\" IS NOT NULL AND \"TokenCreatedAt\" IS NOT NULL)");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.AccessTokenEncrypted)
            .HasMaxLength(1000);

        builder.Property(c => c.TokenStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(c => c.LastError)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(c => c.UserId)
            .IsUnique()
            .HasDatabaseName("UQ_WebullConnections_UserId");

        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<WebullConnection>(c => c.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(c => c.Accounts)
            .WithOne()
            .HasForeignKey(a => a.WebullConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.DailyValues)
            .WithOne()
            .HasForeignKey(d => d.WebullConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
