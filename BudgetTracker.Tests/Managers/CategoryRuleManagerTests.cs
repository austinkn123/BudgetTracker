using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;
using BudgetTracker.Server.Managers;
using Moq;

namespace BudgetTracker.Tests.Managers;

public class CategoryRuleManagerTests
{
    private const int UserId = 5;
    private static readonly Category Coffee = new() { Id = 10, UserId = UserId, Name = "Coffee", CategoryType = "Expense" };
    private static readonly Category Salary = new() { Id = 20, UserId = UserId, Name = "Salary", CategoryType = "Income" };

    private readonly Mock<ICategoryRuleAccessor> _rules = new(MockBehavior.Strict);
    private readonly Mock<ICategoryAccessor> _categories = new(MockBehavior.Strict);
    private readonly Mock<ITransactionAccessor> _transactions = new(MockBehavior.Strict);
    private readonly Mock<IDataVersionAccessor> _dataVersion = new(MockBehavior.Strict);
    private readonly List<string> _calls = [];

    public CategoryRuleManagerTests()
    {
        _categories.Setup(c => c.GetByIdForUserAsync(Coffee.Id, UserId)).ReturnsAsync(Coffee);
        _categories.Setup(c => c.GetByIdForUserAsync(Salary.Id, UserId)).ReturnsAsync(Salary);
        _categories.Setup(c => c.GetByIdForUserAsync(It.Is<int>(id => id != Coffee.Id && id != Salary.Id), UserId)).ReturnsAsync((Category?)null);
        _dataVersion.Setup(d => d.BumpAsync(UserId)).Callback(() => _calls.Add("bump")).ReturnsAsync(true);
    }

    private CategoryRuleManager BuildSut() =>
        new(new CategoryRuleEngine(), _rules.Object, _categories.Object, _transactions.Object, new PlaidEngine(), _dataVersion.Object);

    private static CategoryRule Rule(string pattern, int categoryId = 10, string matchType = CategoryRuleMatchType.Contains, int id = 0) =>
        new() { Id = id, UserId = UserId, Pattern = pattern, MatchType = matchType, CategoryId = categoryId };

    private static CategoryRule Stored(int id, string pattern, Category category, DateTime? createdAt = null) =>
        new()
        {
            Id = id, UserId = UserId, Pattern = pattern, MatchType = CategoryRuleMatchType.Contains, CategoryId = category.Id,
            Category = category, CreatedAt = createdAt ?? new DateTime(2026, 11, 1),
        };

    private void GivenCounts(params PayeeCategoryCount[] counts) =>
        _transactions.Setup(t => t.GetPayeeCategoryCountsAsync(UserId)).ReturnsAsync(counts);

    // --- List ---

