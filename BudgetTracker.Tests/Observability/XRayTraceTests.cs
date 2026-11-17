using BudgetTracker.Server.Utilities.Observability;

namespace BudgetTracker.Tests.Observability;

public class XRayTraceTests
{
    private const string Root = "1-5759e988-bd862e3fe1be46a994272793";

    [Theory]
    [InlineData("Root=1-5759e988-bd862e3fe1be46a994272793;Parent=53995c3f42cd8ad8;Sampled=1")]
    [InlineData("Root=1-5759e988-bd862e3fe1be46a994272793")]
    [InlineData("Self=1-67891233-abcdef012345678912345678;Root=1-5759e988-bd862e3fe1be46a994272793;Sampled=0")]
    [InlineData("Sampled=1; Root=1-5759e988-bd862e3fe1be46a994272793 ;Parent=53995c3f42cd8ad8")]
    public void Should_ReadTheRootTraceId_FromALambdaTraceHeader(string header) =>
        Assert.Equal(Root, XRayTrace.RootId(header));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Parent=53995c3f42cd8ad8;Sampled=1")] // no Root
    [InlineData("Root=not-a-trace-id")]
    [InlineData("Root=2-5759e988-bd862e3fe1be46a994272793")] // unknown version
    [InlineData("Root=1-5759e988-bd862e3fe1be46a99427279")] // one digit short
    [InlineData("Root=1-5759E988-BD862E3FE1BE46A994272793")] // X-Ray IDs are lower-case hex
    [InlineData("Root=1-5759e988-bd862e3fe1be46a994272793\nforged=line")] // nothing unvalidated reaches the logs
    public void Should_ReturnNull_When_TheHeaderHasNoWellFormedRoot(string? header) =>
        Assert.Null(XRayTrace.RootId(header));
}
