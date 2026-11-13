using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Tests.Engines;

public class TransactionEngineTests
{
    private readonly TransactionEngine _sut = new();

    private static Transaction Row(int id, DateTime occurredAt) => new() { Id = id, OccurredAt = occurredAt };

    // ── HasAnyFilter ────────────────────────────────────────────────────────

    [Fact]
    public void Should_ReportNoFilter_When_EveryFieldIsUnsetOrBlank()
    {
        Assert.False(_sut.HasAnyFilter(new TransactionFilter()));
        Assert.False(_sut.HasAnyFilter(new TransactionFilter(Search: "   ", PlaidAccountId: "")));
    }

    public static TheoryData<TransactionFilter> SingleFilters => new()
    {
        new TransactionFilter(From: new DateTime(2026, 1, 1)),
        new TransactionFilter(To: new DateTime(2026, 1, 1)),
        new TransactionFilter(Uncategorized: false),
        new TransactionFilter(IsImported: false),
        new TransactionFilter(IsPending: false),
        new TransactionFilter(Search: "rent"),
        new TransactionFilter(PlaidAccountId: "chk"),
        new TransactionFilter(CategoryId: 3)
    };

    [Theory]
    [MemberData(nameof(SingleFilters))]
    public void Should_ReportAFilter_When_AnySingleFieldIsSet(TransactionFilter filter) =>
        Assert.True(_sut.HasAnyFilter(filter));

    // ── ResolvePageSize ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, 50)]
    [InlineData(1, 1)]
    [InlineData(37, 37)]
    [InlineData(200, 200)]
    [InlineData(201, 200)]
    [InlineData(int.MaxValue, 200)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(int.MinValue, 1)]
    public void Should_DefaultAndClampThePageSize(int? requested, int expected) =>
        Assert.Equal(expected, _sut.ResolvePageSize(requested));

    // ── Cursor ──────────────────────────────────────────────────────────────

    [Fact]
    public void Should_RoundTripACursor_When_EncodedThenParsed()
    {
        var position = new TransactionCursor(new DateTime(2026, 10, 3, 14, 5, 6, 123).AddTicks(4560), 98765);

        var parsed = _sut.ParseCursor(_sut.EncodeCursor(position));

        Assert.True(parsed.IsSuccess);
        Assert.Equal(position, parsed.Value);
        Assert.Equal(DateTimeKind.Unspecified, parsed.Value!.OccurredAt.Kind);
    }

    [Fact]
    public void Should_EncodeAUrlSafeCursor()
    {
        var cursor = _sut.EncodeCursor(new TransactionCursor(DateTime.MaxValue, int.MaxValue));

        Assert.DoesNotContain('+', cursor);
        Assert.DoesNotContain('/', cursor);
        Assert.DoesNotContain('=', cursor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_StartAtTheFirstPage_When_CursorIsAbsent(string? cursor)
    {
        var parsed = _sut.ParseCursor(cursor);

        Assert.True(parsed.IsSuccess);
        Assert.Null(parsed.Value);
    }

    [Theory]
    [InlineData("not a cursor!")]     // not base64url
    [InlineData("AAAA")]              // decodes, wrong length
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // decodes, too long
    public void Should_RejectACursor_When_ItIsNotOneWeIssued(string cursor)
    {
        var parsed = _sut.ParseCursor(cursor);

        Assert.False(parsed.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(parsed.Error));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_RejectACursor_When_ItsIdIsNotPositive(int id)
    {
        var forged = _sut.EncodeCursor(new TransactionCursor(new DateTime(2026, 1, 1), id));

        Assert.False(_sut.ParseCursor(forged).IsSuccess);
    }

    [Fact]
    public void Should_RejectACursor_When_ItsTicksAreOutOfRange()
    {
        var bytes = new byte[12];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 8), long.MaxValue);
        BitConverter.TryWriteBytes(bytes.AsSpan(8, 4), 1);
        var forged = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        Assert.False(_sut.ParseCursor(forged).IsSuccess);
    }

    // ── BuildPage ───────────────────────────────────────────────────────────

    [Fact]
    public void Should_ReturnNoNextCursor_When_FewerRowsThanThePageSizeCameBack()
    {
        var page = _sut.BuildPage([Row(2, new DateTime(2026, 10, 2)), Row(1, new DateTime(2026, 10, 1))], pageSize: 3);

        Assert.Equal([2, 1], page.Items.Select(t => t.Id));
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public void Should_ReturnNoNextCursor_When_ExactlyAPageCameBack()
    {
        var page = _sut.BuildPage([Row(2, new DateTime(2026, 10, 2)), Row(1, new DateTime(2026, 10, 1))], pageSize: 2);

        Assert.Equal(2, page.Items.Count);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public void Should_DropTheLookaheadRowAndPointTheCursorAtTheLastKeptRow_When_MoreRowsExist()
    {
        var page = _sut.BuildPage(
            [Row(3, new DateTime(2026, 10, 3)), Row(2, new DateTime(2026, 10, 2)), Row(1, new DateTime(2026, 10, 1))],
            pageSize: 2);

        Assert.Equal([3, 2], page.Items.Select(t => t.Id));
        Assert.NotNull(page.NextCursor);
        Assert.Equal(new TransactionCursor(new DateTime(2026, 10, 2), 2), _sut.ParseCursor(page.NextCursor).Value);
    }

    [Fact]
    public void Should_ReturnAnEmptyPage_When_NothingCameBack()
    {
        var page = _sut.BuildPage([], pageSize: 50);

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }
}
