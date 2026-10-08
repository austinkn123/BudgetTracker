using Microsoft.AspNetCore.Http;

namespace BudgetTracker.Tests.Endpoints;

/// <summary>Shared assertions for handler-level endpoint tests.</summary>
internal static class EndpointAssert
{
    /// <summary>The HTTP status code of <paramref name="result"/>.</summary>
    public static int StatusOf(IResult result) =>
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode!.Value;

    /// <summary>The <c>error</c> property of an <c>{ error }</c> response body.</summary>
    public static string? ErrorOf(IResult result)
    {
        var body = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
        Assert.NotNull(body);
        var property = body.GetType().GetProperty("error");
        Assert.NotNull(property);
        return property.GetValue(body) as string;
    }
}
