using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Engines;

namespace BudgetTracker.Tests.Engines;

public class BudgetPlanEngineTests
{
    private readonly BudgetPlanEngine _engine = new();

    [Fact]
    public void Should_ReturnNull_When_NameIsNotTakenForTheMonth()
    {
        Assert.Null(_engine.ValidateNameIsUnique("Baseline", ["Stretch"]));
    }

    [Fact]
    public void Should_ReturnNull_When_UserHasNoOtherPlansForTheMonth()
    {
        Assert.Null(_engine.ValidateNameIsUnique("Baseline", []));
    }

    [Theory]
    [InlineData("Baseline")]
    [InlineData("baseline")]
    [InlineData("BASELINE")]
    public void Should_ReturnDuplicateError_When_NameMatchesExistingIgnoringCase(string name)
    {
        var error = _engine.ValidateNameIsUnique(name, ["Stretch", "Baseline"]);

        Assert.Equal($"A budget plan named \"{name}\" already exists for this month", error);
    }

    [Fact]
    public void Should_Throw_When_NameIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.ValidateNameIsUnique(null!, ["Baseline"]));
    }

    [Fact]
    public void Should_Throw_When_ExistingNamesIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.ValidateNameIsUnique("Baseline", null!));
    }

    [Fact]
    public void Should_ReturnTheUniquenessMessage_When_BuildingDuplicateNameError()
    {
        Assert.Equal(_engine.ValidateNameIsUnique("Baseline", ["Baseline"]), _engine.DuplicateNameError("Baseline"));
    }

    [Fact]
    public void Should_SetNameToEmptyWithoutThrowing_When_NormalizingANullName()
    {
        var plan = new BudgetPlan { UserId = 5, Name = null!, PlanMonth = new DateTime(2026, 3, 17) };

        _engine.NormalizeForPersistence(plan);

        Assert.Equal(string.Empty, plan.Name);
        Assert.Equal("Plan name is required", _engine.ValidateBudgetPlan(plan));
    }

    private static BudgetPlan Plan(int id, DateTime planMonth, bool isActive = true) =>
        new() { Id = id, UserId = 1, Name = $"Plan {id}", PlanMonth = planMonth, IsActive = isActive };

    [Fact]
    public void Should_PickNewestEffectivePlan_When_SeveralHaveTakenEffect()
    {
        var plans = new[] { Plan(1, new DateTime(2026, 1, 1)), Plan(2, new DateTime(2026, 5, 1)), Plan(3, new DateTime(2026, 3, 1)) };

        Assert.Equal(2, _engine.SelectEffectivePlan(plans, new DateTime(2026, 6, 1))?.Id);
    }

    [Fact]
    public void Should_IncludePlanDatedThatMonth_When_SelectingEffectivePlan()
    {
        var plans = new[] { Plan(1, new DateTime(2026, 1, 1)), Plan(2, new DateTime(2026, 6, 1)) };

        Assert.Equal(2, _engine.SelectEffectivePlan(plans, new DateTime(2026, 6, 1))?.Id);
    }

    [Fact]
    public void Should_IgnoreFuturePlans_When_SelectingEffectivePlan()
    {
        var plans = new[] { Plan(1, new DateTime(2026, 1, 1)), Plan(2, new DateTime(2026, 7, 1)) };

        Assert.Equal(1, _engine.SelectEffectivePlan(plans, new DateTime(2026, 6, 1))?.Id);
    }

    [Fact]
    public void Should_IgnoreInactivePlans_When_SelectingEffectivePlan()
    {
        var plans = new[] { Plan(1, new DateTime(2026, 1, 1)), Plan(2, new DateTime(2026, 5, 1), isActive: false) };

        Assert.Equal(1, _engine.SelectEffectivePlan(plans, new DateTime(2026, 6, 1))?.Id);
    }

    [Fact]
    public void Should_ReturnNull_When_NoPlanHasTakenEffect()
    {
        var plans = new[] { Plan(1, new DateTime(2026, 7, 1)), Plan(2, new DateTime(2026, 1, 1), isActive: false) };

        Assert.Null(_engine.SelectEffectivePlan(plans, new DateTime(2026, 6, 1)));
    }

    [Fact]
    public void Should_ReturnNull_When_UserHasNoPlans()
    {
        Assert.Null(_engine.SelectEffectivePlan([], new DateTime(2026, 6, 1)));
    }
}
