using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Models.MoneyMap;
using BudgetTracker.Server.Managers;
using Moq;

namespace BudgetTracker.Tests.Managers;

public class MoneyMapManagerTests
{
    private const int UserId = 5;
    private readonly Mock<IMoneyMapEngine> _engine = new(MockBehavior.Strict);
    private readonly Mock<ITransactionAccessor> _txnAccessor = new(MockBehavior.Strict);
    private readonly Mock<IBudgetPlanAccessor> _planAccessor = new(MockBehavior.Strict);
    private readonly Mock<ICategoryAccessor> _categoryAccessor = new(MockBehavior.Strict);

    private MoneyMapManager BuildSut() => new(_engine.Object, _txnAccessor.Object, _planAccessor.Object, _categoryAccessor.Object);

    [Fact]
    public async Task Get_HandsTheUsersPlansLedgerAndCategoriesToTheEngine()
    {
        BudgetPlan[] plans = [new() { Id = 1, IsActive = true }];
        Transaction[] rows = [new() { Id = 2 }];
        Category[] categories = [new() { Id = 3 }];
        var map = new MoneyMap("Plan", DateTime.Today, DateTime.Today, 6, 0m, 0m, [], []);
        _planAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync(plans);
        _txnAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync(rows);
        _categoryAccessor.Setup(a => a.GetByUserIdAsync(UserId)).ReturnsAsync(categories);
        _engine.Setup(e => e.Build(plans, It.Is<IReadOnlyList<Transaction>>(t => t.Single().Id == 2), categories, DateTime.UtcNow.Date, 6))
            .Returns(Result<MoneyMap>.Success(map));

        var result = await BuildSut().GetAsync(UserId, 6);

        Assert.Same(map, result.Value);
    }
}