    [Fact]
    public async Task Should_ListRulesWithTheRowsEachHasFiled()
    {
        _rules.Setup(r => r.GetByUserIdAsync(UserId)).ReturnsAsync([Stored(1, "uber", Coffee), Stored(2, "uber eats", Salary)]);
        GivenCounts(
            new PayeeCategoryCount("UBER TRIP", Coffee.Id, 3),
            new PayeeCategoryCount("UBER EATS 9", Salary.Id, 2),
            new PayeeCategoryCount("UBER EATS 9", Coffee.Id, 7));   // won by "uber eats", which files into Salary: not counted for either

        var result = await BuildSut().GetAllAsync(UserId);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [new(1, "uber", "Contains", Coffee.Id, "Coffee", 3), new(2, "uber eats", "Contains", Salary.Id, "Salary", 2)],
            result.Value!);
    }

    // --- Create ---

    [Fact]
    public async Task Should_RejectAnInvalidRule_WithoutWriting()
    {
        var result = await BuildSut().CreateAsync(Rule("   "), applyToExisting: false);

        Assert.Equal("Pattern is required", result.Error);
        _rules.Verify(r => r.CreateAsync(It.IsAny<CategoryRule>()), Times.Never);
    }

    [Fact]
    public async Task Should_RejectACategoryTheUserDoesNotOwn()
    {
        var result = await BuildSut().CreateAsync(Rule("uber", categoryId: 99), applyToExisting: false);

        Assert.Equal("Category not found", result.Error);
        _rules.Verify(r => r.CreateAsync(It.IsAny<CategoryRule>()), Times.Never);
    }

    [Fact]
    public async Task Should_SaveATrimmedPatternAndBump_When_NotApplyingToExisting()
    {
        CategoryRule? saved = null;
        _rules.Setup(r => r.CreateAsync(It.IsAny<CategoryRule>())).Callback<CategoryRule>(r => saved = r).ReturnsAsync(7);

        var result = await BuildSut().CreateAsync(Rule("  uber  "), applyToExisting: false);

        Assert.Equal(new CategoryRuleCreated(7, 0), result.Value);
        Assert.Equal("uber", saved!.Pattern);
        Assert.Equal(["bump"], _calls);
        _transactions.Verify(t => t.SetCategoryAsync(It.IsAny<IReadOnlyDictionary<int, string>>(), It.IsAny<int?>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Should_ReturnTheDuplicateMessage_When_ThePatternIsTaken()
    {
        _rules.Setup(r => r.CreateAsync(It.IsAny<CategoryRule>()))
            .ThrowsAsync(new UniqueNameViolationException("UQ_CategoryRules_User_Pattern", new Exception()));

        var result = await BuildSut().CreateAsync(Rule("Uber"), applyToExisting: false);

        Assert.Equal("A rule for \"Uber\" already exists", result.Error);
        Assert.Empty(_calls);
    }

    [Fact]
    public async Task Should_FileOnlyUncategorizedRowsTheNewRuleWins_When_ApplyingToExisting()
    {
        _rules.Setup(r => r.CreateAsync(It.IsAny<CategoryRule>())).ReturnsAsync(3);
        _rules.Setup(r => r.GetByUserIdAsync(UserId)).ReturnsAsync([Stored(1, "uber eats", Coffee), Stored(3, "uber", Salary)]);
        GivenCounts(new PayeeCategoryCount("UBER TRIP", null, 2), new PayeeCategoryCount("UBER EATS 9", null, 4), new PayeeCategoryCount("UBER TRIP", Coffee.Id, 1));
        _transactions.Setup(t => t.GetUncategorizedByPayeesAsync(UserId, It.Is<IReadOnlyCollection<string>>(p => p.SequenceEqual(new[] { "UBER TRIP" }))))
            .ReturnsAsync([
                new Transaction { Id = 41, TransactionType = "Expense", Amount = 25m },
                new Transaction { Id = 42, TransactionType = "Expense", Amount = 30m },
            ]);
        IReadOnlyDictionary<int, string>? types = null;
        _transactions.Setup(t => t.SetCategoryAsync(It.IsAny<IReadOnlyDictionary<int, string>>(), Salary.Id, UserId))
            .Callback<IReadOnlyDictionary<int, string>, int?, int>((t, _, _) => { types = t; _calls.Add("file"); })
            .ReturnsAsync(2);

        var result = await BuildSut().CreateAsync(Rule("uber", Salary.Id), applyToExisting: true);

        Assert.Equal(new CategoryRuleCreated(3, 2), result.Value);
        // An Income-typed category re-types the rows it files, as a hand-picked category does.
        Assert.Equal(new Dictionary<int, string> { [41] = "Income", [42] = "Income" }, types);
        Assert.Equal(["file", "bump"], _calls);
    }

    [Fact]
    public async Task Should_FileNothingAndStillSucceed_When_NoUncategorizedRowMatches()
    {
        _rules.Setup(r => r.CreateAsync(It.IsAny<CategoryRule>())).ReturnsAsync(3);
        _rules.Setup(r => r.GetByUserIdAsync(UserId)).ReturnsAsync([Stored(3, "uber", Coffee)]);
        GivenCounts(new PayeeCategoryCount("Lyft", null, 2));
        _transactions.Setup(t => t.GetUncategorizedByPayeesAsync(UserId, It.Is<IReadOnlyCollection<string>>(p => p.Count == 0))).ReturnsAsync([]);

        var result = await BuildSut().CreateAsync(Rule("uber"), applyToExisting: true);

        Assert.Equal(new CategoryRuleCreated(3, 0), result.Value);
        _transactions.Verify(t => t.SetCategoryAsync(It.IsAny<IReadOnlyDictionary<int, string>>(), It.IsAny<int?>(), It.IsAny<int>()), Times.Never);
    }

    // --- Preview ---

    [Fact]
    public async Task Should_CountWhatTheCandidateWouldFile_AheadOfOlderEquallyLongRules_WithoutWriting()
    {
        _rules.Setup(r => r.GetByUserIdAsync(UserId)).ReturnsAsync([Stored(1, "UBER", Salary)]);
        GivenCounts(new PayeeCategoryCount("UBER TRIP", null, 2), new PayeeCategoryCount("Lyft", null, 9), new PayeeCategoryCount("UBER TRIP", Coffee.Id, 5));

        var result = await BuildSut().PreviewAsync(Rule("uber"));

        Assert.Equal(2, result.Value);
        Assert.Empty(_calls);
    }

    [Fact]
    public async Task Should_ValidateTheCandidate_When_Previewing()
    {
        var result = await BuildSut().PreviewAsync(Rule("uber", matchType: "Regex"));

        Assert.Equal("Match type must be Contains or Exact", result.Error);
    }

    // --- Update / delete ---

    [Fact]
    public async Task Should_ReportNotFound_When_UpdatingAnotherUsersRule()
    {
        _rules.Setup(r => r.UpdateAsync(It.IsAny<CategoryRule>())).ReturnsAsync(false);

        var result = await BuildSut().UpdateAsync(Rule("uber", id: 3));

        Assert.Equal("Rule not found", result.Error);
        Assert.Empty(_calls);
    }

    [Fact]
    public async Task Should_SaveATrimmedPatternAndBump_When_Updating()
    {
        CategoryRule? saved = null;
        _rules.Setup(r => r.UpdateAsync(It.IsAny<CategoryRule>())).Callback<CategoryRule>(r => saved = r).ReturnsAsync(true);

        var result = await BuildSut().UpdateAsync(Rule(" uber eats ", Salary.Id, CategoryRuleMatchType.Exact, id: 3));

        Assert.True(result.Value);
        Assert.Equal(("uber eats", CategoryRuleMatchType.Exact, Salary.Id), (saved!.Pattern, saved.MatchType, saved.CategoryId));
        Assert.Equal(["bump"], _calls);
    }

    [Fact]
    public async Task Should_ReturnTheDuplicateMessage_When_AnUpdateCollides()
    {
        _rules.Setup(r => r.UpdateAsync(It.IsAny<CategoryRule>()))
            .ThrowsAsync(new UniqueNameViolationException("UQ_CategoryRules_User_Pattern", new Exception()));

        var result = await BuildSut().UpdateAsync(Rule("uber", id: 3));

        Assert.Equal("A rule for \"uber\" already exists", result.Error);
    }

    [Fact]
    public async Task Should_RejectAnInvalidUpdate_WithoutWriting()
    {
        var result = await BuildSut().UpdateAsync(Rule("uber", categoryId: 99, id: 3));

        Assert.Equal("Category not found", result.Error);
        _rules.Verify(r => r.UpdateAsync(It.IsAny<CategoryRule>()), Times.Never);
    }

    [Fact]
    public async Task Should_DeleteAndBump()
    {
        _rules.Setup(r => r.DeleteAsync(3, UserId)).ReturnsAsync(true);

        var result = await BuildSut().DeleteAsync(3, UserId);

        Assert.True(result.Value);
        Assert.Equal(["bump"], _calls);
    }

    [Fact]
    public async Task Should_ReportNotFound_When_DeletingAMissingRule()
    {
        _rules.Setup(r => r.DeleteAsync(3, UserId)).ReturnsAsync(false);

        var result = await BuildSut().DeleteAsync(3, UserId);

        Assert.Equal("Rule not found", result.Error);
        Assert.Empty(_calls);
    }
}
