using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Tests.Engines;

public class CategoryRuleEngineTests
{
    private readonly CategoryRuleEngine _engine = new();
    private static readonly DateTime Earlier = new(2026, 11, 1);
    private static readonly DateTime Later = new(2026, 11, 2);

    private static CategoryRule Rule(string pattern, string matchType = CategoryRuleMatchType.Contains, int id = 1, int categoryId = 10, DateTime? createdAt = null) =>
        new() { Id = id, Pattern = pattern, MatchType = matchType, CategoryId = categoryId, CreatedAt = createdAt ?? Earlier };

    // --- Validation ---

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Should_RejectABlankPattern(string? pattern)
    {
        Assert.Equal("Pattern is required", _engine.ValidateRule(Rule(pattern!)));
    }

    [Fact]
    public void Should_RejectAPatternOver255Characters()
    {
        Assert.Equal("Pattern must be 255 characters or fewer", _engine.ValidateRule(Rule(new string('a', 256))));
        Assert.Null(_engine.ValidateRule(Rule(new string('a', 255))));
    }

    [Theory]
    [InlineData("Regex")]
    [InlineData("contains")]
    [InlineData("")]
    public void Should_RejectAnUnknownMatchType(string matchType)
    {
        Assert.Equal("Match type must be Contains or Exact", _engine.ValidateRule(Rule("uber", matchType)));
    }

    [Fact]
    public void Should_RejectAMissingCategory()
    {
        Assert.Equal("Category is required", _engine.ValidateRule(Rule("uber", categoryId: 0)));
    }

    [Fact]
    public void Should_TrimThePattern_When_Normalizing()
    {
        Assert.Equal("uber", _engine.NormalizePattern("  uber \t"));
        Assert.Equal("", _engine.NormalizePattern(null));
    }

    // --- Matching ---

    [Theory]
    [InlineData("UBER 072515 SF**POOL**", true)]
    [InlineData("uber eats", true)]
    [InlineData("Lyft", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void Should_MatchAnywhereIgnoringCase_When_Contains(string? payee, bool expected)
    {
        Assert.Equal(expected, _engine.Matches(Rule("Uber"), payee));
    }

    [Theory]
    [InlineData("STARBUCKS", true)]
    [InlineData("  starbucks ", true)]
    [InlineData("Starbucks Reserve", false)]
    public void Should_MatchTheWholePayeeIgnoringCase_When_Exact(string payee, bool expected)
    {
        Assert.Equal(expected, _engine.Matches(Rule("Starbucks", CategoryRuleMatchType.Exact), payee));
    }

    [Fact]
    public void Should_PickTheLongestMatchingPattern()
    {
        var uber = Rule("uber", id: 1, categoryId: 10);
        var uberEats = Rule("uber eats", id: 2, categoryId: 20);

        Assert.Same(uberEats, _engine.FindMatch("UBER EATS 1234", [uber, uberEats]));
        Assert.Same(uber, _engine.FindMatch("UBER TRIP", [uber, uberEats]));
    }

    [Fact]
    public void Should_PickTheNewestRule_When_PatternsAreEquallyLong()
    {
        var older = Rule("uber", CategoryRuleMatchType.Contains, id: 1, categoryId: 10, createdAt: Earlier);
        var newer = Rule("UBER", CategoryRuleMatchType.Exact, id: 2, categoryId: 20, createdAt: Later);

        Assert.Same(newer, _engine.FindMatch("uber", [older, newer]));
    }

    [Fact]
    public void Should_BreakACreatedAtTieByTheHigherId()
    {
        var first = Rule("uber", CategoryRuleMatchType.Contains, id: 1, createdAt: Earlier);
        var second = Rule("uber", CategoryRuleMatchType.Exact, id: 2, createdAt: Earlier);

        Assert.Same(second, _engine.FindMatch("uber", [second, first]));
    }

    [Fact]
    public void Should_ReturnNull_When_NoRuleMatches()
    {
        Assert.Null(_engine.FindMatch("Lyft", [Rule("uber")]));
        Assert.Null(_engine.FindMatch("Lyft", []));
    }

    // --- Payees a rule would file ---

    [Fact]
    public void Should_ListOnlyPayeesTheRuleWins()
    {
        var uber = Rule("uber", id: 1);
        var uberEats = Rule("uber eats", id: 2);

        var payees = _engine.PayeesWonBy(uber, [uber, uberEats], ["UBER TRIP", "Uber Eats 99", "Lyft"]);

        Assert.Equal(["UBER TRIP"], payees);
    }

    [Fact]
    public void Should_CountTheRowsInTheRulesCategoryThatItWins()
    {
        var uber = Rule("uber", id: 1, categoryId: 10);
        PayeeCategoryCount[] counts =
        [
            new("UBER TRIP", 10, 3),
            new("UBER TRIP", 99, 2),   // a different category: the user filed these elsewhere
            new("Lyft", 10, 4),        // the rule doesn't match
        ];

        Assert.Equal(3, _engine.CountMatched(uber, [uber], counts));
    }

    [Fact]
    public void Should_CountUncategorizedRowsTheRuleWouldFile()
    {
        var uber = Rule("uber", id: 1, categoryId: 10);
        PayeeCategoryCount[] counts = [new("UBER TRIP", null, 5), new("UBER TRIP", 10, 3), new("Lyft", null, 1)];

        Assert.Equal(5, _engine.CountUncategorizedWon(uber, [uber], counts));
    }

    // --- Messages ---

    [Fact]
    public void Should_NameTheRules_When_ACategoryIsInUseByRules()
    {
        Assert.Equal(
            "This category is used by rules: \"uber\", \"lyft\". Delete or change those rules first.",
            _engine.CategoryInUseByRulesError(["uber", "lyft"]));
    }
}
