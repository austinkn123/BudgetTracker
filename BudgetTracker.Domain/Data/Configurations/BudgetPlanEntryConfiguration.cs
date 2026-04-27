using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetTracker.Domain.Data.Configurations;

public class BudgetPlanEntryConfiguration : IEntityTypeConfiguration<BudgetPlanEntry>
{
    public void Configure(EntityTypeBuilder<BudgetPlanEntry> builder)
    {
        builder.ToTable("BudgetPlanEntries", t =>
        {
            t.HasCheckConstraint("CK_BudgetPlanEntries_LineType", "LineType IN ('Income', 'Expense')");
            t.HasCheckConstraint("CK_BudgetPlanEntries_Bucket", "Bucket IN ('Core', 'Buffer')");
            t.HasCheckConstraint("CK_BudgetPlanEntries_Cadence", "Cadence IN ('Monthly', 'Annual')");
            t.HasCheckConstraint("CK_BudgetPlanEntries_Amount", "Amount >= 0");
            t.HasCheckConstraint("CK_BudgetPlanEntries_MonthlyEq", "MonthlyEquivalent >= 0");
        });

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.LineType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(entry => entry.Bucket)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(entry => entry.Cadence)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(entry => entry.Amount)
            .HasColumnType("decimal(18, 2)");

        builder.Property(entry => entry.MonthlyEquivalent)
            .HasColumnType("decimal(18, 2)");

        builder.Property(entry => entry.IsStressFactor)
            .HasDefaultValue(false);

        builder.Property(entry => entry.Notes)
            .HasMaxLength(500);

        builder.Property(entry => entry.SortOrder)
            .HasDefaultValue(0);

        builder.Property(entry => entry.CreatedAt)
            .HasDefaultValueSql("GETDATE()");

        builder.HasIndex(entry => new { entry.BudgetPlanId, entry.Bucket, entry.LineType })
            .HasDatabaseName("IX_BudgetPlanEntries_Plan_Bucket_Type")
            .IncludeProperties(entry => new { entry.MonthlyEquivalent, entry.Amount, entry.Cadence, entry.CategoryId, entry.SortOrder });

        builder.HasOne(entry => entry.BudgetPlan)
            .WithMany(bp => bp.Entries)
            .HasForeignKey(entry => entry.BudgetPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(entry => entry.Category)
            .WithMany(c => c.BudgetPlanEntries)
            .HasForeignKey(entry => entry.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}