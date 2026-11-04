using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetTracker.Domain.Data.Configurations;

/// <summary>
/// EF Core Fluent API configuration for <see cref="WebullAccountOrder"/> (table WebullOrders). Keyed (connection, account,
/// order); no check constraints, since the columns hold Webull's raw strings. Deliberately no foreign key to
/// <c>WebullAccounts</c>, so replacing the balance snapshot never deletes history.
/// </summary>
public class WebullAccountOrderConfiguration : IEntityTypeConfiguration<WebullAccountOrder>
{
    public void Configure(EntityTypeBuilder<WebullAccountOrder> builder)
    {
        builder.ToTable("WebullOrders");

        builder.HasKey(o => new { o.WebullConnectionId, o.AccountId, o.OrderId });

        builder.Property(o => o.AccountId)
            .HasMaxLength(100);

        builder.Property(o => o.OrderId)
            .HasMaxLength(100);

        builder.Property(o => o.Symbol)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.InstrumentType)
            .HasMaxLength(50);

        builder.Property(o => o.Side)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.Status)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.FilledQuantity)
            .HasColumnType("numeric(28,10)");

        builder.Property(o => o.FilledPrice)
            .HasColumnType("numeric(28,10)");

        builder.HasOne<WebullConnection>()
            .WithMany()
            .HasForeignKey(o => o.WebullConnectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
