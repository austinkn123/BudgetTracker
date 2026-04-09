using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Server.Endpoints;
using BudgetTracker.Server.Managers;
using BudgetTracker.Server.Utilities;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<BudgetTrackerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("BudgetTrackerConnection")));

// Current user provider (swap for CognitoCurrentUserProvider when auth is added)
builder.Services.AddScoped<ICurrentUserProvider, HardcodedCurrentUserProvider>();

// Accessors (data access)
builder.Services.Scan(scan => scan
    .FromAssemblies(typeof(TransactionAccessor).Assembly)
        .AddClasses(classes => classes.Where(c => c.Name.EndsWith("Accessor")))
        .AsImplementedInterfaces()
        .WithScopedLifetime());

// Engines (business logic)
builder.Services.Scan(scan => scan
    .FromAssemblies(typeof(TransactionEngine).Assembly)
        .AddClasses(classes => classes.Where(c => c.Name.EndsWith("Engine")))
        .AsImplementedInterfaces()
        .WithScopedLifetime());

// Managers (orchestration)
builder.Services.Scan(scan => scan
    .FromAssemblies(typeof(TransactionManager).Assembly)
        .AddClasses(classes => classes.Where(c => c.Name.EndsWith("Manager")))
        .AsImplementedInterfaces()
        .WithScopedLifetime());

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

// Transaction endpoints group
var transactionGroup = apiGroup.MapGroup("/transactions")
    .WithTags("Transactions");
transactionGroup.MapTransactionEndpoints();

// Category endpoints group
var categoryGroup = apiGroup.MapGroup("/categories")
    .WithTags("Categories");
categoryGroup.MapCategoryEndpoints();

// User endpoints group
var userGroup = apiGroup.MapGroup("/users")
    .WithTags("Users");
userGroup.MapUserEndpoints();

app.MapFallbackToFile("/index.html");

app.Run();
