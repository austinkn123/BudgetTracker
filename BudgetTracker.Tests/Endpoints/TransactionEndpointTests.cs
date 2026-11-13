using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Models;
using BudgetTracker.Server.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace BudgetTracker.Tests.Endpoints;

public class TransactionEndpointTests
{
    private readonly Mock<ITransactionManager> _manager = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUserProvider> _currentUser = new();

    public TransactionEndpointTests()
    {
        _currentUser.SetupGet(u => u.UserId).Returns(5);
    }

    // ── GET /api/transactions ───────────────────────────────────────────────

    [Fact]
    public async Task Should_Return400PointingAtThePagedRead_When_NoFilterIsGiven()
    {
        var none = new TransactionFilter();
        _manager.Setup(m => m.GetFilteredAsync(5, none))
            .ReturnsAsync(Result<IEnumerable<Transaction>>.Failure("Use GET /api/transactions/page"));

        var result = await TransactionEndpoints.GetAsync(
            _manager.Object, _currentUser.Object, null, null, null, null, null, null, null, null);

        Assert.Equal(StatusCodes.Status400BadRequest, EndpointAssert.StatusOf(result));
        Assert.Contains("/api/transactions/page", EndpointAssert.ErrorOf(result));
    }

    [Fact]
    public async Task Should_ReturnTheFilteredArray_When_AFilterIsGiven()
    {
        var from = new DateTime(2026, 10, 1);
        var expected = new TransactionFilter(from, null, true, null, null, "rent", "chk", 3);
        Transaction[] rows = [new() { Id = 1 }];
        _manager.Setup(m => m.GetFilteredAsync(5, expected)).ReturnsAsync(Result<IEnumerable<Transaction>>.Success(rows));

        var result = await TransactionEndpoints.GetAsync(
            _manager.Object, _currentUser.Object, from, null, true, null, null, "rent", "chk", 3);

        var ok = Assert.IsType<Ok<IEnumerable<Transaction>>>(result);
        Assert.Same(rows, ok.Value);
    }

    // ── GET /api/transactions/page ──────────────────────────────────────────

    [Fact]
    public async Task Should_ReturnThePage_WithEveryFilterCursorAndLimitPassedThrough()
    {
        var to = new DateTime(2026, 10, 31);
        var expected = new TransactionFilter(null, to, null, true, false, null, "chk", null);
        var page = new TransactionPage([new Transaction { Id = 1 }], "next");
        _manager.Setup(m => m.GetPageAsync(5, expected, "cur", 25)).ReturnsAsync(Result<TransactionPage>.Success(page));

        var result = await TransactionEndpoints.GetPageAsync(
            _manager.Object, _currentUser.Object, null, to, null, true, false, null, "chk", null, "cur", 25);

        var ok = Assert.IsType<Ok<TransactionPage>>(result);
        Assert.Same(page, ok.Value);
    }

    [Fact]
    public async Task Should_Return400_When_TheCursorIsInvalid()
    {
        _manager.Setup(m => m.GetPageAsync(5, It.IsAny<TransactionFilter>(), "bad", null))
            .ReturnsAsync(Result<TransactionPage>.Failure("Invalid cursor"));

        var result = await TransactionEndpoints.GetPageAsync(
            _manager.Object, _currentUser.Object, null, null, null, null, null, null, null, null, "bad", null);

        Assert.Equal(StatusCodes.Status400BadRequest, EndpointAssert.StatusOf(result));
        Assert.Equal("Invalid cursor", EndpointAssert.ErrorOf(result));
    }

    // ── Zoned from/to ───────────────────────────────────────────────────────

    // "2026-10-01T00:00:00Z" binds as Kind=Utc, which Npgsql refuses against the timestamp-without-time-zone column.
    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public async Task Should_PassTheSameWallClockAsUnspecifiedKind_When_GetIsGivenZonedDates(DateTimeKind kind)
    {
        TransactionFilter? passed = null;
        _manager.Setup(m => m.GetFilteredAsync(5, It.IsAny<TransactionFilter>()))
            .Callback<int, TransactionFilter>((_, f) => passed = f)
            .ReturnsAsync(Result<IEnumerable<Transaction>>.Success([]));

        var result = await TransactionEndpoints.GetAsync(
            _manager.Object, _currentUser.Object,
            new DateTime(2026, 10, 1, 0, 0, 0, kind), new DateTime(2026, 10, 31, 23, 30, 0, kind),
            null, null, null, null, null, null);

        Assert.Equal(StatusCodes.Status200OK, EndpointAssert.StatusOf(result));
        AssertUnzoned(new DateTime(2026, 10, 1), passed!.From);
        AssertUnzoned(new DateTime(2026, 10, 31, 23, 30, 0), passed.To);
    }

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public async Task Should_PassTheSameWallClockAsUnspecifiedKind_When_GetPageIsGivenZonedDates(DateTimeKind kind)
    {
        TransactionFilter? passed = null;
        _manager.Setup(m => m.GetPageAsync(5, It.IsAny<TransactionFilter>(), null, null))
            .Callback<int, TransactionFilter, string?, int?>((_, f, _, _) => passed = f)
            .ReturnsAsync(Result<TransactionPage>.Success(new TransactionPage([], null)));

        var result = await TransactionEndpoints.GetPageAsync(
            _manager.Object, _currentUser.Object,
            new DateTime(2026, 10, 1, 0, 0, 0, kind), new DateTime(2026, 10, 31, 0, 0, 0, kind),
            null, null, null, null, null, null, null, null);

        Assert.Equal(StatusCodes.Status200OK, EndpointAssert.StatusOf(result));
        AssertUnzoned(new DateTime(2026, 10, 1), passed!.From);
        AssertUnzoned(new DateTime(2026, 10, 31), passed.To);
    }

    [Fact]
    public async Task Should_LeaveAbsentDatesNull_When_GetPageIsGivenNoDates()
    {
        TransactionFilter? passed = null;
        _manager.Setup(m => m.GetPageAsync(5, It.IsAny<TransactionFilter>(), null, null))
            .Callback<int, TransactionFilter, string?, int?>((_, f, _, _) => passed = f)
            .ReturnsAsync(Result<TransactionPage>.Success(new TransactionPage([], null)));

        await TransactionEndpoints.GetPageAsync(
            _manager.Object, _currentUser.Object, null, null, null, null, null, null, null, null, null, null);

        Assert.Equal((null, null), (passed!.From, passed.To));
    }

    // DateTime equality ignores Kind, so the kind is asserted separately.
    private static void AssertUnzoned(DateTime expected, DateTime? actual) =>
        Assert.Equal((expected, DateTimeKind.Unspecified), (actual!.Value, actual.Value.Kind));

    // ── GET /api/transactions/summary ───────────────────────────────────────

    [Fact]
    public async Task Should_ReturnTheSummary()
    {
        var summary = new TransactionSummary(3, []);
        _manager.Setup(m => m.GetSummaryAsync(5)).ReturnsAsync(Result<TransactionSummary>.Success(summary));

        var result = await TransactionEndpoints.GetSummaryAsync(_manager.Object, _currentUser.Object);

        Assert.Same(summary, Assert.IsType<Ok<TransactionSummary>>(result).Value);
    }
}
