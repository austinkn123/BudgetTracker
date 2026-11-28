using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BudgetTracker.Domain.Data.Configurations;

public class FeedbackConfiguration : IEntityTypeConfiguration<Feedback>
{
    public void Configure(EntityTypeBuilder<Feedback> builder)
    {
        builder.ToTable("Feedback", t =>
        {
            t.HasCheckConstraint("CK_Feedback_Message", "btrim(\"Message\") <> ''");
        });

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Message)
            .HasMaxLength(FeedbackLimits.MaxMessageLength)
            .IsRequired();

        builder.Property(f => f.Page)
            .HasMaxLength(FeedbackLimits.MaxPageLength);

        builder.Property(f => f.CreatedAt)
            .HasDefaultValueSql("(now() AT TIME ZONE 'utc')");

        // Backs the FK and the per-user "how many in the last hour/day" rate-limit count.
        builder.HasIndex(f => new { f.UserId, f.CreatedAt })
            .HasDatabaseName("IX_Feedback_UserId_CreatedAt");

        // NO ACTION like every other per-user table: deleting a user is an explicit, ordered job (BUD-27), not a cascade.
        builder.HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
