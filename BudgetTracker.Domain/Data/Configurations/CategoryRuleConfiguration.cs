using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetTracker.Domain.Data.Configurations;

public class CategoryRuleConfiguration : IEntityTypeConfiguration<CategoryRule>
{
    public void Configure(EntityTypeBuilder<CategoryRule> builder)
    {
        builder.ToTable("CategoryRules", t =>
        {
            t.HasCheckConstraint("CK_CategoryRules_MatchType", "\"MatchType\" IN ('Contains', 'Exact')");
            t.HasCheckConstraint("CK_CategoryRules_Pattern", "btrim(\"Pattern\") <> ''");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.MatchType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.Pattern)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(r => r.CreatedAt)
            .HasDefaultValueSql("(now() AT TIME ZONE 'utc')");

        // One rule per pattern and match type per user, ignoring case. EF Core cannot model the expression index, so
        // UQ_CategoryRules_User_Pattern on ("UserId", lower("Pattern"), "MatchType") is raw SQL in the AddCategoryRules
        // migration. This plain index on UserId backs the FK and the per-user reads.
        builder.HasIndex(r => r.UserId)
            .HasDatabaseName("IX_CategoryRules_UserId");

        builder.HasIndex(r => r.CategoryId)
            .HasDatabaseName("IX_CategoryRules_CategoryId");

        // NO ACTION like every other per-user table: deleting a user is an explicit, ordered job (BUD-27), not a cascade.
        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // A category a rule uses can't be deleted (the manager names the rules; this is the backstop). NO ACTION, not
        // RESTRICT, matching the other foreign keys onto Categories.
        builder.HasOne(r => r.Category)
            .WithMany()
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
