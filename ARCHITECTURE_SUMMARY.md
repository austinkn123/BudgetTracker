# BudgetTracker - Architecture at a Glance

> **One-page visual reference for the BudgetTracker architecture**

---

## 🎯 System Overview

```
┌─────────────────────────────────────────────────────────────┐
│                     BUDGETTRACKER APP                        │
│                  Personal Finance Management                 │
└─────────────────────────────────────────────────────────────┘

   Web Browser (Users)
         ↓
   ┌─────────────────┐
   │   React 19 SPA   │  TypeScript, Vite, Material UI
   │  TanStack Query  │  State Management & Caching
   └────────┬─────────┘
            │ HTTPS/JSON
            ↓
   ┌─────────────────┐
   │  ASP.NET Core 9  │  Minimal APIs, C#
   │  Minimal APIs    │  Repository Pattern
   └────────┬─────────┘
            │ Dapper ORM
            ↓
   ┌─────────────────┐
   │   SQL Server    │  Relational Database
   │    Database     │  Users, Categories, Expenses
   └─────────────────┘
```

---

## 📦 Clean Architecture Layers

```
Outer ←──────────────────────────────────────────────→ Inner
┌────────────┬─────────────┬───────────┬──────────────┐
│Presentation│Infrastructure│Application│    Core      │
├────────────┼─────────────┼───────────┼──────────────┤
│ React SPA  │ Dapper      │ Interfaces│ Domain Models│
│ Server API │ Repositories│ Contracts │ Entities     │
│            │ DbContext   │           │              │
├────────────┴─────────────┴───────────┴──────────────┤
│          Dependencies flow inward ONLY →             │
└──────────────────────────────────────────────────────┘
```

---

## 🔧 Technology Stack

### Frontend Stack
```
┌─────────────────────────────────────┐
│ React 19.1.1         UI Framework   │
│ TypeScript 5.9       Type Safety    │
│ Vite 5.4            Build Tool      │
│ TanStack Query 5.90  State Mgmt     │
│ Material UI 7.3      Components     │
│ Tailwind CSS 3.4    Styling         │
│ Axios 1.13          HTTP Client     │
│ React Router 7.10   Routing         │
└─────────────────────────────────────┘
```

### Backend Stack
```
┌─────────────────────────────────────┐
│ ASP.NET Core 9.0    Framework       │
│ C# .NET 9.0         Language        │
│ Minimal APIs        API Style       │
│ Dapper 2.1          Micro-ORM       │
│ SQL Server          Database        │
│ Scrutor 6.1         DI Scanning     │
│ OpenAPI/Swagger     Documentation   │
└─────────────────────────────────────┘
```

---

## 📊 Domain Model

```
┌──────────────┐
│    USERS     │
├──────────────┤
│ Id           │◄────┐
│ CognitoId    │     │
│ Email        │     │
│ CreatedAt    │     │
└──────────────┘     │
                     │
       ┌─────────────┴─────────────┐
       │                           │
┌──────▼───────┐           ┌───────▼──────┐
│  CATEGORIES  │           │   EXPENSES   │
├──────────────┤           ├──────────────┤
│ Id           │           │ Id           │
│ UserId    ───┼───────┐   │ UserId       │
│ Name         │       │   │ CategoryId ──┼──┐
└──────────────┘       │   │ Amount       │  │
                       │   │ Date         │  │
                       │   │ Merchant     │  │
                       │   │ Notes        │  │
                       │   │ CreatedAt    │  │
                       │   └──────────────┘  │
                       │                     │
                       └─────────────────────┘
```

---

## 🔄 Request Flow

```
User Action → Component → React Query → Service → Axios
                                                    ↓
Database ← Dapper ← Repository ← Endpoint ← Kestrel
```

**Detailed:**
1. User clicks "View Expenses"
2. Dashboard component triggers
3. React Query checks cache (5 min stale time)
4. If stale, calls expenseService.getByUserId()
5. Axios sends GET /api/expenses/user/1
6. Kestrel receives and routes to endpoint
7. Endpoint calls IExpenseRepository
8. ExpenseRepository uses Dapper
9. Dapper executes SQL query
10. Results flow back up the chain
11. React Query caches and updates UI

