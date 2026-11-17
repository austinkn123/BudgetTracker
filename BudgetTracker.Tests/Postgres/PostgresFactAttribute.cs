namespace BudgetTracker.Tests.Postgres;

/// <summary>
/// A <see cref="FactAttribute"/> for tests that need a real PostgreSQL server (see <see cref="PostgresFixture"/>).
/// When the server is unreachable the test is reported as <b>Skipped</b> with the reason, unless
/// <c>BUDGETTRACKER_REQUIRE_POSTGRES</c> is <c>1</c> or <c>true</c>, in which case it runs and fails in the fixture.
/// </summary>
/// <remarks>
/// xUnit 2 has no runtime skip, so <see cref="FactAttribute.Skip"/> is set while the attribute is constructed
/// (discovery). That needs no extra package and no per-test guard call that a new test could forget.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class PostgresFactAttribute : FactAttribute
{
    /// <summary>Marks the test as skipped when the configured server does not answer and is not required.</summary>
    public PostgresFactAttribute()
    {
        if (!PostgresTestServer.IsRequired && PostgresTestServer.UnreachableReason is { } reason)
            Skip = reason;
    }
}
