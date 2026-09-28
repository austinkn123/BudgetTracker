using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using BudgetTracker.Server.Managers;
using Moq;

namespace BudgetTracker.Tests.Managers;

public class CategoryManagerTests
{
    private readonly Mock<ICategoryAccessor> _accessor = new(MockBehavior.Strict);
    private readonly CategoryEngine _engine = new();

    private CategoryManager BuildSut() => new(_engine, _accessor.Object);

    private static Category BuildMappedCategory() => new()
    {
        Id = 8,
        UserId = 5,
        Name = "Food (Groceries + Eating Out)",
        CategoryType = "Expense",
        PlaidCategoryPrimary = "FOOD_AND_DRINK"
    };

    [Fact]
    public async Task Update_OmittedPlaidMapping_PreservesExistingMapping()
    {
        // The category form PUTs only {id, userId, name, categoryType}. Treating that missing value
        // as "clear it" silently destroyed auto-categorisation on every rename.
        var existing = BuildMappedCategory();
        _accessor.Setup(a => a.GetByIdForUserAsync(8, 5)).ReturnsAsync(existing);
        _accessor.Setup(a => a.UpdateAsync(It.IsAny<Category>())).ReturnsAsync(true);

        var incoming = new Category
        {
            Id = 8,
            UserId = 5,
            Name = "Food & Dining",
            CategoryType = "Expense",
            PlaidCategoryPrimary = null
        };

        var result = await BuildSut().UpdateAsync(incoming);

        Assert.True(result.IsSuccess);
        Assert.Equal("Food & Dining", existing.Name);
        Assert.Equal("FOOD_AND_DRINK", existing.PlaidCategoryPrimary);
    }

    [Fact]
    public async Task Update_ExplicitEmptyString_ClearsMapping()
    {
        var existing = BuildMappedCategory();
        _accessor.Setup(a => a.GetByIdForUserAsync(8, 5)).ReturnsAsync(existing);
        _accessor.Setup(a => a.UpdateAsync(It.IsAny<Category>())).ReturnsAsync(true);

        var incoming = BuildMappedCategory();
        incoming.PlaidCategoryPrimary = "";

        var result = await BuildSut().UpdateAsync(incoming);

        Assert.True(result.IsSuccess);
        Assert.Null(existing.PlaidCategoryPrimary);
    }

    [Fact]
    public async Task Update_NewMapping_IsTrimmedAndStored()
    {
        var existing = BuildMappedCategory();
        existing.PlaidCategoryPrimary = null;
        _accessor.Setup(a => a.GetByIdForUserAsync(8, 5)).ReturnsAsync(existing);
        _accessor.Setup(a => a.UpdateAsync(It.IsAny<Category>())).ReturnsAsync(true);

        var incoming = BuildMappedCategory();
        incoming.PlaidCategoryPrimary = "  TRANSPORTATION  ";

        var result = await BuildSut().UpdateAsync(incoming);

        Assert.True(result.IsSuccess);
        Assert.Equal("TRANSPORTATION", existing.PlaidCategoryPrimary);
    }

    [Fact]
    public async Task Create_BlankMapping_IsStoredAsNull()
    {
        _accessor.Setup(a => a.CreateAsync(It.IsAny<Category>())).ReturnsAsync(12);

        var incoming = BuildMappedCategory();
        incoming.PlaidCategoryPrimary = "   ";

        var result = await BuildSut().CreateAsync(incoming);

        Assert.True(result.IsSuccess);
        Assert.Null(incoming.PlaidCategoryPrimary);
    }

    [Fact]
    public async Task Update_MissingCategory_ReturnsNotFound()
    {
        _accessor.Setup(a => a.GetByIdForUserAsync(8, 5)).ReturnsAsync((Category?)null);

        var result = await BuildSut().UpdateAsync(BuildMappedCategory());

        Assert.False(result.IsSuccess);
        Assert.Equal("Category not found", result.Error);
    }
}