---

## 🗂️ Project Structure

```
BudgetTracker/
│
├── budgettracker.client/         Frontend (React + TypeScript)
│   ├── src/
│   │   ├── components/           React components
│   │   │   └── Dashboard.tsx
│   │   ├── services/             API client services
│   │   │   └── api.service.ts
│   │   ├── types/                TypeScript definitions
│   │   │   └── api.ts
│   │   ├── lib/                  Utilities (Axios)
│   │   │   └── api.ts
│   │   ├── App.tsx
│   │   └── main.tsx
│   └── package.json
│
├── BudgetTracker.Core/           Domain Layer (Inner)
│   └── Models/
│       ├── User.cs
│       ├── Category.cs
│       └── Expense.cs
│
├── BudgetTracker.Application/    Application Layer
│   └── Interfaces/
│       ├── IUserRepository.cs
│       ├── ICategoryRepository.cs
│       └── IExpenseRepository.cs
│
├── BudgetTracker.Infrastructure/ Infrastructure Layer
│   ├── Data/
│   │   └── DapperContext.cs
│   └── Repositories/
│       ├── UserRepository.cs
│       ├── CategoryRepository.cs
│       └── ExpenseRepository.cs
│
└── BudgetTracker.Server/         Presentation Layer (Outer)
    ├── Endpoints/
    │   ├── UserEndpoints.cs
    │   ├── CategoryEndpoints.cs
    │   └── ExpenseEndpoints.cs
    ├── Program.cs
    └── appsettings.json
```

---

## 🚦 Current Status

### ✅ What's Working Well

```
✓ Clean Architecture implementation
✓ Repository pattern for data access
✓ Modern tech stack (React 19, .NET 9)
✓ Type-safe frontend with TypeScript
✓ Efficient data access with Dapper
✓ RESTful API design
✓ React Query for state management
✓ Development environment setup
```

### ⚠️ Critical Gaps (MUST FIX before production)

```
❌ No authentication/authorization
❌ No input validation
❌ No error handling middleware
❌ No structured logging
❌ No CORS policy
❌ Connection strings in plain text
❌ No rate limiting
❌ No health checks
❌ No unit tests
```

---

## 🎯 Quick Implementation Priority

```
Week 1-2: SECURITY
┌──────────────────────────────────┐
│ 1. Add JWT Authentication        │
│ 2. Implement Input Validation    │
│ 3. Add Error Handling            │
│ 4. Configure CORS                │
└──────────────────────────────────┘

Week 3-4: RELIABILITY
┌──────────────────────────────────┐
│ 5. Add Health Checks             │
│ 6. Move to Key Vault/Secrets Mgr │
│ 7. Implement Response Caching    │
│ 8. Add Rate Limiting             │
└──────────────────────────────────┘

Week 5-6: QUALITY
┌──────────────────────────────────┐
│ 9. Add API Versioning            │
│ 10. Create DTOs                  │
│ 11. Write Unit Tests             │
│ 12. Add Integration Tests        │
└──────────────────────────────────┘
```

---

## 🏗️ Deployment Architecture

### Development (Local)
```
Vite Dev Server (:53608) ──proxy──> Kestrel (:7134) ──> LocalDB
```

### Production (Recommended - Azure)
```
Users
  ↓
Azure Front Door (CDN + WAF)
  ↓                    ↓
Static Web App    App Service (2+ instances)
(React SPA)           ↓              ↓
                Redis Cache    Azure SQL DB
```

### Production (Alternative - AWS)
```
Users
  ↓
CloudFront CDN
  ↓              ↓
S3 Bucket    ALB → ECS Fargate (2+ containers)
(React SPA)         ↓              ↓
                ElastiCache    RDS SQL Server
```

---

## 📡 API Endpoints Reference

