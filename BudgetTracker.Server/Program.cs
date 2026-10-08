using Amazon.Lambda.AspNetCoreServer.Hosting;
using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Engines;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Utilities;
using BudgetTracker.Domain.Plaid;
using BudgetTracker.Server.Endpoints;
using BudgetTracker.Server.Managers;
using BudgetTracker.Server.Services;
using BudgetTracker.Server.Utilities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Lambda sets AWS_LAMBDA_FUNCTION_NAME; locally it is absent and the app runs as a normal Kestrel host.
var isLambda = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));

if (isLambda)
{
    // Secrets live in SSM Parameter Store (SecureString) under /budgettracker/<Section>/<Key>, e.g.
    // /budgettracker/Plaid/Secret -> Plaid:Secret. CloudFormation cannot inject SecureStrings into
    // Lambda environment variables, so they are read (and decrypted) once per cold start instead.
    builder.Configuration.AddSystemsManager(builder.Configuration["SsmParameterPath"] ?? "/budgettracker");
}

// No-op outside Lambda. Function URLs use the HTTP API v2 payload format.
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);

// Add services to the container.
builder.Services.AddDbContext<BudgetTrackerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("BudgetTrackerConnection")));

// Data Protection — used by PlaidItemAccessor to encrypt access_tokens at rest. Keys are persisted in the
// database so every Lambda instance (and every cold start) shares one key ring.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<BudgetTrackerDbContext>()
    .SetApplicationName("BudgetTracker");

// In-memory cache — PlaidAccessor caches webhook verification keys (JWK) to avoid a per-webhook
// round-trip to Plaid (closes the webhook-verification timing oracle).
builder.Services.AddMemoryCache();

// Plaid configuration + typed HttpClient (BaseAddress comes from Plaid:BaseUrl).
builder.Services.Configure<PlaidOptions>(builder.Configuration.GetSection(PlaidOptions.SectionName));
builder.Services.AddHttpClient<IPlaidAccessor, PlaidAccessor>((sp, client) =>
{
    var plaidOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlaidOptions>>().Value;
    client.BaseAddress = new Uri(plaidOptions.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// HTTP context accessor for Cognito claims extraction
builder.Services.AddHttpContextAccessor();

// Current user provider backed by Cognito
builder.Services.AddScoped<ICurrentUserProvider, CognitoCurrentUserProvider>();

// Accessors (data access). PlaidAccessor is excluded because it's registered above as a typed HttpClient.
builder.Services.Scan(scan => scan
    .FromAssemblies(typeof(TransactionAccessor).Assembly)
        .AddClasses(classes => classes.Where(c => c.Name.EndsWith("Accessor") && c != typeof(PlaidAccessor)))
        .AsImplementedInterfaces()
        .WithScopedLifetime());

// Engines (business logic)
builder.Services.Scan(scan => scan
    .FromAssemblies(typeof(BudgetAnalysisEngine).Assembly)
        .AddClasses(classes => classes.Where(c => c.Name.EndsWith("Engine")))
        .AsImplementedInterfaces()
        .WithScopedLifetime());

// Managers (orchestration)
builder.Services.Scan(scan => scan
    .FromAssemblies(typeof(TransactionManager).Assembly)
        .AddClasses(classes => classes.Where(c => c.Name.EndsWith("Manager")))
        .AsImplementedInterfaces()
        .WithScopedLifetime());

// Background sweep — periodic backup re-sync of all active Plaid items (BUD-6). Lambda freezes the process
// between requests, so a timer-driven hosted service cannot run there; the client triggers
// POST /api/plaid/sync?staleAfterHours= instead.
if (!isLambda)
{
    builder.Services.AddHostedService<PlaidSyncSweepService>();
}

// Serialize enums as names rather than ordinals so API consumers get e.g. "Ahead", not 0.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Function URLs are HTTPS-only and terminate TLS before the function, so redirection there is meaningless.
if (!isLambda)
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

var apiGroup = app.MapGroup("/api")
    .WithOpenApi()
    .RequireAuthorization();

var transactionGroup = apiGroup.MapGroup("/transactions")
    .WithTags("Transactions");
transactionGroup.MapTransactionEndpoints();

var categoryGroup = apiGroup.MapGroup("/categories")
    .WithTags("Categories");
categoryGroup.MapCategoryEndpoints();

var budgetPlanGroup = apiGroup.MapGroup("/budget-plans")
    .WithTags("Budget Plans");
budgetPlanGroup.MapBudgetPlanEndpoints();

var userGroup = apiGroup.MapGroup("/users")
    .WithTags("Users");
userGroup.MapUserEndpoints();

var plaidGroup = apiGroup.MapGroup("/plaid")
    .WithTags("Plaid");
plaidGroup.MapPlaidEndpoints();

var budgetAnalysisGroup = apiGroup.MapGroup("/budget-analysis")
    .WithTags("Budget Analysis");
budgetAnalysisGroup.MapBudgetAnalysisEndpoints();

app.MapFallbackToFile("/index.html");

app.Run();
