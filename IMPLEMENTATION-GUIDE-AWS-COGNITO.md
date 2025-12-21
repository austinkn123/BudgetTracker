# AWS Cognito with Gmail Sign-In - Implementation Guide

**Quick Reference for Development Team**

---

## Overview

This guide provides a high-level implementation roadmap for adding AWS Cognito authentication with Google/Gmail sign-in to the BudgetTracker application.

For complete requirements, see [REQUIREMENTS-AWS-COGNITO-AUTH.md](./REQUIREMENTS-AWS-COGNITO-AUTH.md)

---

## Architecture Overview

```
┌─────────────┐         ┌──────────────┐         ┌─────────────┐
│   React     │  JWT    │   ASP.NET    │  Query  │  SQL Server │
│   Frontend  │<──────>│   Backend    │<──────>│  Database   │
│             │         │              │         │             │
└──────┬──────┘         └──────┬───────┘         └─────────────┘
       │                       │
       │ OAuth 2.0            │ Token Validation
       │                       │
       ▼                       ▼
┌─────────────┐         ┌──────────────┐
│   Google    │         │    AWS       │
│   OAuth     │         │   Cognito    │
└─────────────┘         └──────────────┘
```

---

## Phase 1: AWS Cognito Setup

### 1.1 Create Cognito User Pool

**AWS Console Steps:**
1. Navigate to AWS Cognito service
2. Click "Create user pool"
3. Configure sign-in options:
   - ✅ Email
   - ✅ Preferred username
4. Configure password policy:
   - Minimum 8 characters
   - Require uppercase, lowercase, numbers, special characters
5. Configure MFA: Optional for MVP
6. Configure email delivery: Use Cognito (for development)
7. Name the user pool: `BudgetTracker-Users-{environment}`

**Save these values:**
- User Pool ID: `us-east-1_XXXXXXXXX`
- User Pool ARN: `arn:aws:cognito-idp:...`
- Region: `us-east-1` (or your chosen region)

### 1.2 Create App Client

1. In User Pool, go to "App integration" → "App clients"
2. Click "Create app client"
3. Configure:
   - Name: `BudgetTracker-Web-Client`
   - Client secret: Generate (for backend)
   - OAuth 2.0 flows: ✅ Authorization code grant
   - OAuth scopes: `openid`, `email`, `profile`
   - Callback URLs: 
     - Dev: `http://localhost:5173/auth/callback`
     - Prod: `https://yourdomain.com/auth/callback`
   - Sign-out URLs:
     - Dev: `http://localhost:5173/login`
     - Prod: `https://yourdomain.com/login`

**Save these values:**
- App Client ID: `abcdefghij1234567890`
- App Client Secret: `secret_value` (keep secure!)

### 1.3 Configure Google Identity Provider

**Google Cloud Console:**
1. Go to [Google Cloud Console](https://console.cloud.google.com)
2. Create new project: `BudgetTracker`
3. Enable Google+ API
4. Go to "Credentials" → "Create Credentials" → "OAuth 2.0 Client ID"
5. Application type: Web application
6. Authorized redirect URIs:
   - `https://{your-cognito-domain}.auth.us-east-1.amazoncognito.com/oauth2/idpresponse`
7. Save Client ID and Client Secret

**AWS Cognito:**
1. In User Pool, go to "Sign-in experience" → "Federated identity providers"
2. Click "Add identity provider" → "Google"
3. Enter Google Client ID and Client Secret
4. Attribute mapping:
   - `email` → `email`
   - `name` → `name`
   - `sub` → `preferred_username`
5. Save configuration

### 1.4 Configure Cognito Domain

1. In User Pool, go to "App integration" → "Domain"
2. Choose domain type:
   - Cognito domain: `budgettracker-{random}.auth.us-east-1.amazoncognito.com`
   - Or custom domain (requires SSL certificate)
3. Save domain

**Save this value:**
- Cognito Domain: `budgettracker-abc123.auth.us-east-1.amazoncognito.com`

---

## Phase 2: Backend Implementation (.NET)

### 2.1 Install NuGet Packages

```bash
cd BudgetTracker.Server
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Amazon.Extensions.CognitoAuthentication
dotnet add package AWSSDK.CognitoIdentityProvider
```

### 2.2 Update appsettings.json

```json
{
  "AWS": {
    "Region": "us-east-1",
    "Cognito": {
      "UserPoolId": "us-east-1_XXXXXXXXX",
      "AppClientId": "abcdefghij1234567890",
      "AppClientSecret": "your-client-secret",
      "Authority": "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_XXXXXXXXX"
    }
  }
}
```

### 2.3 Configure JWT Authentication (Program.cs)

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

// Add this after builder.Services configuration
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var cognitoConfig = builder.Configuration.GetSection("AWS:Cognito");
        options.Authority = cognitoConfig["Authority"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = cognitoConfig["Authority"],
            ValidateAudience = true,
            ValidAudience = cognitoConfig["AppClientId"],
            ValidateLifetime = true
        };
    });

