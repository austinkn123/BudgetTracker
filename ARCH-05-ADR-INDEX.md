# Architectural Decision Records (ADRs)

**Document:** ARCH-05  
**Version:** 1.0  
**Status:** Design Phase  
**Last Updated:** December 21, 2025  
**Author:** Software Architecture Team

---

## Table of Contents
1. [ADR Index](#adr-index)
2. [ADR-001: JWT Token Strategy](#adr-001-jwt-token-strategy)
3. [ADR-002: User Session Management](#adr-002-user-session-management)
4. [ADR-003: API Authentication Middleware](#adr-003-api-authentication-middleware)
5. [ADR-004: Frontend Auth State Management](#adr-004-frontend-auth-state-management)
6. [ADR-005: Secrets Management](#adr-005-secrets-management)
7. [ADR-006: Error Handling Strategy](#adr-006-error-handling-strategy)
8. [ADR-007: Database User Provisioning](#adr-007-database-user-provisioning)

---

## ADR Index

| ID | Title | Status | Date | Impact |
|----|-------|--------|------|--------|
| ADR-001 | JWT Token Strategy | ✅ Approved | 2025-12-21 | High |
| ADR-002 | User Session Management | ✅ Approved | 2025-12-21 | High |
| ADR-003 | API Authentication Middleware | ✅ Approved | 2025-12-21 | High |
| ADR-004 | Frontend Auth State Management | ✅ Approved | 2025-12-21 | Medium |
| ADR-005 | Secrets Management | ✅ Approved | 2025-12-21 | High |
| ADR-006 | Error Handling Strategy | ✅ Approved | 2025-12-21 | Medium |
| ADR-007 | Database User Provisioning | ✅ Approved | 2025-12-21 | High |

---

## ADR-001: JWT Token Strategy

### Status
✅ **Approved** - December 21, 2025

### Context
We need to secure our API endpoints and identify authenticated users. Multiple authentication strategies exist (session-based, token-based, API keys), and we need to choose the most appropriate one for our architecture.

### Decision
**Use JWT (JSON Web Tokens) issued by AWS Cognito for API authentication.**

**Token Configuration:**
- **Access Token**: 1 hour expiration
- **ID Token**: 1 hour expiration (contains user claims)
- **Refresh Token**: 30 days expiration
- **Token Type**: Bearer tokens in Authorization header
- **Token Validation**: ASP.NET Core JWT Bearer middleware

### Rationale

#### Why JWT?
1. **Stateless**: No server-side session storage needed
2. **Scalable**: Can scale horizontally without session affinity
3. **Industry Standard**: Well-understood and widely supported
4. **Self-Contained**: Contains user identity and claims
5. **Cognito Native**: AWS Cognito issues JWTs by default

#### Why These Expiration Times?
- **1-hour access token**: 
  - Balance between security and UX
  - Short enough to limit exposure if compromised
  - Long enough to avoid constant refreshes
  
- **30-day refresh token**:
  - Allows "remember me" functionality
  - Matches user expectation for budget app
  - Forces re-authentication monthly

#### Why Authorization Header?
- HTTP standard (RFC 6750)
- Works with CORS
- Automatic browser handling
- Compatible with all HTTP clients

### Alternatives Considered

#### 1. Session-Based Authentication
**Rejected**: 
- Requires server-side state
- Doesn't scale horizontally easily
- Not compatible with AWS Cognito

#### 2. API Keys
**Rejected**:
- Not suitable for user authentication
- No automatic expiration
- Harder to revoke

#### 3. OAuth 2.0 Opaque Tokens
**Rejected**:
- Requires token introspection endpoint call
- Added latency on every request
- Cognito doesn't support this pattern well

### Consequences

#### Positive
- ✅ Stateless API (easier to scale)
- ✅ Standard approach (well-documented)
- ✅ Compatible with Cognito
- ✅ Good security with short expiration
- ✅ Automatic token refresh with Amplify

#### Negative
- ⚠️ Cannot invalidate tokens before expiration
- ⚠️ Token size larger than session ID
- ⚠️ Need to handle token refresh logic

#### Mitigations
- Use short expiration times (1 hour)
- Implement token refresh in frontend
- Consider token blacklist for logout (future)

### Implementation Notes
```csharp
// Backend token validation
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = cognito["Authority"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });
```

```typescript
// Frontend token usage
api.interceptors.request.use(async (config) => {
  const session = await fetchAuthSession();
  const token = session.tokens?.idToken?.toString();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});
```

### Related ADRs
- ADR-002: User Session Management
- ADR-003: API Authentication Middleware

---

## ADR-002: User Session Management

### Status
✅ **Approved** - December 21, 2025

### Context
Users expect to stay logged in when they close and reopen the browser. We need to decide how to manage user sessions across browser restarts while maintaining security.

### Decision
**Use AWS Cognito refresh tokens for persistent sessions with 30-day expiration.**

**Session Strategy:**
- Store tokens in memory (AWS Amplify handles this)
- Use browser's built-in secure storage (IndexedDB via Amplify)
- Automatic token refresh before expiration
- Clear tokens on explicit logout
- No custom session management on backend

### Rationale

#### Why Cognito Refresh Tokens?
1. **Managed by AWS**: Secure, tested, reliable
2. **Automatic Refresh**: Amplify handles refresh logic
3. **Secure Storage**: Amplify uses browser secure storage
4. **Standard OAuth 2.0**: Industry best practice

#### Why 30 Days?
- Matches user expectation ("remember me")
- Balances security with convenience
- Industry standard for non-sensitive apps
- Long enough for typical usage patterns
- Short enough to limit unauthorized access

#### Why Memory + IndexedDB?
- More secure than localStorage
- Automatic cleanup on tab close (memory)
- Persistence across browser restarts (IndexedDB)
- Amplify handles this automatically

### Alternatives Considered

#### 1. LocalStorage
**Rejected**:
- Accessible to JavaScript (XSS risk)
- Not secure for sensitive tokens
- No automatic cleanup

#### 2. Cookies with HttpOnly Flag
**Rejected**:
- Requires backend session management
- Cognito doesn't support this pattern
- Adds complexity

#### 3. No Persistence (Login Every Time)
**Rejected**:
- Poor user experience
- Increases authentication load
- Users expect "remember me"

#### 4. Longer Refresh Token (90+ days)
**Rejected**:
- Higher security risk
- Too long for budget app
- Industry standard is 30 days

### Consequences

#### Positive
- ✅ Good user experience (stay logged in)
- ✅ Secure (Amplify best practices)
- ✅ Automatic token refresh
- ✅ No backend session state
- ✅ Works across tabs

#### Negative
- ⚠️ User logged out after 30 days of inactivity
- ⚠️ Tokens survive browser close (intentional)
- ⚠️ Shared computer risk

#### Mitigations
- Clear messaging about session duration
- Prominent logout button
- Automatic logout on token expiration
- Consider "logout of all devices" feature (future)

### Implementation Notes
```typescript
// Amplify automatically handles refresh
// No manual refresh logic needed
await fetchAuthSession({ forceRefresh: true });

// Check if user is authenticated
const user = await getCurrentUser();
```

### Security Considerations
- Tokens encrypted at rest (browser secure storage)
- HTTPS required for token transmission
- Token automatically refreshed
- Logout clears all tokens

### Related ADRs
- ADR-001: JWT Token Strategy
- ADR-004: Frontend Auth State Management

---

## ADR-003: API Authentication Middleware

### Status
✅ **Approved** - December 21, 2025

### Context
All API endpoints need to be protected. We need to decide where and how to enforce authentication at the API layer.

### Decision
**Use ASP.NET Core JWT Bearer middleware with centralized API group protection.**

**Implementation:**
```csharp
// Register authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => { /* Cognito config */ });

// Enable in pipeline
app.UseAuthentication();
app.UseAuthorization();

// Protect all API routes
var apiGroup = app.MapGroup("/api").RequireAuthorization();
```

### Rationale

#### Why Middleware?
1. **Centralized**: Single point of authentication
2. **Automatic**: No need for [Authorize] on every endpoint
3. **Consistent**: All endpoints protected the same way
4. **Testable**: Easy to test authentication logic
5. **Standard**: ASP.NET Core built-in feature

#### Why Group-Level Protection?
- Protects all `/api/*` routes automatically
- New endpoints automatically protected
- Can't accidentally forget [Authorize]
- Clean, declarative syntax

#### Why JWT Bearer Middleware?
- Built into ASP.NET Core
- Well-tested and performant
- Handles token validation automatically
- Extracts user claims

### Alternatives Considered

#### 1. [Authorize] Attribute on Each Endpoint
**Rejected**:
- Easy to forget on new endpoints
- Repetitive code
- Higher risk of security holes

#### 2. Custom Middleware
**Rejected**:
- Reinventing the wheel
- More code to maintain
- Likely less secure than built-in

#### 3. API Gateway Authentication
**Rejected**:
- Over-engineering for current scale
- Added complexity
- Not needed for monolithic API

### Consequences

#### Positive
- ✅ All endpoints automatically protected
- ✅ Consistent authentication
- ✅ Less code to write
- ✅ Hard to make security mistakes
- ✅ Standard approach

#### Negative
- ⚠️ All routes under `/api` require auth
- ⚠️ Need to explicitly allow public endpoints

#### Mitigations
```csharp
// For public endpoints (if needed later)
app.MapGet("/api/public/status", () => "OK")
   .AllowAnonymous();
```

### Implementation Notes
```csharp
// Accessing user identity in endpoints
app.MapGet("/api/expenses", async (
    HttpContext context,
    IExpenseRepository repo) =>
{
    var cognitoUserId = context.User.FindFirst("sub")?.Value;
    // ... use cognitoUserId
});
```

### Security Considerations
- Validates JWT signature using Cognito public keys
- Checks token expiration
- Verifies issuer and audience
- Returns 401 for invalid/missing tokens

### Related ADRs
- ADR-001: JWT Token Strategy
- ADR-007: Database User Provisioning

---

## ADR-004: Frontend Auth State Management

### Status
✅ **Approved** - December 21, 2025

### Context
The frontend needs to manage authentication state (logged in/out, user info, loading states) and make it available throughout the component tree.

### Decision
**Use React Context API with custom AuthProvider for authentication state management.**

**Structure:**
- AuthContext provides auth state and methods
- useAuth hook for consuming auth state
- AuthProvider wraps the app
- No Redux or other state management library

### Rationale

#### Why React Context?
1. **Built-in**: No additional dependencies
2. **Simple**: Easy to understand and maintain
3. **Sufficient**: Auth state is simple enough
4. **Standard Pattern**: Well-documented React pattern
5. **Tree-Wide Access**: Any component can access auth

#### Why Custom Hook (useAuth)?
- Type-safe access to auth context
- Throws error if used outside provider
- Clean API for components
- Encapsulates context usage

#### Why Not Redux/Zustand?
- Over-engineering for simple auth state
- Additional dependency
- More boilerplate
- Auth state doesn't need Redux features (time travel, middleware)

### Alternatives Considered

#### 1. Redux Toolkit
**Rejected**:
- Overkill for auth state
- Adds complexity
- Already using React Query for server state

#### 2. Zustand
**Rejected**:
- Additional dependency
- Not needed for simple state
- Context API sufficient

#### 3. Prop Drilling
**Rejected**:
- Painful for deeply nested components
- Refactoring nightmare
- Violates DRY principle

### Consequences

#### Positive
- ✅ Simple, maintainable code
- ✅ No additional dependencies
- ✅ Type-safe with TypeScript
- ✅ Easy to test
- ✅ Standard React pattern

#### Negative
- ⚠️ Re-renders when auth state changes
- ⚠️ Can't easily debug like Redux DevTools

#### Mitigations
- Optimize with React.memo if needed
- Most auth state changes infrequent
- Use React DevTools for debugging

### Implementation Notes
```typescript
interface AuthContextType {
  user: CognitoUser | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  signInWithGoogle: () => Promise<void>;
  logout: () => Promise<void>;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<CognitoUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  
  // Check auth state on mount
  useEffect(() => {
    checkAuthState();
  }, []);
  
  // ... methods
  
  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
}

// Usage in components
function Dashboard() {
  const { user, isAuthenticated, logout } = useAuth();
  // ...
}
```

### Related ADRs
- ADR-002: User Session Management

---

## ADR-005: Secrets Management

### Status
✅ **Approved** - December 21, 2025

### Context
The application needs to store sensitive configuration (Cognito IDs, database connection strings, etc.) securely across different environments.

### Decision
**Use environment variables for all environments with Azure Key Vault / AWS Secrets Manager in production.**

**Strategy:**
- **Development**: `.env` files (not committed)
- **CI/CD**: Environment variables in pipeline
- **Production**: Azure Key Vault or AWS Secrets Manager
- **Never**: Commit secrets to source control

### Rationale

#### Why Environment Variables?
1. **12-Factor App**: Industry best practice
2. **Universal**: Works everywhere
3. **Simple**: Easy to understand
4. **Secure**: Not in source control

#### Why Key Vault in Production?
- Centralized secret management
- Audit logging (who accessed what)
- Automatic rotation support
- Integration with Azure/AWS
- Encrypted at rest and in transit

#### Why Not Config Files?
- Easy to accidentally commit
- No audit trail
- No rotation support
- Harder to manage across environments

### Alternatives Considered

#### 1. appsettings.json with Secrets
**Rejected**:
- Risk of committing secrets
- No rotation support
- No audit logging

#### 2. Environment Variables Only (Prod)
**Rejected**:
- Less secure than Key Vault
- No audit trail
- Manual rotation

#### 3. Encrypted Config Files
**Rejected**:
- Encryption key management problem
- Added complexity
- Less standard

### Consequences

#### Positive
- ✅ Secrets never in source control
- ✅ Easy to rotate secrets
- ✅ Audit trail in production
- ✅ Standard practice
- ✅ Different secrets per environment

#### Negative
- ⚠️ Need to manage Key Vault/Secrets Manager
- ⚠️ Additional cost (minimal)
- ⚠️ Complexity in setup

#### Mitigations
- Document setup process clearly
- Use infrastructure as code
- Automate secret rotation where possible

### Implementation Notes

#### Development
```bash
# .env.local (NOT committed)
VITE_COGNITO_USER_POOL_ID=us-east-1_XXXXXXXXX
VITE_COGNITO_CLIENT_ID=abcdefghij1234567890
```

#### Production (Azure)
```csharp
// Program.cs
builder.Configuration.AddAzureKeyVault(
    new Uri(builder.Configuration["KeyVault:Url"]),
    new DefaultAzureCredential());
```

#### Production (AWS)
```csharp
// Program.cs
builder.Configuration.AddSecretsManager(region: RegionEndpoint.USEast1);
```

### Security Considerations
- Never log secrets
- Rotate secrets regularly
- Use managed identities (Azure) or IAM roles (AWS)
- Monitor secret access

### Related ADRs
- ADR-001: JWT Token Strategy

---

## ADR-006: Error Handling Strategy

### Status
✅ **Approved** - December 21, 2025

### Context
The application needs consistent error handling across frontend and backend, with appropriate error messages for users and detailed logging for developers.

### Decision
**Use Problem Details (RFC 7807) for API errors with global exception handler and structured logging.**

### Backend Strategy
```csharp
// Global exception handler
app.UseExceptionHandler("/api/error");

app.Map("/api/error", (HttpContext context) =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    
    return Results.Problem(
        title: "An error occurred",
        statusCode: 500,
        detail: isDevelopment ? exception?.Message : "Please try again",
        instance: context.Request.Path,
        extensions: new Dictionary<string, object?>
        {
            ["correlationId"] = context.TraceIdentifier
        }
    );
});
```

### Frontend Strategy
```typescript
// Axios error interceptor
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      window.location.href = '/login';
    } else {
      showErrorNotification(error.response?.data?.detail || 'An error occurred');
    }
    return Promise.reject(error);
  }
);
```

### Rationale
- Standard error format (RFC 7807)
- Consistent across all endpoints
- User-friendly messages
- Developer-friendly logging
- Correlation IDs for tracing

### Related ADRs
- ADR-003: API Authentication Middleware

---

## ADR-007: Database User Provisioning

### Status
✅ **Approved** - December 21, 2025

### Context
When a user authenticates for the first time, we need to create a corresponding user record in our database. We need to decide when and how this happens.

### Decision
**Auto-provision users on first API call after successful authentication.**

**Strategy:**
1. User authenticates with Cognito (gets JWT)
2. Frontend makes first API call to `/api/auth/user`
3. Backend validates JWT, extracts CognitoUserId
4. Backend checks if user exists in database
5. If not exists, create user record
6. Return user data to frontend

### Rationale

#### Why Auto-Provisioning?
1. **Simple**: No separate registration flow
2. **Automatic**: User doesn't need to "sign up"
3. **Reliable**: Happens on first authenticated request
4. **Idempotent**: Safe to call multiple times

#### Why on First API Call?
- JWT already validated at this point
- All required data available (CognitoUserId, email)
- Single responsibility (auth endpoint handles user creation)
- Easy to test

#### Why Not During OAuth Callback?
- OAuth callback happens in frontend
- Frontend shouldn't create database records
- Backend needs to validate JWT anyway

### Alternatives Considered

#### 1. Explicit Registration Endpoint
**Rejected**:
- Extra step for user
- More complex flow
- Can forget to call

#### 2. Database Trigger
**Rejected**:
- Business logic in database
- Harder to test
- Less control

#### 3. Cognito Lambda Trigger
**Rejected**:
- Tight coupling to AWS
- Harder to test locally
- Additional AWS configuration

### Consequences

#### Positive
- ✅ Simple implementation
- ✅ Automatic (no user action)
- ✅ Safe (idempotent)
- ✅ Easy to test

#### Negative
- ⚠️ First API call slightly slower
- ⚠️ Race condition with concurrent requests

#### Mitigations
- Cache user after creation
- Use database unique constraint on CognitoUserId
- Handle duplicate user creation gracefully

### Implementation Notes
```csharp
// Auto-provisioning logic
var user = await userRepository.GetByCognitoUserIdAsync(cognitoUserId);

if (user == null)
{
    user = new User
    {
        CognitoUserId = cognitoUserId,
        Email = email,
        CreatedAt = DateTime.UtcNow
    };
    
    try
    {
        await userRepository.CreateAsync(user);
    }
    catch (SqlException ex) when (ex.Number == 2627) // Duplicate key
    {
        // Another request created the user, fetch it
        user = await userRepository.GetByCognitoUserIdAsync(cognitoUserId);
    }
}
```

### Related ADRs
- ADR-001: JWT Token Strategy
- ADR-003: API Authentication Middleware

---

## ADR Summary

### Decision Status Overview

| Decision | Rationale | Impact |
|----------|-----------|--------|
| JWT Tokens | Industry standard, scalable | High |
| 30-Day Sessions | Balance security/UX | High |
| Middleware Auth | Centralized, consistent | High |
| React Context | Simple, sufficient | Medium |
| Key Vault Secrets | Secure, auditable | High |
| Problem Details Errors | Standard, consistent | Medium |
| Auto-Provision Users | Simple, automatic | High |

### Cross-Cutting Impact

These decisions collectively:
- ✅ Maintain Clean Architecture
- ✅ Use industry standards
- ✅ Minimize custom code
- ✅ Prioritize security
- ✅ Enable scalability
- ✅ Simplify testing

---

## Related Documents

- [ARCH-01: AWS Cognito Architecture](./ARCH-01-AWS-COGNITO-ARCHITECTURE.md)
- [ARCH-02: Cross-Cutting Concerns](./ARCH-02-CROSS-CUTTING-CONCERNS.md)
- [ARCH-04: Scope & Phasing](./ARCH-04-SCOPE-PHASING.md)

---

**Document Status:** 🟢 Complete  
**Approved By:** Pending Review  
**Next Review Date:** January 2026
