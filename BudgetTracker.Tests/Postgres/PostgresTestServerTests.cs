namespace BudgetTracker.Tests.Postgres;

/// <summary>
/// The skip/fail decision behind <see cref="PostgresFactAttribute"/>. These need no server: they pin that a bad
/// configuration becomes a reason instead of an exception thrown during test discovery.
/// </summary>
public sealed class PostgresTestServerTests
{
    [Theory]
    [InlineData("Username=postgres;Password=postgres")]
    [InlineData("Host=;Username=postgres")]
    [InlineData("this is not a connection string")]
    [InlineData("Host=localhost;Port=notanumber")]
    public void Should_ReturnAReasonInsteadOfThrowing_When_ConnectionStringCannotBeUsed(string configured)
    {
        var reason = PostgresTestServer.ProbeServer(configured);

        Assert.False(string.IsNullOrWhiteSpace(reason));
        Assert.Contains(PostgresTestServer.ConnectionVariable, reason);
    }

    [Fact]
    public void Should_NotLeakThePassword_When_ReportingWhyTheServerIsUnreachable()
    {
        var reason = PostgresTestServer.ProbeServer("Username=postgres;Password=s3cr3t-value");

        Assert.NotNull(reason);
        Assert.DoesNotContain("s3cr3t-value", reason);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("True")]
    [InlineData(" true ")]
    public void Should_RequireTheServer_When_FlagIsOneOrTrueInAnyCasing(string value) =>
        Assert.True(PostgresTestServer.IsRequiredValue(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("0")]
    [InlineData("false")]
    [InlineData("yes")]
    [InlineData("11")]
    public void Should_NotRequireTheServer_When_FlagIsUnsetOrAnythingElse(string? value) =>
        Assert.False(PostgresTestServer.IsRequiredValue(value));
}