// Update authorization line
app.UseAuthentication(); // Add before UseAuthorization
app.UseAuthorization();

// Uncomment this line to protect all API routes
apiGroup.RequireAuthorization();
```

### 2.4 Add Authentication Endpoints

Create `BudgetTracker.Server/Endpoints/AuthEndpoints.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using BudgetTracker.Application.Interfaces;
using BudgetTracker.Core.Models;
using System.Security.Claims;

namespace BudgetTracker.Server.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Get current user info
        app.MapGet("/user", [Authorize] async (
            HttpContext context,
            IUserRepository userRepository) =>
        {
            var cognitoUserId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(cognitoUserId))
                return Results.Unauthorized();

            var user = await userRepository.GetByCognitoUserIdAsync(cognitoUserId);
            if (user == null)
            {
                // Create user on first login
                var email = context.User.FindFirst(ClaimTypes.Email)?.Value ?? "";
                user = new User
                {
                    CognitoUserId = cognitoUserId,
                    Email = email,
                    CreatedAt = DateTime.UtcNow
                };
                await userRepository.CreateAsync(user);
            }

            return Results.Ok(new
            {
                user.Id,
                user.Email,
                user.CognitoUserId,
                user.CreatedAt
            });
        });

        // Check authentication status
        app.MapGet("/status", [Authorize] (HttpContext context) =>
        {
            return Results.Ok(new
            {
                Authenticated = true,
                UserId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                Email = context.User.FindFirst(ClaimTypes.Email)?.Value
            });
        });
    }
}
```

Register in Program.cs:
```csharp
var authGroup = apiGroup.MapGroup("/auth")
    .WithTags("Authentication");
authGroup.MapAuthEndpoints();
```

### 2.5 Update User Repository

Add method to `BudgetTracker.Application/Interfaces/IUserRepository.cs`:

```csharp
Task<User?> GetByCognitoUserIdAsync(string cognitoUserId);
```

Implement in `BudgetTracker.Infrastructure/Repositories/UserRepository.cs`:

```csharp
public async Task<User?> GetByCognitoUserIdAsync(string cognitoUserId)
{
    const string sql = "SELECT * FROM Users WHERE CognitoUserId = @CognitoUserId";
    using var connection = _context.CreateConnection();
    return await connection.QuerySingleOrDefaultAsync<User>(sql, new { CognitoUserId = cognitoUserId });
}
```

---

## Phase 3: Frontend Implementation (React)

### 3.1 Install Dependencies

```bash
cd budgettracker.client
npm install @aws-amplify/auth aws-amplify
```

### 3.2 Configure Amplify

Create `src/lib/cognitoConfig.ts`:

```typescript
import { Amplify } from 'aws-amplify';

Amplify.configure({
  Auth: {
    Cognito: {
      userPoolId: 'us-east-1_XXXXXXXXX',
      userPoolClientId: 'abcdefghij1234567890',
      loginWith: {
        oauth: {
          domain: 'budgettracker-abc123.auth.us-east-1.amazoncognito.com',
          scopes: ['openid', 'email', 'profile'],
          redirectSignIn: ['http://localhost:5173/auth/callback'],
          redirectSignOut: ['http://localhost:5173/login'],
          responseType: 'code',
          providers: ['Google']
        }
      }
    }
  }
});
```

### 3.3 Create Auth Context

Create `src/contexts/AuthContext.tsx`:

```typescript
import { createContext, useContext, useEffect, useState, ReactNode } from 'react';
import { getCurrentUser, signInWithRedirect, signOut } from '@aws-amplify/auth';

