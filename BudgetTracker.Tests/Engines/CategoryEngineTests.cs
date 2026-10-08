using BudgetTracker.Domain.Engines;

namespace BudgetTracker.Tests.Engines;

public class CategoryEngineTests
{
    private readonly CategoryEngine _engine = new();

    [Fact]
    public void Should_ReturnNull_When_NameIsNotTaken()
    {
        Assert.Null(_engine.ValidateNameIsUnique("Food", ["Rent", "Travel"]));
    }

    [Fact]
    public void Should_ReturnNull_When_UserHasNoOtherCategories()
    {
        Assert.Null(_engine.ValidateNameIsUnique("Food", []));
    }

    [Theory]
    [InlineData("Food")]
    [InlineData("food")]
    [InlineData("FOOD")]
    public void Should_ReturnDuplicateError_When_NameMatchesExistingIgnoringCase(string name)
    {
        var error = _engine.ValidateNameIsUnique(name, ["Rent", "Food"]);

        Assert.Equal($"A category named \"{name}\" already exists", error);
    }

    [Fact]
    public void Should_ReturnNull_When_NameDiffersOnlyByWhitespace()
    {
        // Mirrors the database index, which compares lower("Name") without trimming.
        Assert.Null(_engine.ValidateNameIsUnique("Food ", ["Food"]));
    }

    [Fact]
    public void Should_Throw_When_NameIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.ValidateNameIsUnique(null!, ["Food"]));
    }

    [Fact]
    public void Should_Throw_When_ExistingNamesIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.ValidateNameIsUnique("Food", null!));
    }

    [Fact]
    public void Should_ReturnTheUniquenessMessage_When_BuildingDuplicateNameError()
    {
        Assert.Equal(_engine.ValidateNameIsUnique("Food", ["Food"]), _engine.DuplicateNameError("Food"));
    }
}
