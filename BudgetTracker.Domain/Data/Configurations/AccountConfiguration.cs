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
            .HasDefaultValueSql("(now() AT TIME ZONE 'utc')");

        // Names are unique per user ignoring case. Postgres compares text case-sensitively and EF Core cannot model an
        // expression index, so UQ_Accounts_User_Name on ("UserId", lower("Name")) is created with raw SQL in the
        // InitialCreate migration. Re-add that SQL if the migration is ever regenerated (AccountNameUniquenessTests fails otherwise).
        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("IX_Accounts_UserId");

        builder.HasMany(a => a.Transactions)
            .WithOne(t => t.Account)
            .HasForeignKey(t => t.AccountId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
