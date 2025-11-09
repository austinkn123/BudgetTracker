namespace BudgetTracker.Server.Endpoints;

public static class WeatherEndpoints
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    public static RouteGroupBuilder MapWeatherEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetWeatherForecast)
            .WithName("GetWeatherForecast")
            .WithOpenApi();

        // Add more weather endpoints here
        // group.MapGet("/{id}", GetWeatherById)
        //     .WithName("GetWeatherById")
        //     .WithOpenApi();

        return group;
    }

    private static IEnumerable<WeatherForecast> GetWeatherForecast(ILogger<Program> logger)
    {
        var forecast = Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        })
        .ToArray();

        return forecast;
    }
}
