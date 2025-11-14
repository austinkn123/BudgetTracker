using BudgetTracker.Application.Interfaces;
using BudgetTracker.Infrastructure.Data;
using BudgetTracker.Infrastructure.Repositories;
using BudgetTracker.Server.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<DapperContext>();
builder.Services.AddScoped<IExpenseRepository, ExpenseRepository>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddAuthorization();

var app = builder.Build();


app.UseDefaultFiles();
app.MapStaticAssets();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

// Map endpoint groups
var apiGroup = app.MapGroup("/api")
    .WithOpenApi();
    // .RequireAuthorization(); // Uncomment to require authentication for all API endpoints
    // .AddEndpointFilter<LoggingFilter>(); // Add custom filters

// Weather endpoints group
apiGroup.MapGroup("/weather")
    .MapWeatherEndpoints()
    .WithTags("Weather");
    // .RequireAuthorization(); // Uncomment to require authentication for weather endpoints only

// Expense endpoints group
var expenseGroup = apiGroup.MapGroup("/expenses")
    .WithTags("Expenses");
expenseGroup.MapExpenseEndpoints();

app.MapFallbackToFile("/index.html");

app.Run();
