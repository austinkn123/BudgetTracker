using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetTracker.Domain.Data.Configurations;

public class BudgetPlanLineConfiguration : IEntityTypeConfiguration<BudgetPlanLine>
{
    public void Configure(EntityTypeBuilder<BudgetPlanLine> builder)
    {
        builder.ToTable("BudgetPlanLines", t =>
        {
            t.HasCheckConstraint("CK_BudgetPlanLines_LineType", "LineType IN ('Income', 'Expense')");
            t.HasCheckConstraint("CK_BudgetPlanLines_Bucket", "Bucket IN ('Core', 'Buffer')");
            t.HasCheckConstraint("CK_BudgetPlanLines_Cadence", "Cadence IN ('Monthly', 'Annual')");
            t.HasCheckConstraint("CK_BudgetPlanLines_Amount", "Amount >= 0");
            t.HasCheckConstraint("CK_BudgetPlanLines_MonthlyEq", "MonthlyEquivalent >= 0");
        });

        builder.HasKey(bpl => bpl.Id);

        builder.Property(bpl => bpl.LineType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(bpl => bpl.Bucket)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(bpl => bpl.Cadence)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(bpl => bpl.Amount)
            .HasColumnType("decimal(18, 2)");

        builder.Property(bpl => bpl.MonthlyEquivalent)
            .HasColumnType("decimal(18, 2)");

        builder.Property(bpl => bpl.IsStressFactor)
            .HasDefaultValue(false);

        builder.Property(bpl => bpl.Notes)
            .HasMaxLength(500);

        builder.Property(bpl => bpl.SortOrder)
            .HasDefaultValue(0);

        builder.Property(bpl => bpl.CreatedAt)
            .HasDefaultValueSql("GETDATE()");

        builder.HasIndex(bpl => new { bpl.BudgetPlanId, bpl.Bucket, bpl.LineType })
            .HasDatabaseName("IX_BudgetPlanLines_Plan_Bucket_Type")
            .IncludeProperties(bpl => new { bpl.MonthlyEquivalent, bpl.Amount, bpl.Cadence, bpl.CategoryId, bpl.SortOrder });

        builder.HasOne(bpl => bpl.BudgetPlan)
            .WithMany(bp => bp.Lines)
            .HasForeignKey(bpl => bpl.BudgetPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(bpl => bpl.Category)
            .WithMany(c => c.BudgetPlanLines)
            .HasForeignKey(bpl => bpl.CategoryId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
