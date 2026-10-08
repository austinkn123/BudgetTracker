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
public class CategoryAccessorTests : IDisposable
{
    private const string NameIndex = "UQ_Categories_User_Name";
    private readonly FailingSaveInterceptor _interceptor = new();
    private readonly BudgetTrackerDbContext _context;

    public CategoryAccessorTests()
    {
        var options = new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(_interceptor)
            .Options;
        _context = new BudgetTrackerDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    private CategoryAccessor BuildSut() => new(_context);

    private static Category BuildCategory() => new() { UserId = 5, Name = "Rent", CategoryType = "Expense" };

    [Fact]
    public async Task Create_NameIndexViolated_ThrowsUniqueNameViolation()
    {
        _interceptor.FailNextSaveWith(PostgresErrorCodes.UniqueViolation, NameIndex);

        var ex = await Assert.ThrowsAsync<UniqueNameViolationException>(() => BuildSut().CreateAsync(BuildCategory()));

        Assert.Equal(NameIndex, ex.ConstraintName);
        Assert.IsType<DbUpdateException>(ex.InnerException);
    }

    [Fact]
    public async Task Update_NameIndexViolated_ThrowsUniqueNameViolation()
    {
        var category = BuildCategory();
        await BuildSut().CreateAsync(category);
        _interceptor.FailNextSaveWith(PostgresErrorCodes.UniqueViolation, NameIndex);

        await Assert.ThrowsAsync<UniqueNameViolationException>(() => BuildSut().UpdateAsync(category));
    }

    [Fact]
    public async Task Create_OtherUniqueIndexViolated_RethrowsOriginalException()
    {
        _interceptor.FailNextSaveWith(PostgresErrorCodes.UniqueViolation, "UQ_Categories_User_PlaidCategory");

        await Assert.ThrowsAsync<DbUpdateException>(() => BuildSut().CreateAsync(BuildCategory()));
    }

    [Fact]
    public async Task Create_OtherSqlStateOnNameIndex_RethrowsOriginalException()
    {
        _interceptor.FailNextSaveWith(PostgresErrorCodes.ForeignKeyViolation, NameIndex);

        await Assert.ThrowsAsync<DbUpdateException>(() => BuildSut().CreateAsync(BuildCategory()));
    }
}
