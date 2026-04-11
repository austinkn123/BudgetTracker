using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetTracker.Domain.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions", t =>
        {
            t.HasCheckConstraint("CK_Transactions_TransactionType",
                "TransactionType IN ('Expense', 'Income', 'Transfer', 'Adjustment')");
            t.HasCheckConstraint("CK_Transactions_PositiveAmount",
                "Amount > 0");
            t.HasCheckConstraint("CK_Transactions_TransferAccount",
                "(TransactionType = 'Transfer' AND TransferAccountId IS NOT NULL) OR (TransactionType <> 'Transfer' AND TransferAccountId IS NULL)");
        });

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TransactionType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.Amount)
            .HasColumnType("decimal(18, 2)");

        builder.Property(t => t.OccurredAt)
            .HasColumnType("datetime2");

        builder.Property(t => t.Payee)
            .HasMaxLength(255);

        builder.Property(t => t.Notes)
            .HasMaxLength(1000);

        builder.Property(t => t.CreatedAt)
            .HasDefaultValueSql("GETDATE()");

        builder.HasIndex(t => new { t.AccountId, t.OccurredAt })
            .HasDatabaseName("IX_Transactions_AccountId_OccurredAt")
            .IncludeProperties(t => new { t.TransactionType, t.Amount, t.CategoryId });

        builder.HasIndex(t => new { t.CategoryId, t.OccurredAt })
            .HasDatabaseName("IX_Transactions_CategoryId_OccurredAt")
            .IncludeProperties(t => new { t.TransactionType, t.Amount, t.AccountId });

        builder.HasOne(t => t.TransferAccount)
            .WithMany()
            .HasForeignKey(t => t.TransferAccountId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
