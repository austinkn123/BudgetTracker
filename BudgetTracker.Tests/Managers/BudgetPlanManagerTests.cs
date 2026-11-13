using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using BudgetTracker.Server.Managers;
using Moq;

namespace BudgetTracker.Tests.Managers;

public class BudgetPlanManagerTests
{
    private const int UserId = 5;
    private static readonly DateTime March = new(2026, 3, 1);

    private readonly Mock<IBudgetPlanAccessor> _accessor = new(MockBehavior.Strict);
    private readonly BudgetPlanEngine _engine = new();
    private readonly Mock<IDataVersionAccessor> _dataVersion = new(MockBehavior.Strict);
    private readonly List<string> _calls = [];

    public BudgetPlanManagerTests()
    {
        _accessor.Setup(a => a.GetOtherNamesForMonthAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>()))
            .ReturnsAsync([]);
        _dataVersion.Setup(d => d.BumpAsync(It.IsAny<int>()))
            .Callback<int>(userId => _calls.Add($"bump:{userId}"))
            .ReturnsAsync(true);
    }

    private BudgetPlanManager BuildSut() => new(_engine, _accessor.Object, _dataVersion.Object);

    private static BudgetPlan BuildPlan(int id = 0, string name = "Baseline") => new()
    {
        Id = id,
        Name = name,
        PlanMonth = new DateTime(2026, 3, 17),
        NetIncomeMonthly = 5000m
    };

    [Fact]
    public async Task Create_UniqueName_Persists()
    {
        _accessor.Setup(a => a.CreateAsync(It.IsAny<BudgetPlan>())).ReturnsAsync(3);

        var result = await BuildSut().CreateAsync(BuildPlan(), UserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value);
    }

    [Fact]
    public async Task Create_NameTakenForTheMonthIgnoringCase_ReturnsDuplicateErrorAndDoesNotPersist()
    {
        _accessor.Setup(a => a.GetOtherNamesForMonthAsync(UserId, March, 0)).ReturnsAsync(["baseline"]);

        var result = await BuildSut().CreateAsync(BuildPlan(name: "  Baseline  "), UserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("A budget plan named \"Baseline\" already exists for this month", result.Error);
        _accessor.Verify(a => a.CreateAsync(It.IsAny<BudgetPlan>()), Times.Never);
    }

    [Fact]
    public async Task Create_ComparesAgainstTheNormalizedPlanMonth()
    {
        _accessor.Setup(a => a.CreateAsync(It.IsAny<BudgetPlan>())).ReturnsAsync(3);

        await BuildSut().CreateAsync(BuildPlan(), UserId);

        _accessor.Verify(a => a.GetOtherNamesForMonthAsync(UserId, March, 0), Times.Once);
    }

    [Fact]
    public async Task Create_InvalidPlan_DoesNotFetchNames()
    {
        var result = await BuildSut().CreateAsync(BuildPlan(name: " "), UserId);

        Assert.Equal("Plan name is required", result.Error);
        _accessor.Verify(
            a => a.GetOtherNamesForMonthAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Update_NameTakenByAnotherPlanIgnoringCase_ReturnsDuplicateErrorAndDoesNotPersist()
    {
        _accessor.Setup(a => a.GetOtherNamesForMonthAsync(UserId, March, 7)).ReturnsAsync(["STRETCH"]);

        var result = await BuildSut().UpdateAsync(BuildPlan(id: 7, name: "Stretch"), UserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("A budget plan named \"Stretch\" already exists for this month", result.Error);
        _accessor.Verify(a => a.UpdateAsync(It.IsAny<BudgetPlan>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Create_NullName_ReturnsNameRequiredInsteadOfThrowing()
    {
        var result = await BuildSut().CreateAsync(BuildPlan(name: null!), UserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("Plan name is required", result.Error);
    }

    [Fact]
    public async Task Update_NullName_ReturnsNameRequiredInsteadOfThrowing()
    {
        var result = await BuildSut().UpdateAsync(BuildPlan(id: 7, name: null!), UserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("Plan name is required", result.Error);
    }

    [Fact]
    public async Task Create_ConcurrentInsertWinsTheUniqueIndex_ReturnsDuplicateError()
    {
        // Both requests passed the name pre-check; the index rejects the second one.
        _accessor.Setup(a => a.CreateAsync(It.IsAny<BudgetPlan>()))
            .ThrowsAsync(new UniqueNameViolationException("UQ_BudgetPlans_User_Month_Name", new Exception("23505")));

        var result = await BuildSut().CreateAsync(BuildPlan(name: " Baseline "), UserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("A budget plan named \"Baseline\" already exists for this month", result.Error);
    }

    [Fact]
    public async Task Update_ConcurrentSaveWinsTheUniqueIndex_ReturnsDuplicateError()
    {
        _accessor.Setup(a => a.UpdateAsync(It.IsAny<BudgetPlan>(), UserId))
            .ThrowsAsync(new UniqueNameViolationException("UQ_BudgetPlans_User_Month_Name", new Exception("23505")));

        var result = await BuildSut().UpdateAsync(BuildPlan(id: 7, name: "Stretch"), UserId);

        Assert.False(result.IsSuccess);
        Assert.Equal("A budget plan named \"Stretch\" already exists for this month", result.Error);
    }

    [Fact]
    public async Task Update_ExcludesThePlanBeingUpdated()
    {
        _accessor.Setup(a => a.UpdateAsync(It.IsAny<BudgetPlan>(), UserId)).ReturnsAsync(true);

        var result = await BuildSut().UpdateAsync(BuildPlan(id: 7, name: "BASELINE"), UserId);

        Assert.True(result.IsSuccess);
        _accessor.Verify(a => a.GetOtherNamesForMonthAsync(UserId, March, 7), Times.Once);
    }

    // ── Data version (live refresh) ─────────────────────────────────────────

    private void VerifyNeverBumped() => _dataVersion.Verify(d => d.BumpAsync(It.IsAny<int>()), Times.Never);

    [Fact]
    public async Task Create_Persisted_BumpsTheDataVersionAfterTheWrite()
    {
        _accessor.Setup(a => a.CreateAsync(It.IsAny<BudgetPlan>())).Callback(() => _calls.Add("write")).ReturnsAsync(3);

        var result = await BuildSut().CreateAsync(BuildPlan(), UserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(["write", $"bump:{UserId}"], _calls);
    }

    [Fact]
    public async Task Create_InvalidDuplicateOrForeignCategory_DoesNotBump()
    {
        await BuildSut().CreateAsync(BuildPlan(name: null!), UserId);

        _accessor.Setup(a => a.GetOtherNamesForMonthAsync(UserId, March, 0)).ReturnsAsync(["baseline"]);
        await BuildSut().CreateAsync(BuildPlan(), UserId);

        _accessor.Setup(a => a.GetOtherNamesForMonthAsync(UserId, March, 0)).ReturnsAsync([]);
        _accessor.Setup(a => a.CategoriesBelongToUserAsync(It.IsAny<IEnumerable<int>>(), UserId)).ReturnsAsync(false);
        var foreign = BuildPlan();
        foreign.Entries = [new BudgetPlanEntry { CategoryId = 99, LineType = "Expense", Bucket = "Needs", Cadence = "Monthly", Amount = 1m }];
        await BuildSut().CreateAsync(foreign, UserId);

        _accessor.Setup(a => a.CreateAsync(It.IsAny<BudgetPlan>()))
            .ThrowsAsync(new UniqueNameViolationException("UQ_BudgetPlans_User_Month_Name", new Exception("23505")));
        await BuildSut().CreateAsync(BuildPlan(), UserId);

        VerifyNeverBumped();
    }

    [Fact]
    public async Task Update_Persisted_BumpsTheDataVersionAfterTheWrite()
    {
        _accessor.Setup(a => a.UpdateAsync(It.IsAny<BudgetPlan>(), UserId)).Callback(() => _calls.Add("write")).ReturnsAsync(true);

        var result = await BuildSut().UpdateAsync(BuildPlan(id: 7), UserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(["write", $"bump:{UserId}"], _calls);
    }

    [Fact]
    public async Task Update_NotFoundInvalidOrDuplicate_DoesNotBump()
    {
        await BuildSut().UpdateAsync(BuildPlan(id: 7, name: null!), UserId);

        _accessor.Setup(a => a.UpdateAsync(It.IsAny<BudgetPlan>(), UserId)).ReturnsAsync(false);
        await BuildSut().UpdateAsync(BuildPlan(id: 7), UserId);

        _accessor.Setup(a => a.UpdateAsync(It.IsAny<BudgetPlan>(), UserId))
            .ThrowsAsync(new UniqueNameViolationException("UQ_BudgetPlans_User_Month_Name", new Exception("23505")));
        await BuildSut().UpdateAsync(BuildPlan(id: 7), UserId);

        VerifyNeverBumped();
    }

    [Fact]
    public async Task Delete_Deleted_BumpsTheDataVersionAfterTheWrite()
    {
        _accessor.Setup(a => a.DeleteAsync(7, UserId)).Callback(() => _calls.Add("write")).ReturnsAsync(true);

        var result = await BuildSut().DeleteAsync(7, UserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(["write", $"bump:{UserId}"], _calls);
    }

    [Fact]
    public async Task Delete_NotFound_DoesNotBump()
    {
        _accessor.Setup(a => a.DeleteAsync(99, UserId)).ReturnsAsync(false);

        await BuildSut().DeleteAsync(99, UserId);

        VerifyNeverBumped();
    }
}
