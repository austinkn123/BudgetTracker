using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Server.Endpoints;
using BudgetTracker.Server.Managers;
using BudgetTracker.Server.Utilities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<BudgetTrackerDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("BudgetTrackerConnection")));

// HTTP context accessor for Cognito claims extraction
builder.Services.AddHttpContextAccessor();

// Current user provider backed by Cognito
builder.Services.AddScoped<ICurrentUserProvider, CognitoCurrentUserProvider>();

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

// JWT Bearer authentication for Cognito
var cognitoConfig = builder.Configuration.GetSection("Cognito");
var authority = cognitoConfig["Authority"];
var audience = cognitoConfig["ClientId"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = authority,
            ValidAudience = audience
        };
    });

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

app.UseAuthentication();
app.UseAuthorization();

// Map endpoint groups
var apiGroup = app.MapGroup("/api")
    .WithOpenApi()
    .RequireAuthorization();

// Transaction endpoints group
var transactionGroup = apiGroup.MapGroup("/transactions")
    .WithTags("Transactions");
transactionGroup.MapTransactionEndpoints();

// Category endpoints group
var categoryGroup = apiGroup.MapGroup("/categories")
    .WithTags("Categories");
categoryGroup.MapCategoryEndpoints();

// Budget plan endpoints group
var budgetPlanGroup = apiGroup.MapGroup("/budget-plans")
    .WithTags("Budget Plans");
budgetPlanGroup.MapBudgetPlanEndpoints();

// User endpoints group
var userGroup = apiGroup.MapGroup("/users")
    .WithTags("Users");
userGroup.MapUserEndpoints();

app.MapFallbackToFile("/index.html");

app.Run();
