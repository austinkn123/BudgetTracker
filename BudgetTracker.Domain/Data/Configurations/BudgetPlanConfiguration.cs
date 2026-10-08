using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetTracker.Domain.Data.Configurations;

public class BudgetPlanConfiguration : IEntityTypeConfiguration<BudgetPlan>
{
    public void Configure(EntityTypeBuilder<BudgetPlan> builder)
    {
        builder.ToTable("BudgetPlans", t =>
        {
            t.HasCheckConstraint("CK_BudgetPlans_NetIncome", "\"NetIncomeMonthly\" >= 0");
        });

        builder.HasKey(bp => bp.Id);

        builder.Property(bp => bp.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(bp => bp.PlanMonth)
            .HasColumnType("date");

        builder.Property(bp => bp.NetIncomeMonthly)
            .HasColumnType("numeric(18,2)");

        builder.Property(bp => bp.IsActive)
            .HasDefaultValue(true);

        builder.Property(bp => bp.CreatedAt)
            .HasDefaultValueSql("(now() AT TIME ZONE 'utc')");

        // Names are unique per user and month ignoring case. Postgres compares text case-sensitively and EF Core cannot
        // model an expression index, so UQ_BudgetPlans_User_Month_Name on ("UserId", "PlanMonth", lower("Name")) is created
        // with raw SQL in the InitialCreate migration. Re-add that SQL if the migration is ever regenerated
        // (BudgetPlanNameUniquenessTests fails otherwise). IX_BudgetPlans_User_Month_Active still leads with UserId for the FK.
        builder.HasIndex(bp => new { bp.UserId, bp.PlanMonth, bp.IsActive })
            .HasDatabaseName("IX_BudgetPlans_User_Month_Active")
            .IncludeProperties(bp => new { bp.Name, bp.NetIncomeMonthly });

        builder.HasOne(bp => bp.User)
            .WithMany(u => u.BudgetPlans)
            .HasForeignKey(bp => bp.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
