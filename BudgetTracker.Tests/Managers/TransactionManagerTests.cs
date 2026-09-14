using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using BudgetTracker.Server.Managers;
using Moq;

namespace BudgetTracker.Tests.Managers;

public class TransactionManagerTests
{
    private readonly Mock<ITransactionAccessor> _accessor = new(MockBehavior.Strict);
    private readonly Mock<ICategoryAccessor> _categoryAccessor = new(MockBehavior.Strict);

    private TransactionManager BuildSut() => new(_accessor.Object, _categoryAccessor.Object);

    // ── Bulk categorise ─────────────────────────────────────────────────────

    [Fact]
    public async Task SetCategory_ValidCategory_UpdatesOwnedRows()
    {
        _categoryAccessor.Setup(a => a.GetByIdForUserAsync(8, 5))
            .ReturnsAsync(new Category { Id = 8, UserId = 5, Name = "Food", CategoryType = "Expense" });
        _accessor.Setup(a => a.SetCategoryAsync(It.IsAny<IEnumerable<int>>(), 8, 5)).ReturnsAsync(3);

        var result = await BuildSut().SetCategoryAsync([1, 2, 3], categoryId: 8, userId: 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value);
    }

    [Fact]
    public async Task SetCategory_CategoryBelongsToAnotherUser_IsRejected()
    {
        // The row ids are scoped by owner in the accessor, but the category id comes straight from
        // the request body — so it has to be verified before being written across a batch.
        _categoryAccessor.Setup(a => a.GetByIdForUserAsync(99, 5)).ReturnsAsync((Category?)null);

        var result = await BuildSut().SetCategoryAsync([1, 2], categoryId: 99, userId: 5);

        Assert.False(result.IsSuccess);
        Assert.Equal("Category not found", result.Error);
        _accessor.Verify(a => a.SetCategoryAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int?>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SetCategory_NullCategory_ClearsWithoutLookup()
    {
        _accessor.Setup(a => a.SetCategoryAsync(It.IsAny<IEnumerable<int>>(), null, 5)).ReturnsAsync(2);

        var result = await BuildSut().SetCategoryAsync([1, 2], categoryId: null, userId: 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public async Task SetCategory_EmptySelection_IsRejected()
    {
        var result = await BuildSut().SetCategoryAsync([], categoryId: 8, userId: 5);

        Assert.False(result.IsSuccess);
        _accessor.Verify(a => a.SetCategoryAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<int?>(), It.IsAny<int>()), Times.Never);
    }

    // ── Notes ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetNotes_TrimsAndPersists()
    {
        _accessor.Setup(a => a.SetNotesAsync(4, "Split with Sam", 5)).ReturnsAsync(true);

        var result = await BuildSut().SetNotesAsync(4, "  Split with Sam  ", userId: 5);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SetNotes_BlankIsStoredAsNull()
    {
        _accessor.Setup(a => a.SetNotesAsync(4, null, 5)).ReturnsAsync(true);

        var result = await BuildSut().SetNotesAsync(4, "   ", userId: 5);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SetNotes_TooLong_IsRejectedBeforeHittingTheDatabase()
    {
        var result = await BuildSut().SetNotesAsync(4, new string('x', 1001), userId: 5);

        Assert.False(result.IsSuccess);
        _accessor.Verify(a => a.SetNotesAsync(It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task SetNotes_RowNotOwned_ReturnsNotFound()
    {
        _accessor.Setup(a => a.SetNotesAsync(99, "x", 5)).ReturnsAsync(false);

        var result = await BuildSut().SetNotesAsync(99, "x", userId: 5);

        Assert.False(result.IsSuccess);
        Assert.Equal("Transaction not found", result.Error);
    }
}