```
BASE: /api

EXPENSES
  GET    /expenses/{id}           Get single expense
  GET    /expenses/user/{userId}  List user's expenses
  POST   /expenses                Create expense
  PUT    /expenses/{id}           Update expense
  DELETE /expenses/{id}           Delete expense

CATEGORIES
  GET    /categories/{id}           Get single category
  GET    /categories/user/{userId}  List user's categories
  POST   /categories                Create category
  PUT    /categories/{id}           Update category
  DELETE /categories/{id}           Delete category

USERS
  GET    /users/{id}                Get user by ID
  GET    /users/cognito/{cognitoId} Get user by Cognito ID
  POST   /users                     Create user
  PUT    /users/{id}                Update user
  DELETE /users/{id}                Delete user
```

---

## 🔒 Security Checklist

```
Authentication        [ ] JWT with AWS Cognito
Authorization         [ ] Role-based access control (RBAC)
Input Validation      [ ] FluentValidation on all endpoints
Rate Limiting         [ ] 100 requests/minute per user
HTTPS                 [ ] Enforce HTTPS only (HSTS)
CORS                  [ ] Whitelist frontend origins only
Secrets               [ ] Move to Key Vault/Secrets Manager
Security Headers      [ ] X-Frame-Options, CSP, etc.
SQL Injection         [✓] Parameterized queries (Dapper)
XSS Protection        [✓] React auto-escaping
Audit Logging         [ ] Log all sensitive operations
Dependency Security   [ ] Dependabot alerts enabled
```

---

## 📈 Performance Targets

```
Response Time
  P50:  < 100ms
  P95:  < 500ms
  P99:  < 1000ms

Throughput
  Minimum: 100 req/sec
  Target:  1000 req/sec

Availability
  Target: 99.9% (8.76h downtime/year)

Cache Hit Ratio
  Target: > 70%

Database Query Time
  Average: < 100ms
  P95:     < 200ms
```

---

## 🎓 Key Design Patterns Used

```
Repository Pattern
  ├── IExpenseRepository (Interface)
  └── ExpenseRepository (Implementation)

Dependency Injection
  ├── Constructor Injection
  └── Scrutor Assembly Scanning

Clean Architecture
  ├── Dependency Inversion Principle
  └── Separation of Concerns

CQRS (Potential)
  ├── Commands (Write operations)
  └── Queries (Read operations)
```

---

## 📚 Documentation Index

```
📄 README.md                  Getting started, quick reference
📄 ARCHITECTURE.md            Comprehensive architecture guide
📄 ARCHITECTURE_DIAGRAMS.md   Visual diagrams (10 diagrams)
📄 IMPROVEMENTS.md            Prioritized improvement list
📄 THIS FILE                  One-page visual reference
```

---

## 🚀 Quick Commands

```bash
# Backend
cd BudgetTracker.Server
dotnet restore              # Install dependencies
dotnet run                  # Start server (port 7134)
dotnet build                # Build solution
dotnet test                 # Run tests (when added)

# Frontend
cd budgettracker.client
npm install                 # Install dependencies
npm run dev                 # Start dev server (port 53608)
npm run build               # Production build
npm run lint                # Run ESLint

# Database
sqlcmd -S localhost -d BudgetTracker -Q "SELECT * FROM Expenses"
```

---

## 💡 Key Takeaways

1. **Solid Foundation**: Clean Architecture is properly implemented
2. **Modern Stack**: Using latest technologies (.NET 9, React 19)
3. **Security First**: Need to implement auth before production
4. **Scalable Design**: Repository pattern allows easy scaling
5. **Developer Experience**: Good local dev setup with SPA proxy
6. **Production Gap**: Multiple critical items needed for prod

---

**Status**: 🟡 Development Ready | 🔴 Not Production Ready  
**Next Steps**: Implement security layer (auth, validation, error handling)  
**Full Documentation**: See ARCHITECTURE.md

---

*Last Updated: December 2024*  
*Architecture Version: 1.0*
