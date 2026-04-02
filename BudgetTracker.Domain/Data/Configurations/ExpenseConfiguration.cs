using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetTracker.Domain.Data.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("Expenses");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Amount)
            .HasColumnType("decimal(18, 2)");

        builder.Property(e => e.Date)
            .HasColumnType("datetime2");

        builder.Property(e => e.Merchant)
            .HasMaxLength(255);

        builder.Property(e => e.Notes)
            .HasMaxLength(1000);

        builder.Property(e => e.CreatedAt)
            .HasDefaultValueSql("GETDATE()");
    }
}
