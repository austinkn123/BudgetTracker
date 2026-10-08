using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BudgetTracker.Tests.Accessors;

/// <summary>
/// Unique-index violations raised by Postgres when two requests race past the manager's name pre-check.
/// </summary>
public class BudgetPlanAccessorTests : IDisposable
{
    private const int UserId = 5;
    private const string NameIndex = "UQ_BudgetPlans_User_Month_Name";
    private readonly FailingSaveInterceptor _interceptor = new();
    private readonly BudgetTrackerDbContext _context;

    public BudgetPlanAccessorTests()
    {
        var options = new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(_interceptor)
            .Options;
        _context = new BudgetTrackerDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    private BudgetPlanAccessor BuildSut() => new(_context);

    private static BudgetPlan BuildPlan() => new()
    {
        UserId = UserId,
        Name = "Baseline",
        PlanMonth = new DateTime(2026, 3, 1),
        NetIncomeMonthly = 5000m
    };

    [Fact]
    public async Task Create_NameIndexViolated_ThrowsUniqueNameViolation()
    {
        _interceptor.FailNextSaveWith(PostgresErrorCodes.UniqueViolation, NameIndex);

        var ex = await Assert.ThrowsAsync<UniqueNameViolationException>(() => BuildSut().CreateAsync(BuildPlan()));

        Assert.Equal(NameIndex, ex.ConstraintName);
        Assert.IsType<DbUpdateException>(ex.InnerException);
    }

    [Fact]
    public async Task Update_NameIndexViolated_ThrowsUniqueNameViolation()
    {
        var plan = BuildPlan();
        await BuildSut().CreateAsync(plan);
        _interceptor.FailNextSaveWith(PostgresErrorCodes.UniqueViolation, NameIndex);

        var rename = BuildPlan();
        rename.Id = plan.Id;
        rename.Name = "Stretch";

        await Assert.ThrowsAsync<UniqueNameViolationException>(() => BuildSut().UpdateAsync(rename, UserId));
    }

    [Fact]
    public async Task Create_OtherUniqueIndexViolated_RethrowsOriginalException()
    {
        _interceptor.FailNextSaveWith(PostgresErrorCodes.UniqueViolation, "PK_BudgetPlans");

        await Assert.ThrowsAsync<DbUpdateException>(() => BuildSut().CreateAsync(BuildPlan()));
    }
}
