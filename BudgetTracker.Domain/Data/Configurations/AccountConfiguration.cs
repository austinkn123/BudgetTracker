using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetTracker.Domain.Data.Configurations;

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Accounts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(a => a.AccountType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.CreatedAt)
            .HasDefaultValueSql("GETDATE()");

        builder.HasIndex(a => new { a.UserId, a.Name })
            .IsUnique()
            .HasDatabaseName("UQ_Accounts_User_Name");

        builder.HasMany(a => a.Transactions)
            .WithOne(t => t.Account)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