interface AuthContextType {
  user: any | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  signInWithGoogle: () => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<any | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    checkUser();
  }, []);

  async function checkUser() {
    try {
      const currentUser = await getCurrentUser();
      setUser(currentUser);
    } catch {
      setUser(null);
    } finally {
      setIsLoading(false);
    }
  }

  async function signInWithGoogle() {
    await signInWithRedirect({ provider: 'Google' });
  }

  async function logout() {
    await signOut();
    setUser(null);
  }

  const value = {
    user,
    isAuthenticated: !!user,
    isLoading,
    signInWithGoogle,
    logout
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
```

### 3.4 Create Login Component

Create `src/components/Login.tsx`:

```typescript
import { useAuth } from '../contexts/AuthContext';
import { Button, Container, Typography, Box } from '@mui/material';
import GoogleIcon from '@mui/icons-material/Google';

export default function Login() {
  const { signInWithGoogle, isLoading } = useAuth();

  return (
    <Container maxWidth="sm">
      <Box
        sx={{
          marginTop: 8,
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
        }}
      >
        <Typography component="h1" variant="h4" gutterBottom>
          BudgetTracker
        </Typography>
        <Typography variant="body1" color="text.secondary" sx={{ mb: 4 }}>
          Track your expenses and manage your budget
        </Typography>
        <Button
          variant="contained"
          size="large"
          startIcon={<GoogleIcon />}
          onClick={signInWithGoogle}
          disabled={isLoading}
          sx={{ minWidth: 250 }}
        >
          Sign in with Google
        </Button>
      </Box>
    </Container>
  );
}
```

### 3.5 Create Auth Callback Handler

Create `src/components/AuthCallback.tsx`:

```typescript
import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { fetchAuthSession } from '@aws-amplify/auth';
import { CircularProgress, Box } from '@mui/material';

export default function AuthCallback() {
  const navigate = useNavigate();

  useEffect(() => {
    handleCallback();
  }, []);

  async function handleCallback() {
    try {
      // Get auth session and tokens
      await fetchAuthSession({ forceRefresh: true });
      navigate('/');
    } catch (error) {
      console.error('Auth callback error:', error);
      navigate('/login');
    }
  }

  return (
    <Box
      sx={{
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'center',
        minHeight: '100vh',
      }}
    >
      <CircularProgress />
    </Box>
  );
}
```

### 3.6 Create Protected Route Component

Create `src/components/ProtectedRoute.tsx`:

```typescript
import { Navigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { CircularProgress, Box } from '@mui/material';

export default function ProtectedRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return (
      <Box
        sx={{
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          minHeight: '100vh',
        }}
      >
        <CircularProgress />
      </Box>
    );
  }

  return isAuthenticated ? <>{children}</> : <Navigate to="/login" />;
}
```

### 3.7 Update API Client

Update `src/lib/api.ts` to include auth token:

```typescript
import axios from 'axios';
import { fetchAuthSession } from '@aws-amplify/auth';

const api = axios.create({
  baseURL: '/api',
});

// Add auth token to all requests
api.interceptors.request.use(async (config) => {
  try {
    const session = await fetchAuthSession();
    const token = session.tokens?.idToken?.toString();
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
  } catch (error) {
    console.error('Failed to get auth token:', error);
  }
  return config;
});

// Handle 401 errors
api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401) {
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export default api;
```

### 3.8 Update App.tsx with Routing

```typescript
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AuthProvider } from './contexts/AuthContext';
import Login from './components/Login';
import AuthCallback from './components/AuthCallback';
import ProtectedRoute from './components/ProtectedRoute';
import Dashboard from './components/Dashboard';
import './lib/cognitoConfig'; // Initialize Amplify

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 1000 * 60 * 5,
      refetchOnWindowFocus: false,
    },
  },
});

function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <QueryClientProvider client={queryClient}>
          <Routes>
            <Route path="/login" element={<Login />} />
            <Route path="/auth/callback" element={<AuthCallback />} />
            <Route
              path="/"
              element={
                <ProtectedRoute>
                  <Dashboard />
                </ProtectedRoute>
              }
            />
          </Routes>
        </QueryClientProvider>
      </AuthProvider>
    </BrowserRouter>
  );
}

export default App;
```

### 3.9 Add Logout to Dashboard

Update `src/components/Dashboard.tsx`:

```typescript
import { useAuth } from '../contexts/AuthContext';
import { Button } from '@mui/material';
import LogoutIcon from '@mui/icons-material/Logout';

