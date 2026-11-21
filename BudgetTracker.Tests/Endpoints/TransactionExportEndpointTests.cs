using System.Text;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Models;
using BudgetTracker.Server.Endpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BudgetTracker.Tests.Endpoints;

public class TransactionExportEndpointTests
{
    private readonly Mock<ITransactionExportManager> _manager = new(MockBehavior.Strict);
    private readonly Mock<ICurrentUserProvider> _currentUser = new();

    public TransactionExportEndpointTests() => _currentUser.SetupGet(u => u.UserId).Returns(5);

    private static async IAsyncEnumerable<string> Lines(params string[] lines)
    {
        foreach (var line in lines)
        {
            await Task.Yield();
            yield return line;
        }
    }

    private static async Task<(HttpContext Context, byte[] Body)> ExecuteAsync(IResult result)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance).BuildServiceProvider(),
        };
        var body = new MemoryStream();
        context.Response.Body = body;
        await result.ExecuteAsync(context);
        return (context, body.ToArray());
    }

    [Fact]
    public async Task Should_StreamACsvAttachment_WithABomAndCrlfLines()
    {
        _manager.Setup(m => m.ExportCsv(5, It.IsAny<TransactionFilter>()))
            .Returns(new CsvExport("budgettracker-transactions-all.csv", Lines("Date,Payee", "2026-11-09,Café")));

        var (context, body) = await ExecuteAsync(TransactionEndpoints.Export(
            _manager.Object, _currentUser.Object, null, null, null, null, null, null, null, null));

        Assert.Equal("text/csv; charset=utf-8", context.Response.ContentType);
        Assert.Contains("attachment", context.Response.Headers.ContentDisposition.ToString());
        Assert.Contains("budgettracker-transactions-all.csv", context.Response.Headers.ContentDisposition.ToString());
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, body[..3]);
        Assert.Equal("Date,Payee\r\n2026-11-09,Café\r\n", Encoding.UTF8.GetString(body[3..]));
    }

    [Fact]
    public void Should_PassTheFeedFiltersThrough_WithUnzonedDates()
    {
        TransactionFilter? passed = null;
        _manager.Setup(m => m.ExportCsv(5, It.IsAny<TransactionFilter>()))
            .Callback<int, TransactionFilter>((_, f) => passed = f)
            .Returns(new CsvExport("x.csv", Lines()));

        TransactionEndpoints.Export(_manager.Object, _currentUser.Object,
            new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 30), true, true, false, "uber", "acct-1", 7);

        Assert.Equal(
            new TransactionFilter(new DateTime(2026, 11, 1), new DateTime(2026, 11, 30), true, true, false, "uber", "acct-1", 7),
            passed);
        Assert.Equal(DateTimeKind.Unspecified, passed!.From!.Value.Kind);
    }
}
