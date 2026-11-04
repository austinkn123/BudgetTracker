using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetTracker.Domain.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="WebullAccountOrderSync"/> (table WebullOrderSyncs). One row per
/// (connection, account); coverage bounds are both null or both set; a pagination key needs a window. No foreign key to
/// <c>WebullAccounts</c>.
/// </summary>
public class WebullAccountOrderSyncConfiguration : IEntityTypeConfiguration<WebullAccountOrderSync>
{
    public void Configure(EntityTypeBuilder<WebullAccountOrderSync> builder)
    {
        builder.ToTable("WebullOrderSyncs", t =>
        {
            t.HasCheckConstraint("CK_WebullOrderSyncs_LastError",
                "\"LastError\" IN ('Unreachable', 'KeyRejected', 'TokenRejected')");
            t.HasCheckConstraint("CK_WebullOrderSyncs_Covered",
                "(\"CoveredFrom\" IS NULL AND \"CoveredTo\" IS NULL) OR (\"CoveredFrom\" IS NOT NULL AND \"CoveredTo\" IS NOT NULL)");
            t.HasCheckConstraint("CK_WebullOrderSyncs_Window",
                "(\"WindowStart\" IS NULL AND \"WindowEnd\" IS NULL AND \"PaginationKey\" IS NULL) OR " +
                "(\"WindowStart\" IS NOT NULL AND \"WindowEnd\" IS NOT NULL)");
        });

        builder.HasKey(s => new { s.WebullConnectionId, s.AccountId });

        builder.Property(s => s.AccountId)
            .HasMaxLength(100);

        builder.Property(s => s.PaginationKey)
            .HasMaxLength(2000);

        builder.Property(s => s.LastError)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasOne<WebullConnection>()
            .WithMany()
            .HasForeignKey(s => s.WebullConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
