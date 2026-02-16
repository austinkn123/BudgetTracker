# Cross-Cutting Concerns Analysis

**Document:** ARCH-02  
**Version:** 1.0  
**Status:** Design Phase  
**Last Updated:** December 21, 2025  
**Author:** Software Architecture Team

---

## Table of Contents
1. [Executive Summary](#executive-summary)
2. [Authentication & Authorization](#authentication--authorization)
3. [Logging & Monitoring](#logging--monitoring)
4. [Error Handling](#error-handling)
5. [Caching Strategy](#caching-strategy)
6. [Security Headers & CORS](#security-headers--cors)
7. [Rate Limiting](#rate-limiting)
8. [API Versioning](#api-versioning)
9. [Data Validation](#data-validation)
10. [Performance Monitoring](#performance-monitoring)

---

## Executive Summary

Cross-cutting concerns are aspects of the system that affect multiple layers and components. These concerns must be addressed systematically to ensure consistency, maintainability, and quality across the entire application.

### Key Cross-Cutting Concerns Identified

| Concern | Priority | Current Status | Target State |
|---------|----------|----------------|--------------|
| Authentication | P0 | ❌ Not Implemented | AWS Cognito + JWT |
| Authorization | P0 | ❌ Not Implemented | Claims-based |
| Logging | P0 | ⚠️ Basic | Structured logging |
| Error Handling | P0 | ❌ Not Implemented | Global middleware |
| CORS | P0 | ❌ Not Configured | Whitelist policy |
| Caching | P1 | ❌ Not Implemented | Response caching |
| Rate Limiting | P1 | ❌ Not Implemented | Token bucket |
| Monitoring | P1 | ❌ Not Implemented | Application Insights |
| Validation | P1 | ❌ Not Implemented | FluentValidation |
| API Versioning | P2 | ❌ Not Implemented | URL-based versioning |

---

## Authentication & Authorization

### Authentication Strategy

#### Current State
```csharp
// Program.cs (commented out)
// apiGroup.RequireAuthorization();
```
**Status:** No authentication ❌

#### Target State
```csharp
// JWT Bearer authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["AWS:Cognito:Authority"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidAudience = builder.Configuration["AWS:Cognito:AppClientId"]
        };
    });

// Protect all API endpoints
app.UseAuthentication();
app.UseAuthorization();
apiGroup.RequireAuthorization();
```

### Authorization Strategy

#### Levels of Authorization

1. **Authentication-based** (Phase 1 MVP)
   - User must be authenticated to access any API
   - All authenticated users have equal access

2. **Resource-based** (Future)
   - Users can only access their own data
   - Implemented via UserId filtering

3. **Role-based** (Future Enhancement)
   - Admin vs Regular User roles
   - Feature flags per role

#### Implementation Pattern

```csharp
// Current approach: Filter by UserId from token
public async Task<IResult> GetExpenses(HttpContext context, IExpenseRepository repo)
{
    var cognitoUserId = context.User.FindFirst("sub")?.Value;
    var user = await userRepo.GetByCognitoUserIdAsync(cognitoUserId);
    
    // Only return user's own expenses
    var expenses = await repo.GetByUserIdAsync(user.Id);
    return Results.Ok(expenses);
}

// Future: Policy-based authorization
[Authorize(Policy = "AdminOnly")]
public async Task<IResult> GetAllUsers(IUserRepository repo)
{
    var users = await repo.GetAllAsync();
    return Results.Ok(users);
}
```

### Security Implications

- **Impact:** Affects all API endpoints
- **Dependencies:** AWS Cognito, JWT middleware
- **Testing:** Requires authenticated test users
- **Migration:** Existing endpoints need [Authorize] attribute

---

## Logging & Monitoring

### Logging Strategy

#### Current State
- **Console logging**: Basic, unstructured
- **No centralized logging**: Logs lost on restart
- **No correlation IDs**: Hard to trace requests

#### Target State: Structured Logging

```csharp
// Serilog configuration
builder.Host.UseSerilog((context, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName()
        .Enrich.WithProperty("Application", "BudgetTracker")
        .WriteTo.Console(new JsonFormatter())
        .WriteTo.ApplicationInsights(
            context.Configuration["ApplicationInsights:InstrumentationKey"],
            TelemetryConverter.Traces)
        .WriteTo.File(
            "logs/budgettracker-.log",
            rollingInterval: RollingInterval.Day,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
});
```

### Logging Levels

| Level | Use Case | Example |
|-------|----------|---------|
| Trace | Detailed debugging | Token validation steps |
| Debug | Development debugging | SQL queries, cache hits |
| Information | Normal operations | User login, API calls |
| Warning | Recoverable issues | Token expiration, slow queries |
| Error | Failures | Database errors, auth failures |
| Critical | System failures | Service unavailable |

### Logging Best Practices

```csharp
// Good: Structured logging
logger.LogInformation(
    "User {CognitoUserId} accessed expense {ExpenseId}",
    cognitoUserId, expenseId);

// Bad: String concatenation
logger.LogInformation($"User {cognitoUserId} accessed expense {expenseId}");

// Security: Don't log sensitive data
logger.LogInformation(
    "User authenticated successfully", 
    // Don't log: password, tokens, PII
);
```

### Monitoring Strategy

#### Application Insights Integration

```csharp
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
    options.EnableAdaptiveSampling = true;
    options.EnableQuickPulseMetricStream = true;
});

// Custom metrics
var telemetryClient = serviceProvider.GetRequiredService<TelemetryClient>();

telemetryClient.TrackEvent("UserLogin", new Dictionary<string, string>
{
    { "Provider", "Google" },
    { "Success", "true" }
});

telemetryClient.TrackMetric("ExpenseCreated", 1, new Dictionary<string, string>
{
    { "Category", categoryName },
    { "Amount", amount.ToString() }
});
```

### Metrics to Track

| Metric | Type | Purpose |
|--------|------|---------|
| Request duration | Histogram | Performance monitoring |
| Error rate | Counter | Reliability monitoring |
| Active users | Gauge | Capacity planning |
| Database query time | Histogram | Performance tuning |
| Authentication failures | Counter | Security monitoring |
| Cache hit ratio | Gauge | Cache effectiveness |

---

## Error Handling

### Current State
- No global error handler
- Inconsistent error responses
- No error logging

### Target State: Centralized Error Handling

#### Global Exception Handler

```csharp
// Program.cs
app.UseExceptionHandler("/api/error");

app.Map("/api/error", (HttpContext context, ILogger<Program> logger) =>
{
    var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();
    var exception = exceptionHandlerFeature?.Error;
    
    var correlationId = context.TraceIdentifier;
    
    logger.LogError(exception, 
        "Unhandled exception occurred. CorrelationId: {CorrelationId}", 
        correlationId);
    
    var problemDetails = new ProblemDetails
    {
        Title = "An error occurred",
        Status = StatusCodes.Status500InternalServerError,
        Detail = context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment()
            ? exception?.Message
            : "An unexpected error occurred. Please try again later.",
        Instance = context.Request.Path,
        Extensions =
        {
            ["correlationId"] = correlationId,
            ["timestamp"] = DateTime.UtcNow
        }
    };
    
    return Results.Problem(problemDetails);
});
```

#### Specific Error Handling

```csharp
// Validation errors
public static IResult HandleValidationError(ValidationException ex)
{
    var errors = ex.Errors
        .GroupBy(e => e.PropertyName)
        .ToDictionary(
            g => g.Key,
            g => g.Select(e => e.ErrorMessage).ToArray()
        );
    
    return Results.ValidationProblem(errors);
}

// Not found errors
public static IResult HandleNotFound(string resourceType, string id)
{
    return Results.Problem(
        title: "Resource not found",
        statusCode: StatusCodes.Status404NotFound,
        detail: $"{resourceType} with ID '{id}' was not found."
    );
}

// Unauthorized errors
public static IResult HandleUnauthorized()
{
    return Results.Problem(
        title: "Unauthorized",
        statusCode: StatusCodes.Status401Unauthorized,
        detail: "You must be authenticated to access this resource."
    );
}

// Forbidden errors
public static IResult HandleForbidden()
{
    return Results.Problem(
        title: "Forbidden",
        statusCode: StatusCodes.Status403Forbidden,
        detail: "You don't have permission to access this resource."
    );
}
```

### Error Response Format (RFC 7807)

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "detail": "See the errors property for details.",
  "instance": "/api/expenses",
  "correlationId": "0HMVB8JKT7D9J",
  "timestamp": "2025-12-21T22:00:00Z",
  "errors": {
    "Amount": ["Amount must be greater than 0"],
    "Date": ["Date cannot be in the future"]
  }
}
```

---

## Caching Strategy

### Caching Layers

#### 1. Client-Side Caching (React Query)
```typescript
// Already implemented ✅
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 5, // 5 minutes
      cacheTime: 1000 * 60 * 10, // 10 minutes
      refetchOnWindowFocus: false,
    },
  },
});
```

#### 2. Server-Side Response Caching
```csharp
// Add response caching
builder.Services.AddResponseCaching();

app.UseResponseCaching();

// Cache specific endpoints
app.MapGet("/api/categories/user/{userId}", 
    [ResponseCache(Duration = 300)] // 5 minutes
    async (int userId, ICategoryRepository repo) =>
{
    var categories = await repo.GetByUserIdAsync(userId);
    return Results.Ok(categories);
});
```

#### 3. Distributed Caching (Redis) - Future
```csharp
// Add Redis distributed cache
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"];
    options.InstanceName = "BudgetTracker:";
});

// Usage
public class CachedCategoryRepository : ICategoryRepository
{
    private readonly ICategoryRepository _innerRepository;
    private readonly IDistributedCache _cache;
    
    public async Task<IEnumerable<Category>> GetByUserIdAsync(int userId)
    {
        var cacheKey = $"categories:user:{userId}";
        var cached = await _cache.GetStringAsync(cacheKey);
        
        if (cached != null)
        {
            return JsonSerializer.Deserialize<IEnumerable<Category>>(cached);
        }
        
        var categories = await _innerRepository.GetByUserIdAsync(userId);
        
        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(categories),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });
        
        return categories;
    }
}
```

### Cache Invalidation Strategy

| Event | Invalidation Strategy |
|-------|----------------------|
| Category created | Invalidate user's category list |
| Category updated | Invalidate specific category + list |
| Category deleted | Invalidate specific category + list |
| Expense created | Invalidate user's expense list |
| User updated | Invalidate user profile cache |

---

## Security Headers & CORS

### Security Headers Middleware

```csharp
app.Use(async (context, next) =>
{
    // Prevent MIME type sniffing
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    
    // Prevent clickjacking
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    
    // Enable XSS protection
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    
    // Control referrer information
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    
    // Content Security Policy
    context.Response.Headers.Add("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' data:; " +
        "connect-src 'self' https://cognito-idp.us-east-1.amazonaws.com https://accounts.google.com; " +
        "frame-ancestors 'none'");
    
    // HTTPS Strict Transport Security (HSTS)
    if (!context.Request.Host.Host.Contains("localhost"))
    {
        context.Response.Headers.Add("Strict-Transport-Security", 
            "max-age=31536000; includeSubDomains");
    }
    
    await next();
});
```

### CORS Configuration

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("AllowedOrigins")
            .Get<string[]>() ?? new[] { "http://localhost:53608" };
        
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .WithExposedHeaders("Content-Disposition", "Token-Expired");
    });
});

app.UseCors("AllowFrontend");
```

### Configuration
```json
{
  "AllowedOrigins": [
    "http://localhost:53608",
    "https://budgettracker.azurewebsites.net",
    "https://www.budgettracker.com"
  ]
}
```

---

## Rate Limiting

### Rate Limiting Strategy

```csharp
using System.Threading.RateLimiting;

builder.Services.AddRateLimiter(options =>
{
    // Global rate limit
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var userId = context.User.FindFirst("sub")?.Value ?? "anonymous";
        
        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 100,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
    
    // Specific endpoint rate limits
    options.AddFixedWindowLimiter("auth", options =>
    {
        options.PermitLimit = 5;
        options.Window = TimeSpan.FromMinutes(1);
    });
    
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = "Too many requests",
            Status = StatusCodes.Status429TooManyRequests,
            Detail = "Rate limit exceeded. Please try again later.",
            Instance = context.HttpContext.Request.Path
        }, cancellationToken: token);
    };
});

app.UseRateLimiter();

// Apply to specific endpoints
app.MapPost("/api/auth/login", HandleLogin)
   .RequireRateLimiting("auth");
```

### Rate Limit Tiers

| Tier | Requests/Minute | Use Case |
|------|-----------------|----------|
| Authentication | 5 | Login attempts |
| API (General) | 100 | Normal API usage |
| Bulk Operations | 10 | Expensive operations |
| Admin | 1000 | Admin users |

---

## API Versioning

### Versioning Strategy (Future)

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// Version 1 endpoints
var v1 = app.MapGroup("/api/v1").RequireAuthorization();
v1.MapExpenseEndpoints();
v1.MapCategoryEndpoints();

// Version 2 endpoints (future)
var v2 = app.MapGroup("/api/v2").RequireAuthorization();
v2.MapExpenseEndpointsV2();
```

---

## Data Validation

### Validation Strategy

```csharp
// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Create validators
public class CreateExpenseValidator : AbstractValidator<CreateExpenseRequest>
{
    public CreateExpenseValidator()
    {
        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Amount must be greater than 0");
        
        RuleFor(x => x.Date)
            .LessThanOrEqualTo(DateTime.UtcNow)
            .WithMessage("Date cannot be in the future");
        
        RuleFor(x => x.CategoryId)
            .GreaterThan(0)
            .WithMessage("Category is required");
        
        RuleFor(x => x.Merchant)
            .MaximumLength(100)
            .WithMessage("Merchant name cannot exceed 100 characters");
    }
}

// Apply validation in endpoints
app.MapPost("/api/expenses", async (
    CreateExpenseRequest request,
    IValidator<CreateExpenseRequest> validator,
    IExpenseRepository repo) =>
{
    var validationResult = await validator.ValidateAsync(request);
    if (!validationResult.IsValid)
    {
        return Results.ValidationProblem(validationResult.ToDictionary());
    }
    
    var expense = new Expense
    {
        Amount = request.Amount,
        Date = request.Date,
        CategoryId = request.CategoryId,
        Merchant = request.Merchant
    };
    
    await repo.CreateAsync(expense);
    return Results.Created($"/api/expenses/{expense.Id}", expense);
});
```

---

## Performance Monitoring

### Key Performance Indicators (KPIs)

| KPI | Target | Measurement |
|-----|--------|-------------|
| API Response Time (P95) | < 500ms | Application Insights |
| Authentication Time | < 3s | Custom metric |
| Database Query Time | < 100ms | Dapper profiling |
| Cache Hit Ratio | > 70% | Redis metrics |
| Error Rate | < 1% | Application Insights |
| Concurrent Users | 100+ | Load testing |

### Performance Testing Strategy

```bash
# Load testing with k6
k6 run --vus 100 --duration 60s load-test.js

# Database query profiling
EXPLAIN ANALYZE SELECT * FROM Expenses WHERE UserId = @UserId;

# Memory profiling
dotnet-counters monitor --process-id [PID]
```

---

## Implementation Roadmap

### Phase 1: Critical (Weeks 1-4)
- ✅ Authentication & Authorization
- ✅ Basic logging
- ✅ Global error handling
- ✅ CORS configuration

### Phase 2: Important (Weeks 5-8)
- ⬜ Structured logging with Serilog
- ⬜ Response caching
- ⬜ Rate limiting
- ⬜ Input validation
- ⬜ Security headers

### Phase 3: Enhancement (Weeks 9-12)
- ⬜ Application Insights integration
- ⬜ Distributed caching (Redis)
- ⬜ API versioning
- ⬜ Performance monitoring
- ⬜ Advanced metrics

---

## Impact Analysis

### Development Impact
- **Code changes**: All layers affected
- **Testing**: New test infrastructure needed
- **Documentation**: API docs need updating
- **Training**: Team needs auth/security training

### Infrastructure Impact
- **New services**: AWS Cognito, Application Insights
- **Configuration**: Environment variables, secrets
- **Monitoring**: New dashboards and alerts
- **Costs**: AWS Cognito (free tier initially)

### User Impact
- **Breaking change**: Authentication required
- **Migration**: Existing users need Cognito accounts
- **UX change**: Login flow added
- **Performance**: Minimal impact (< 15ms overhead)

---

## Related Documents

- [ARCH-01: AWS Cognito Architecture](./ARCH-01-AWS-COGNITO-ARCHITECTURE.md)
- [ARCH-03: Hidden Work Analysis](./ARCH-03-HIDDEN-WORK-ANALYSIS.md)
- [ARCH-07: Security Architecture](./ARCH-07-SECURITY-ARCHITECTURE.md)

---

**Document Status:** 🟢 Complete  
**Approved By:** Pending Review  
**Next Review Date:** January 2026
