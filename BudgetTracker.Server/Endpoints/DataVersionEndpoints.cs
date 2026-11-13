using BudgetTracker.Domain.Interfaces.Managers;

namespace BudgetTracker.Server.Endpoints;

/// <summary>Body of <c>GET /api/data-version</c>; serializes as <c>{"version":N}</c>.</summary>
/// <param name="Version">The user's current data version.</param>
public record DataVersionResponse(long Version);

/// <summary>The live-refresh poll endpoint.</summary>
public static class DataVersionEndpoints
{
    /// <summary>Maps <c>GET /</c> on the data-version group.</summary>
    /// <param name="dataVersionGroup">The <c>/api/data-version</c> route group.</param>
    /// <returns>The same group, for chaining.</returns>
    public static IEndpointRouteBuilder MapDataVersionEndpoints(this IEndpointRouteBuilder dataVersionGroup)
    {
        dataVersionGroup.MapGet("/", GetAsync);

        return dataVersionGroup;
    }

    /// <summary>
    /// <c>GET /api/data-version</c> — the caller's data version, polled every 20 seconds. Reads the sub claim directly
    /// instead of taking <c>ICurrentUserProvider</c>, which provisions a user on first touch: the poll must never write.
    /// 401 when the token carries no sub.
    /// </summary>
    /// <param name="context">The request, for its claims, abort token and response headers.</param>
    /// <param name="manager">Data version workflow.</param>
    /// <returns>The HTTP result.</returns>
    public static async Task<IResult> GetAsync(HttpContext context, IDataVersionManager manager)
    {
        context.Response.Headers.CacheControl = "no-store";

        // MapInboundClaims is off (Program.cs), so the JWT's "sub" is not renamed to NameIdentifier.
        var sub = context.User.FindFirst("sub")?.Value;
        if (sub is null)
            return Results.Unauthorized();

        var version = await manager.GetVersionAsync(sub, context.RequestAborted);
        return Results.Ok(new DataVersionResponse(version));
    }
}
