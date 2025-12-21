# AWS Cognito Authentication - Architecture Diagrams

**Document:** ARCH-10  
**Version:** 1.0  
**Status:** Complete  
**Last Updated:** December 21, 2025  
**Author:** Software Architecture Team

---

## Table of Contents
1. [System Context](#system-context)
2. [Authentication Flows](#authentication-flows)
3. [Component Architecture](#component-architecture)
4. [Data Flow Diagrams](#data-flow-diagrams)
5. [Deployment Architecture](#deployment-architecture)
6. [Error Handling Flows](#error-handling-flows)

---

## System Context

### High-Level System Architecture

```mermaid
graph TB
    User[User Browser]
    
    subgraph "Client Layer"
        React[React SPA<br/>AWS Amplify<br/>Material UI]
        AuthContext[Auth Context Provider]
    end
    
    subgraph "AWS Cognito"
        UserPool[Cognito User Pool<br/>us-east-1]
        GoogleIDP[Google Identity Provider<br/>OAuth 2.0]
    end
    
    subgraph "Backend API Layer"
        Kestrel[ASP.NET Core 9<br/>Kestrel Server]
        JWTMiddleware[JWT Validation<br/>Middleware]
        AuthEndpoints[Auth Endpoints<br/>/api/auth/*]
        APIEndpoints[Protected Endpoints<br/>/api/*]
    end
    
    subgraph "Data Layer"
        UserRepo[User Repository<br/>Dapper]
        ExpenseRepo[Expense Repository<br/>Dapper]
        DB[(SQL Server<br/>Users, Categories, Expenses)]
    end
    
    User --> React
    React --> AuthContext
    React -->|OAuth Flow| UserPool
    UserPool <--> GoogleIDP
    UserPool -->|JWT Tokens| React
    
    React -->|API Calls + JWT| JWTMiddleware
    JWTMiddleware --> AuthEndpoints
    JWTMiddleware --> APIEndpoints
    
    AuthEndpoints --> UserRepo
    APIEndpoints --> ExpenseRepo
    UserRepo --> DB
    ExpenseRepo --> DB
    
    style UserPool fill:#ff9900,stroke:#333,stroke-width:2px
    style React fill:#61dafb,stroke:#333,stroke-width:2px
    style Kestrel fill:#512bd4,stroke:#333,stroke-width:2px
    style DB fill:#cc2927,stroke:#333,stroke-width:2px
```

---

## Authentication Flows

### 1. First-Time User Sign-Up Flow

```mermaid
sequenceDiagram
    participant U as User
    participant Browser as Browser
    participant React as React App
    participant Cognito as AWS Cognito
    participant Google as Google OAuth
    participant API as Backend API
    participant DB as Database

    U->>Browser: Navigate to app
    Browser->>React: Load application
    React->>React: Check auth state
    React->>Browser: Show login page
    
    U->>Browser: Click "Sign in with Google"
    Browser->>React: Handle click
    React->>Cognito: signInWithRedirect()
    Cognito->>Browser: Redirect to Google
    
    Browser->>Google: Google consent screen
    U->>Google: Authorize with Google
    Google->>Cognito: Return authorization code
    
    Cognito->>Cognito: Exchange code for tokens
    Cognito->>Browser: Redirect to /auth/callback
    
    Browser->>React: Load callback page
    React->>Cognito: Exchange code for JWT tokens
    Cognito->>React: Return access, ID, refresh tokens
    
    React->>React: Store tokens (memory + IndexedDB)
    React->>API: GET /api/auth/user + JWT
    
    API->>API: Validate JWT signature
    API->>API: Extract CognitoUserId from token
    API->>DB: SELECT user WHERE CognitoUserId = ?
    
    DB->>API: User not found
    API->>DB: INSERT new user (CognitoUserId, Email)
    DB->>API: New user created
    
    API->>React: Return user data
    React->>React: Update auth state
    React->>Browser: Redirect to Dashboard
    Browser->>U: Show Dashboard
```

### 2. Returning User Sign-In Flow

```mermaid
sequenceDiagram
    participant U as User
    participant React as React App
    participant Cognito as AWS Cognito
    participant Google as Google OAuth
    participant API as Backend API
    participant DB as Database

    U->>React: Click "Sign in with Google"
    React->>Cognito: signInWithRedirect()
    Cognito->>Google: Redirect to Google
    
    U->>Google: Already signed in to Google
    Google->>Cognito: Return authorization code
    
    Cognito->>React: Redirect with tokens
    React->>React: Store tokens
    React->>API: GET /api/auth/user + JWT
    
    API->>API: Validate JWT
    API->>DB: SELECT user WHERE CognitoUserId = ?
    DB->>API: Return existing user
    
    API->>React: Return user data
    React->>U: Show Dashboard
```

### 3. Auto-Login on Page Refresh

```mermaid
sequenceDiagram
    participant U as User
    participant React as React App
    participant Storage as Browser Storage
    participant Amplify as AWS Amplify
    participant Cognito as AWS Cognito
    participant API as Backend API

    U->>React: Refresh page
    React->>Amplify: getCurrentUser()
    Amplify->>Storage: Load tokens from IndexedDB
    
    alt Tokens valid
        Storage->>Amplify: Return tokens
        Amplify->>Amplify: Check expiration
        
        alt Access token valid
            Amplify->>React: Return user
            React->>U: Show Dashboard (still logged in)
        else Access token expired
            Amplify->>Cognito: Refresh tokens
            Cognito->>Amplify: New access token
            Amplify->>React: Return user
            React->>U: Show Dashboard (seamless)
        end
    else No tokens
        Storage->>Amplify: No tokens found
        Amplify->>React: User not authenticated
        React->>U: Show login page
    end
```

### 4. Token Refresh Flow (Background)

```mermaid
sequenceDiagram
    participant React as React App
    participant Amplify as AWS Amplify
    participant Cognito as AWS Cognito

    React->>Amplify: Make API call
    Amplify->>Amplify: Check access token expiration
    
    alt Token expired
        Amplify->>Cognito: POST /oauth2/token<br/>(with refresh token)
        
        alt Refresh token valid
            Cognito->>Amplify: New access + ID tokens
            Amplify->>Amplify: Store new tokens
            Amplify->>React: Proceed with API call
        else Refresh token expired
            Cognito->>Amplify: Error: invalid_grant
            Amplify->>React: Trigger logout
            React->>React: Redirect to login
        end
    else Token valid
        Amplify->>React: Use existing token
    end
```

### 5. Logout Flow

```mermaid
sequenceDiagram
    participant U as User
    participant React as React App
    participant Amplify as AWS Amplify
    participant Cognito as AWS Cognito
    participant API as Backend API

    U->>React: Click "Logout"
    React->>Amplify: signOut()
    
    Amplify->>Cognito: Revoke tokens
    Cognito->>Amplify: Tokens revoked
    
    Amplify->>Amplify: Clear local tokens<br/>(memory + IndexedDB)
    Amplify->>React: Sign out complete
    
    React->>React: Clear auth state
    React->>U: Redirect to login page
```

---

## Component Architecture

### Frontend Component Structure

```mermaid
graph TB
    subgraph "App Shell"
        App[App.tsx<br/>Router + Auth Provider]
    end
    
    subgraph "Auth Context"
        AuthProvider[AuthProvider<br/>Auth state management]
        useAuth[useAuth Hook<br/>Access auth state]
    end
    
    subgraph "Route Protection"
        ProtectedRoute[ProtectedRoute<br/>Authenticated route guard]
        PublicRoute[PublicRoute<br/>Login page]
    end
    
    subgraph "Pages"
        Login[Login Page<br/>Sign in with Google button]
        Dashboard[Dashboard<br/>Protected page]
        AuthCallback[Auth Callback<br/>Handle OAuth redirect]
    end
    
    subgraph "API Layer"
        AxiosInstance[Axios Instance<br/>With JWT interceptor]
        APIServices[API Services<br/>expenseService, etc.]
    end
    
    subgraph "AWS Amplify"
        AmplifyConfig[Amplify Config<br/>Cognito settings]
        AmplifyAuth[Amplify Auth<br/>signIn, signOut, etc.]
    end
    
    App --> AuthProvider
    AuthProvider --> useAuth
    
    App --> ProtectedRoute
    App --> PublicRoute
    
    ProtectedRoute --> Dashboard
    PublicRoute --> Login
    App --> AuthCallback
    
    Login --> AmplifyAuth
    Dashboard --> useAuth
    Dashboard --> APIServices
    
    APIServices --> AxiosInstance
    AxiosInstance --> AmplifyAuth
    
    AmplifyAuth --> AmplifyConfig
    
    style AmplifyAuth fill:#ff9900
    style AuthProvider fill:#61dafb
    style ProtectedRoute fill:#4caf50
```

### Backend Component Structure

```mermaid
graph TB
    subgraph "Entry Point"
        Program[Program.cs<br/>App configuration]
    end
    
    subgraph "Middleware Pipeline"
        HTTPS[HTTPS Redirection]
        CORS[CORS Middleware]
        Auth[Authentication<br/>JWT Bearer]
        Authz[Authorization]
        Routing[Endpoint Routing]
    end
    
    subgraph "Endpoints"
        AuthEndpoints[Auth Endpoints<br/>/api/auth/*]
        ExpenseEndpoints[Expense Endpoints<br/>/api/expenses/*]
        CategoryEndpoints[Category Endpoints<br/>/api/categories/*]
    end
    
    subgraph "Repositories"
        IUserRepo[IUserRepository<br/>Interface]
        UserRepo[UserRepository<br/>Implementation]
        IExpenseRepo[IExpenseRepository]
        ExpenseRepo[ExpenseRepository]
    end
    
    subgraph "Data Access"
        DapperContext[DapperContext<br/>Connection factory]
        DB[(SQL Server)]
    end
    
    Program --> HTTPS
    HTTPS --> CORS
    CORS --> Auth
    Auth --> Authz
    Authz --> Routing
    
    Routing --> AuthEndpoints
    Routing --> ExpenseEndpoints
    Routing --> CategoryEndpoints
    
    AuthEndpoints --> IUserRepo
    ExpenseEndpoints --> IExpenseRepo
    CategoryEndpoints --> IExpenseRepo
    
    UserRepo -.->|Implements| IUserRepo
    ExpenseRepo -.->|Implements| IExpenseRepo
    
    UserRepo --> DapperContext
    ExpenseRepo --> DapperContext
    DapperContext --> DB
    
    style Auth fill:#ff9900
    style IUserRepo fill:#2196f3
    style DB fill:#cc2927
```

---

## Data Flow Diagrams

### API Request with Authentication

```mermaid
sequenceDiagram
    participant React as React Component
    participant RQ as React Query
    participant Axios as Axios Interceptor
    participant Amplify as AWS Amplify
    participant MW as JWT Middleware
    participant EP as API Endpoint
    participant Repo as Repository
    participant DB as Database

    React->>RQ: useQuery(fetchExpenses)
    RQ->>Axios: GET /api/expenses/user/1
    
    Axios->>Amplify: getAccessToken()
    Amplify->>Amplify: Check token expiration
    
    alt Token expired
        Amplify->>Amplify: Refresh token
    end
    
    Amplify->>Axios: Return access token
    Axios->>MW: Request + Authorization: Bearer [token]
    
    MW->>MW: Validate JWT signature
    MW->>MW: Check expiration
    MW->>MW: Extract claims (sub, email)
    
    alt Valid token
        MW->>EP: Request + User Principal
        EP->>EP: Extract user ID from claims
        EP->>Repo: GetByUserIdAsync(userId)
        Repo->>DB: SELECT * FROM Expenses WHERE UserId = ?
        DB->>Repo: Result set
        Repo->>EP: IEnumerable<Expense>
        EP->>MW: 200 OK + JSON
        MW->>Axios: Response
        Axios->>RQ: Data
        RQ->>React: Update UI
    else Invalid token
        MW->>Axios: 401 Unauthorized
        Axios->>React: Redirect to login
    end
```

### User Auto-Provisioning Flow

```mermaid
flowchart TD
    Start[User makes first API call] --> ValidateJWT[Validate JWT token]
    
    ValidateJWT -->|Invalid| Return401[Return 401 Unauthorized]
    Return401 --> End1[End]
    
    ValidateJWT -->|Valid| ExtractClaims[Extract CognitoUserId from token]
    ExtractClaims --> CheckDB[Query database for user]
    
    CheckDB -->|User exists| UpdateEmail{Email changed?}
    UpdateEmail -->|Yes| UpdateUser[Update user email]
    UpdateUser --> ReturnUser[Return user data]
    UpdateEmail -->|No| ReturnUser
    
    CheckDB -->|User not found| CreateUser[Create new user record]
    CreateUser --> CheckDuplicate{Duplicate key error?}
    
    CheckDuplicate -->|Yes| QueryAgain[Query for user again]
    QueryAgain --> ReturnUser
    
    CheckDuplicate -->|No| ReturnUser
    ReturnUser --> End2[End]
    
    style CreateUser fill:#4caf50
    style Return401 fill:#f44336
    style ReturnUser fill:#2196f3
```

---

## Deployment Architecture

### Development Environment

```mermaid
graph TB
    subgraph "Developer Machine"
        Browser[Browser<br/>localhost:53608]
        
        subgraph "Frontend"
            Vite[Vite Dev Server<br/>Port 53608<br/>HMR + Hot Reload]
        end
        
        subgraph "Backend"
            Kestrel[Kestrel Server<br/>Port 7134<br/>HTTPS]
        end
        
        IDE[VS Code / Visual Studio<br/>Debugger]
    end
    
    subgraph "Local Services"
        LocalDB[(LocalDB /<br/>SQL Server Express)]
    end
    
    subgraph "AWS Cognito (Dev)"
        DevUserPool[Dev User Pool<br/>us-east-1]
        TestGoogle[Test Google OAuth]
    end
    
    Browser --> Vite
    Vite -.->|Proxy /api| Kestrel
    Kestrel --> LocalDB
    
    Vite --> DevUserPool
    DevUserPool --> TestGoogle
    
    IDE -.->|Debug| Vite
    IDE -.->|Debug| Kestrel
    
    style Vite fill:#646cff
    style Kestrel fill:#512bd4
    style DevUserPool fill:#ff9900
```

### Production Environment (Azure)

```mermaid
graph TB
    subgraph "Users"
        U[Users Worldwide]
    end
    
    subgraph "Azure Front Door"
        FD[Azure Front Door<br/>• Global CDN<br/>• WAF<br/>• SSL Termination]
    end
    
    subgraph "Static Web App"
        SPA[React SPA<br/>Static files served from CDN]
    end
    
    subgraph "App Service"
        AS[App Service Plan<br/>Linux + Docker]
        API1[API Instance 1]
        API2[API Instance 2]
    end
    
    subgraph "Caching"
        Redis[(Azure Redis Cache<br/>Response caching)]
    end
    
    subgraph "Database"
        SQL[(Azure SQL Database<br/>• Auto-backup<br/>• Geo-replication)]
    end
    
    subgraph "Security"
        KV[Azure Key Vault<br/>Secrets + certificates]
    end
    
    subgraph "AWS"
        Cognito[AWS Cognito<br/>Production User Pool]
        Google[Google OAuth]
    end
    
    subgraph "Monitoring"
        AI[Application Insights<br/>• Logs<br/>• Metrics<br/>• Alerts]
    end
    
    U --> FD
    FD --> SPA
    SPA --> FD
    FD --> API1
    FD --> API2
    
    API1 --> Redis
    API2 --> Redis
    API1 --> SQL
    API2 --> SQL
    API1 --> KV
    API2 --> KV
    
    SPA --> Cognito
    API1 --> Cognito
    API2 --> Cognito
    Cognito --> Google
    
    API1 --> AI
    API2 --> AI
    SPA --> AI
    
    style FD fill:#0078d4
    style SPA fill:#61dafb
    style Cognito fill:#ff9900
    style SQL fill:#cc2927
```

---

## Error Handling Flows

### Authentication Error Handling

```mermaid
flowchart TD
    Start[User attempts action] --> AuthCheck{Authenticated?}
    
    AuthCheck -->|No| CheckTokens{Tokens exist?}
    CheckTokens -->|No| ShowLogin[Redirect to login]
    ShowLogin --> End1[End]
    
    CheckTokens -->|Yes| RefreshToken[Attempt token refresh]
    RefreshToken --> RefreshSuccess{Refresh success?}
    
    RefreshSuccess -->|Yes| RetryAction[Retry original action]
    RetryAction --> End2[End]
    
    RefreshSuccess -->|No| ClearTokens[Clear invalid tokens]
    ClearTokens --> ShowLogin
    
    AuthCheck -->|Yes| MakeRequest[Make API request]
    MakeRequest --> CheckResponse{Response status?}
    
    CheckResponse -->|200 OK| Success[Show data to user]
    Success --> End3[End]
    
    CheckResponse -->|401| CheckExpiry{Token expired?}
    CheckExpiry -->|Yes| RefreshToken
    CheckExpiry -->|No| ShowError[Show error message]
    ShowError --> ShowLogin
    
    CheckResponse -->|403| ShowForbidden[Show access denied]
    ShowForbidden --> End4[End]
    
    CheckResponse -->|5xx| ShowServerError[Show server error]
    ShowServerError --> End5[End]
    
    style ShowLogin fill:#f44336
    style Success fill:#4caf50
    style ShowError fill:#ff9800
```

### Token Expiration Handling

```mermaid
sequenceDiagram
    participant U as User
    participant App as React App
    participant Amplify as AWS Amplify
    participant API as Backend API
    participant Cognito as AWS Cognito

    U->>App: Perform action (e.g., create expense)
    App->>Amplify: Get access token
    
    Amplify->>Amplify: Check token expiration
    
    alt Access token valid
        Amplify->>App: Return valid token
        App->>API: API call with token
        API->>App: Success response
        App->>U: Show success
    else Access token expired, refresh valid
        Amplify->>Cognito: Refresh token request
        Cognito->>Amplify: New access token
        Amplify->>App: Return new token
        App->>API: API call with new token
        API->>App: Success response
        App->>U: Show success (seamless)
    else Refresh token expired
        Amplify->>App: Authentication required
        App->>App: Clear auth state
        App->>U: Redirect to login
        Note over U,App: User must sign in again
    end
```

---

## Clean Architecture View

### Layer Dependencies with Authentication

```mermaid
graph TB
    subgraph "Presentation Layer"
        React[React SPA<br/>• Auth Context<br/>• Protected Routes<br/>• Login UI]
        Server[ASP.NET Server<br/>• JWT Middleware<br/>• Auth Endpoints]
    end
    
    subgraph "Application Layer"
        IUserRepo[IUserRepository<br/>GetByCognitoUserIdAsync]
        IExpenseRepo[IExpenseRepository<br/>GetByUserIdAsync]
    end
    
    subgraph "Domain Layer"
        User[User Entity<br/>• Id<br/>• CognitoUserId<br/>• Email]
        Expense[Expense Entity<br/>• Id<br/>• UserId<br/>• Amount]
    end
    
    subgraph "Infrastructure Layer"
        UserRepo[UserRepository<br/>Dapper + SQL]
        ExpenseRepo[ExpenseRepository<br/>Dapper + SQL]
    end
    
    React -->|HTTP| Server
    Server --> IUserRepo
    Server --> IExpenseRepo
    
    IUserRepo --> User
    IExpenseRepo --> Expense
    
    UserRepo -.->|Implements| IUserRepo
    ExpenseRepo -.->|Implements| IExpenseRepo
    
    Server --> UserRepo
    Server --> ExpenseRepo
    
    style User fill:#4caf50
    style IUserRepo fill:#2196f3
    style UserRepo fill:#ff9800
    style Server fill:#9c27b0
    style React fill:#61dafb
```

---

## Security Architecture

### JWT Token Validation Flow

```mermaid
sequenceDiagram
    participant API as API Request
    participant MW as JWT Middleware
    participant Cache as Key Cache
    participant Cognito as AWS Cognito
    participant EP as Endpoint

    API->>MW: Request + Bearer token
    MW->>MW: Extract JWT from header
    
    MW->>MW: Parse JWT (header, payload, signature)
    MW->>Cache: Check for Cognito public keys
    
    alt Keys cached
        Cache->>MW: Return public keys
    else Keys not cached
        MW->>Cognito: Fetch JWKS (public keys)
        Cognito->>MW: Return public keys
        MW->>Cache: Cache keys (1 hour)
    end
    
    MW->>MW: Validate signature with public key
    MW->>MW: Verify issuer (Cognito User Pool)
    MW->>MW: Verify audience (App Client ID)
    MW->>MW: Check expiration (exp claim)
    MW->>MW: Check not-before (nbf claim)
    
    alt All validations pass
        MW->>MW: Extract user claims
        MW->>MW: Create User Principal
        MW->>EP: Pass request with User Principal
        EP->>API: Process request
    else Validation fails
        MW->>API: 401 Unauthorized
    end
```

### Data Isolation Pattern

```mermaid
flowchart TD
    Start[API Request arrives] --> ExtractUser[Extract user from JWT claims]
    ExtractUser --> LookupDB[Look up user in database]
    
    LookupDB --> UserExists{User exists?}
    UserExists -->|No| CreateUser[Auto-provision user]
    CreateUser --> FilterData
    
    UserExists -->|Yes| FilterData[Filter data by UserId]
    FilterData --> ExecuteQuery[Execute query with UserId filter]
    ExecuteQuery --> ReturnData[Return only user's data]
    
    ReturnData --> End[End]
    
    Note right of FilterData: All queries MUST filter by UserId<br/>Users can ONLY see their own data
    
    style FilterData fill:#4caf50
    style ReturnData fill:#2196f3
```

---

## Key Takeaways

### Architecture Strengths

✅ **Security First**
- JWT tokens validated on every request
- Data isolation by UserId
- Industry-standard OAuth 2.0

✅ **Scalability**
- Stateless API design
- Managed services (Cognito, Azure/AWS)
- Horizontal scaling ready

✅ **User Experience**
- Seamless authentication
- Automatic token refresh
- Persistent sessions

✅ **Maintainability**
- Clean Architecture preserved
- Well-defined components
- Clear separation of concerns

### Critical Flows

1. **Authentication**: Google OAuth → Cognito → JWT → API
2. **Authorization**: JWT validation → User lookup → Data filtering
3. **Token Management**: Amplify handles refresh automatically
4. **Error Handling**: Graceful degradation, user-friendly messages

---

## Related Documents

- [ARCH-01: AWS Cognito Architecture](./ARCH-01-AWS-COGNITO-ARCHITECTURE.md)
- [ARCH-02: Cross-Cutting Concerns](./ARCH-02-CROSS-CUTTING-CONCERNS.md)
- [ARCH-04: Scope & Phasing](./ARCH-04-SCOPE-PHASING.md)
- [ARCH-05: ADRs](./ARCH-05-ADR-INDEX.md)

---

**Document Status:** 🟢 Complete  
**Approved By:** Pending Review  
**Next Review Date:** January 2026
