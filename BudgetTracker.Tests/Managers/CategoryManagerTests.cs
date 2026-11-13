using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using BudgetTracker.Server.Managers;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace BudgetTracker.Tests.Managers;

public class CategoryManagerTests
{
    private readonly Mock<ICategoryAccessor> _accessor = new(MockBehavior.Strict);
    private readonly CategoryEngine _engine = new();
    private readonly Mock<ITransactionAccessor> _txnAccessor = new(MockBehavior.Strict);
    private readonly Mock<IDataVersionAccessor> _dataVersion = new(MockBehavior.Strict);
    private readonly List<string> _calls = [];

    public CategoryManagerTests()
    {
        _accessor.Setup(a => a.GetOtherNamesAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync([]);
        _dataVersion.Setup(d => d.BumpAsync(It.IsAny<int>()))
            .Callback<int>(userId => _calls.Add($"bump:{userId}"))
            .ReturnsAsync(true);
    }

    private CategoryManager BuildSut() => new(_engine, _accessor.Object, _txnAccessor.Object, new PlaidEngine(), _dataVersion.Object);

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

    [Fact]
    public async Task Create_NameTakenIgnoringCase_ReturnsDuplicateErrorAndDoesNotPersist()
    {
        _accessor.Setup(a => a.GetOtherNamesAsync(5, 0)).ReturnsAsync(["food & dining"]);
        var incoming = BuildMappedCategory();
        incoming.Name = "Food & Dining";

        var result = await BuildSut().CreateAsync(incoming);

        Assert.False(result.IsSuccess);
        Assert.Equal("A category named \"Food & Dining\" already exists", result.Error);
        _accessor.Verify(a => a.CreateAsync(It.IsAny<Category>()), Times.Never);
    }

    [Fact]
    public async Task Create_ComparesAgainstEveryCategoryOfTheUser()
    {
        _accessor.Setup(a => a.CreateAsync(It.IsAny<Category>())).ReturnsAsync(12);

        await BuildSut().CreateAsync(BuildMappedCategory());

        _accessor.Verify(a => a.GetOtherNamesAsync(5, 0), Times.Once);
    }

    [Fact]
    public async Task Create_InvalidCategory_DoesNotFetchNames()
    {
        var incoming = BuildMappedCategory();
        incoming.Name = "";

        var result = await BuildSut().CreateAsync(incoming);

        Assert.Equal("Category name is required", result.Error);
        _accessor.Verify(a => a.GetOtherNamesAsync(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Update_NameTakenByAnotherCategoryIgnoringCase_ReturnsDuplicateErrorAndDoesNotPersist()
    {
        _accessor.Setup(a => a.GetByIdForUserAsync(8, 5)).ReturnsAsync(BuildMappedCategory());
        _accessor.Setup(a => a.GetOtherNamesAsync(5, 8)).ReturnsAsync(["RENT"]);
        var incoming = BuildMappedCategory();
        incoming.Name = "Rent";

        var result = await BuildSut().UpdateAsync(incoming);

        Assert.False(result.IsSuccess);
        Assert.Equal("A category named \"Rent\" already exists", result.Error);
        _accessor.Verify(a => a.UpdateAsync(It.IsAny<Category>()), Times.Never);
    }

    [Fact]
    public async Task Create_ConcurrentInsertWinsTheUniqueIndex_ReturnsDuplicateError()
    {
        // Both requests passed the name pre-check; the index rejects the second one.
        _accessor.Setup(a => a.CreateAsync(It.IsAny<Category>()))
            .ThrowsAsync(new UniqueNameViolationException("UQ_Categories_User_Name", new Exception("23505")));
        var incoming = BuildMappedCategory();
        incoming.Name = "Rent";

        var result = await BuildSut().CreateAsync(incoming);

        Assert.False(result.IsSuccess);
        Assert.Equal("A category named \"Rent\" already exists", result.Error);
    }

    [Fact]
    public async Task Update_ConcurrentSaveWinsTheUniqueIndex_ReturnsDuplicateError()
    {
        _accessor.Setup(a => a.GetByIdForUserAsync(8, 5)).ReturnsAsync(BuildMappedCategory());
        _accessor.Setup(a => a.UpdateAsync(It.IsAny<Category>()))
            .ThrowsAsync(new UniqueNameViolationException("UQ_Categories_User_Name", new Exception("23505")));
        var incoming = BuildMappedCategory();
        incoming.Name = "Rent";

        var result = await BuildSut().UpdateAsync(incoming);

        Assert.False(result.IsSuccess);
        Assert.Equal("A category named \"Rent\" already exists", result.Error);
    }

    [Fact]
    public async Task Update_ChangingOnlyTheCaseOfItsOwnName_Succeeds()
    {
        var existing = BuildMappedCategory();
        _accessor.Setup(a => a.GetByIdForUserAsync(8, 5)).ReturnsAsync(existing);
        _accessor.Setup(a => a.UpdateAsync(It.IsAny<Category>())).ReturnsAsync(true);
        var incoming = BuildMappedCategory();
        incoming.Name = incoming.Name.ToUpperInvariant();

        var result = await BuildSut().UpdateAsync(incoming);

        Assert.True(result.IsSuccess);
        _accessor.Verify(a => a.GetOtherNamesAsync(5, 8), Times.Once);
    }

    // ── Changing the type re-classifies the category's rows (the user's override) ─

    private static Transaction Row(int id, string type, string? plaidCategory) => new()
    {
        Id = id,
        AccountId = 1,
        CategoryId = 8,
        TransactionType = type,
        Amount = -10m,
        PlaidCategoryPrimary = plaidCategory
    };

    private void StubTypeChange(params Transaction[] rows)
    {
        _accessor.Setup(a => a.GetByIdForUserAsync(8, 5)).ReturnsAsync(BuildMappedCategory());
        _accessor.Setup(a => a.UpdateAsync(It.IsAny<Category>())).ReturnsAsync(true);
        var byCategory = new TransactionFilter(CategoryId: 8);
        _txnAccessor.Setup(a => a.GetFilteredAsync(5, byCategory)).ReturnsAsync(rows);
    }

    [Fact]
    public async Task Update_TypeChangedToIncome_ReclassifiesItsRows_ButPairedTransfersStay()
    {
        StubTypeChange(Row(1, "Expense", "TRANSFER_IN"), Row(2, "Transfer", "TRANSFER_IN"), Row(3, "Adjustment", null));
        IReadOnlyDictionary<int, string>? written = null;
        _txnAccessor.Setup(a => a.SetCategoryAsync(It.IsAny<IReadOnlyDictionary<int, string>>(), 8, 5))
            .Callback<IReadOnlyDictionary<int, string>, int?, int>((types, _, _) => written = types)
            .ReturnsAsync(3);
        var incoming = BuildMappedCategory();
        incoming.CategoryType = "Income";

        var result = await BuildSut().UpdateAsync(incoming);

        Assert.True(result.IsSuccess);
        Assert.Equal("Income", written![1]);
        Assert.Equal("Transfer", written[2]);
        Assert.Equal("Income", written[3]);
    }

    [Fact]
    public async Task Update_TypeChangedToBoth_FallsBackToTheAutomaticRule()
    {
        StubTypeChange(Row(1, "Expense", "INCOME"), Row(2, "Expense", "FOOD_AND_DRINK"));
        IReadOnlyDictionary<int, string>? written = null;
        _txnAccessor.Setup(a => a.SetCategoryAsync(It.IsAny<IReadOnlyDictionary<int, string>>(), 8, 5))
            .Callback<IReadOnlyDictionary<int, string>, int?, int>((types, _, _) => written = types)
            .ReturnsAsync(2);
        var incoming = BuildMappedCategory();
        incoming.CategoryType = "Both";

        await BuildSut().UpdateAsync(incoming);

        Assert.Equal("Income", written![1]);
        Assert.Equal("Expense", written[2]);
    }

    [Fact]
    public async Task Update_TypeUnchanged_LeavesTransactionsAlone()
    {
        _accessor.Setup(a => a.GetByIdForUserAsync(8, 5)).ReturnsAsync(BuildMappedCategory());
        _accessor.Setup(a => a.UpdateAsync(It.IsAny<Category>())).ReturnsAsync(true);
        var incoming = BuildMappedCategory();
        incoming.Name = "Food";

        var result = await BuildSut().UpdateAsync(incoming);

        Assert.True(result.IsSuccess);
        _txnAccessor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Update_TypeChangeRejected_LeavesTransactionsAlone()
    {
        _accessor.Setup(a => a.GetByIdForUserAsync(8, 5)).ReturnsAsync(BuildMappedCategory());
        _accessor.Setup(a => a.GetOtherNamesAsync(5, 8)).ReturnsAsync(["RENT"]);
        var incoming = BuildMappedCategory();
        incoming.Name = "Rent";
        incoming.CategoryType = "Income";

        var result = await BuildSut().UpdateAsync(incoming);

        Assert.False(result.IsSuccess);
        _txnAccessor.VerifyNoOtherCalls();
    }

    // ── Usage ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnTheAccessorsUsage_ForTheUserAndInstant()
    {
        var since = new DateTime(2026, 9, 1);
        IReadOnlyList<CategoryUsage> usage = [new CategoryUsage(3, 4, 5m, -20m)];
        _txnAccessor.Setup(a => a.GetCategoryUsageAsync(5, since)).ReturnsAsync(usage);

        var result = await BuildSut().GetUsageAsync(5, since);

        Assert.True(result.IsSuccess);
        Assert.Same(usage, result.Value);
    }

    // ── Data version (live refresh) ─────────────────────────────────────────

    private void VerifyNeverBumped() => _dataVersion.Verify(d => d.BumpAsync(It.IsAny<int>()), Times.Never);

    [Fact]
    public async Task Create_Persisted_BumpsTheDataVersionAfterTheWrite()
    {
        _accessor.Setup(a => a.CreateAsync(It.IsAny<Category>())).Callback(() => _calls.Add("write")).ReturnsAsync(12);

        var result = await BuildSut().CreateAsync(BuildMappedCategory());

        Assert.True(result.IsSuccess);
        Assert.Equal(["write", "bump:5"], _calls);
    }

    [Fact]
    public async Task Create_InvalidOrDuplicate_DoesNotBump()
    {
        var invalid = BuildMappedCategory();
        invalid.Name = "";
        await BuildSut().CreateAsync(invalid);

        _accessor.Setup(a => a.GetOtherNamesAsync(5, 0)).ReturnsAsync(["food (groceries + eating out)"]);
        await BuildSut().CreateAsync(BuildMappedCategory());

        VerifyNeverBumped();
    }

    [Fact]
    public async Task Create_LosesTheUniqueIndexRace_DoesNotBump()
    {
        _accessor.Setup(a => a.CreateAsync(It.IsAny<Category>()))
            .ThrowsAsync(new UniqueNameViolationException("UQ_Categories_User_Name", new Exception("23505")));

        await BuildSut().CreateAsync(BuildMappedCategory());

        VerifyNeverBumped();
    }

    [Fact]
    public async Task Update_Persisted_BumpsTheDataVersionAfterTheWrite()
    {
        _accessor.Setup(a => a.GetByIdForUserAsync(8, 5)).ReturnsAsync(BuildMappedCategory());
        _accessor.Setup(a => a.UpdateAsync(It.IsAny<Category>())).Callback(() => _calls.Add("write")).ReturnsAsync(true);
        var incoming = BuildMappedCategory();
        incoming.Name = "Food";

        var result = await BuildSut().UpdateAsync(incoming);

        Assert.True(result.IsSuccess);
        Assert.Equal(["write", "bump:5"], _calls);
    }

    [Fact]
    public async Task Update_TypeChanged_BumpsOnceAfterTheRowsAreRetyped()
    {
        StubTypeChange(Row(1, "Expense", "TRANSFER_IN"));
        _accessor.Setup(a => a.UpdateAsync(It.IsAny<Category>())).Callback(() => _calls.Add("write")).ReturnsAsync(true);
        _txnAccessor.Setup(a => a.SetCategoryAsync(It.IsAny<IReadOnlyDictionary<int, string>>(), 8, 5))
            .Callback(() => _calls.Add("retype"))
            .ReturnsAsync(1);
        var incoming = BuildMappedCategory();
        incoming.CategoryType = "Income";

        var result = await BuildSut().UpdateAsync(incoming);

        Assert.True(result.IsSuccess);
        Assert.Equal(["write", "retype", "bump:5"], _calls);
    }

    [Fact]
    public async Task Update_RetypeThrowsAfterTheCategorySaved_StillBumpsOnce_AndRethrows()
    {
        StubTypeChange(Row(1, "Expense", "TRANSFER_IN"));
        _accessor.Setup(a => a.UpdateAsync(It.IsAny<Category>())).Callback(() => _calls.Add("write")).ReturnsAsync(true);
        _txnAccessor.Setup(a => a.SetCategoryAsync(It.IsAny<IReadOnlyDictionary<int, string>>(), 8, 5))
            .Callback(() => _calls.Add("retype"))
            .ThrowsAsync(new InvalidOperationException("db blip"));
        var incoming = BuildMappedCategory();
        incoming.CategoryType = "Income";

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildSut().UpdateAsync(incoming));

        Assert.Equal(["write", "retype", "bump:5"], _calls);
    }

    [Fact]
    public async Task Update_LoadingRowsToRetypeThrowsAfterTheCategorySaved_StillBumpsOnce_AndRethrows()
    {
        StubTypeChange();
        _accessor.Setup(a => a.UpdateAsync(It.IsAny<Category>())).Callback(() => _calls.Add("write")).ReturnsAsync(true);
        var byCategory = new TransactionFilter(CategoryId: 8);
        _txnAccessor.Setup(a => a.GetFilteredAsync(5, byCategory))
            .ThrowsAsync(new InvalidOperationException("db blip"));
        var incoming = BuildMappedCategory();
        incoming.CategoryType = "Income";

        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildSut().UpdateAsync(incoming));

        Assert.Equal(["write", "bump:5"], _calls);
    }

    [Fact]
    public async Task Update_NotFoundDuplicateOrUnsaved_DoesNotBump()
    {
        _accessor.Setup(a => a.GetByIdForUserAsync(8, 5)).ReturnsAsync((Category?)null);
        await BuildSut().UpdateAsync(BuildMappedCategory());

        _accessor.Setup(a => a.GetByIdForUserAsync(8, 5)).ReturnsAsync(BuildMappedCategory());
        _accessor.Setup(a => a.GetOtherNamesAsync(5, 8)).ReturnsAsync(["food (groceries + eating out)"]);
        await BuildSut().UpdateAsync(BuildMappedCategory());

        _accessor.Setup(a => a.GetOtherNamesAsync(5, 8)).ReturnsAsync([]);
        _accessor.Setup(a => a.UpdateAsync(It.IsAny<Category>())).ReturnsAsync(false);
        await BuildSut().UpdateAsync(BuildMappedCategory());

        _accessor.Setup(a => a.UpdateAsync(It.IsAny<Category>()))
            .ThrowsAsync(new UniqueNameViolationException("UQ_Categories_User_Name", new Exception("23505")));
        await BuildSut().UpdateAsync(BuildMappedCategory());

        VerifyNeverBumped();
    }

    [Fact]
    public async Task Delete_Deleted_BumpsTheDataVersionAfterTheWrite()
    {
        _accessor.Setup(a => a.DeleteAsync(8, 5)).Callback(() => _calls.Add("write")).ReturnsAsync(true);

        var result = await BuildSut().DeleteAsync(8, 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(["write", "bump:5"], _calls);
    }

    [Fact]
    public async Task Delete_NotFoundOrInUse_DoesNotBump()
    {
        _accessor.Setup(a => a.DeleteAsync(99, 5)).ReturnsAsync(false);
        _accessor.Setup(a => a.DeleteAsync(8, 5)).ThrowsAsync(new DbUpdateException("FK"));

        await BuildSut().DeleteAsync(99, 5);
        await BuildSut().DeleteAsync(8, 5);

        VerifyNeverBumped();
    }
}
