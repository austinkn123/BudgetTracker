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
}
