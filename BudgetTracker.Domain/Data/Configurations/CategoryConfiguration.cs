using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetTracker.Domain.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories", t =>
        {
            t.HasCheckConstraint("CK_Categories_CategoryType", "CategoryType IN ('Expense', 'Income', 'Both')");
        });

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.CategoryType)
            .HasMaxLength(20)
            .HasDefaultValue("Expense")
            .IsRequired();

        builder.Property(c => c.PlaidCategoryPrimary)
            .HasMaxLength(100);

        builder.HasIndex(c => new { c.UserId, c.Name })
            .IsUnique()
            .HasDatabaseName("UQ_Categories_User_Name");

        // One category per Plaid taxonomy value per user, so a suggestion resolves deterministically.
        builder.HasIndex(c => new { c.UserId, c.PlaidCategoryPrimary })
            .IsUnique()
            .HasFilter("[PlaidCategoryPrimary] IS NOT NULL")
            .HasDatabaseName("UQ_Categories_User_PlaidCategory");

        builder.HasMany(c => c.Transactions)
            .WithOne(t => t.Category)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