// Add to navigation/header
const { logout } = useAuth();

<Button
  startIcon={<LogoutIcon />}
  onClick={logout}
  variant="outlined"
>
  Logout
</Button>
```

---

## Phase 4: Environment Configuration

### 4.1 Development Environment Variables

Create `.env.local` in `budgettracker.client/`:

```env
VITE_COGNITO_USER_POOL_ID=us-east-1_XXXXXXXXX
VITE_COGNITO_CLIENT_ID=abcdefghij1234567890
VITE_COGNITO_DOMAIN=budgettracker-abc123.auth.us-east-1.amazoncognito.com
VITE_COGNITO_REGION=us-east-1
```

Update `src/lib/cognitoConfig.ts` to use env vars:

```typescript
Amplify.configure({
  Auth: {
    Cognito: {
      userPoolId: import.meta.env.VITE_COGNITO_USER_POOL_ID,
      userPoolClientId: import.meta.env.VITE_COGNITO_CLIENT_ID,
      // ... rest of config
    }
  }
});
```

### 4.2 Backend Environment Variables

Update `BudgetTracker.Server/appsettings.Development.json`:

```json
{
  "AWS": {
    "Region": "us-east-1",
    "Cognito": {
      "UserPoolId": "us-east-1_XXXXXXXXX",
      "AppClientId": "abcdefghij1234567890",
      "AppClientSecret": "your-client-secret",
      "Authority": "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_XXXXXXXXX"
    }
  }
}
```

**⚠️ NEVER commit secrets to source control!**

---

## Testing Checklist

### Manual Testing
- [ ] Click "Sign in with Google" redirects to Google
- [ ] Can authenticate with Google account
- [ ] Redirected to Dashboard after successful login
- [ ] User record created in database
- [ ] API requests include JWT token
- [ ] API returns 401 for unauthenticated requests
- [ ] Can log out successfully
- [ ] Cannot access protected routes when logged out
- [ ] Session persists after page refresh
- [ ] Session expires after token expiration

### Security Testing
- [ ] JWT signature validation works
- [ ] Expired tokens are rejected
- [ ] Invalid tokens are rejected
- [ ] Users can only access their own data
- [ ] HTTPS is enforced in production
- [ ] No secrets in source control
- [ ] CORS configured correctly

---

## Common Issues & Solutions

### Issue: "Redirect URI mismatch"
**Solution:** Ensure callback URL in Cognito matches exactly with your app URL (including http/https and port)

### Issue: "Invalid token"
**Solution:** Check that Authority and Audience in backend match Cognito configuration

### Issue: "User pool doesn't exist"
**Solution:** Verify User Pool ID and region are correct in configuration

### Issue: CORS errors
**Solution:** Add frontend URL to allowed origins in backend CORS configuration

### Issue: Token expires too quickly
**Solution:** Implement token refresh using Amplify's auto-refresh or manual refresh logic

---

## Production Deployment Checklist

- [ ] Use custom domain for Cognito (optional but recommended)
- [ ] Configure production callback URLs in Cognito
- [ ] Use AWS Secrets Manager for sensitive values
- [ ] Enable CloudWatch logging for Cognito
- [ ] Set up monitoring and alerts
- [ ] Configure proper CORS for production domain
- [ ] Test with real Google accounts (not just test users)
- [ ] Verify SSL certificates are valid
- [ ] Review and adjust token expiration times
- [ ] Set up automated backups for user data
- [ ] Document incident response procedures

---

## Additional Resources

- [AWS Amplify Auth Documentation](https://docs.amplify.aws/react/build-a-backend/auth/)
- [AWS Cognito Developer Guide](https://docs.aws.amazon.com/cognito/latest/developerguide/)
- [Google OAuth 2.0 Setup](https://developers.google.com/identity/protocols/oauth2)
- [JWT Best Practices](https://tools.ietf.org/html/rfc8725)

---

## Support

For questions or issues during implementation:
1. Check this guide and the requirements document
2. Review AWS Cognito and Amplify documentation
3. Check CloudWatch logs for backend errors
4. Check browser console for frontend errors
5. Contact the team lead or senior developer

---

**Last Updated:** December 21, 2025  
**Version:** 1.0
