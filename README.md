# BudgetTracker

A modern, full-stack budget tracking application built with Clean Architecture principles.

## 🏗️ Architecture

This project follows Clean Architecture with a clear separation of concerns across multiple layers:

- **Frontend**: React 19 + TypeScript + Vite
- **Backend**: ASP.NET Core 9.0 (Minimal APIs)
- **Database**: SQL Server with Dapper ORM
- **Authentication**: AWS Cognito (planned)

### 📚 Documentation

- **[Architecture Documentation](ARCHITECTURE.md)** - Comprehensive architecture guide, tech stack analysis, and improvement recommendations
- **[Architecture Diagrams](ARCHITECTURE_DIAGRAMS.md)** - Visual diagrams showing system architecture, data flow, and component interactions

### 🎯 Quick Start

#### Prerequisites
- .NET 9.0 SDK
- Node.js 18+
- SQL Server (LocalDB or full instance)

#### Running Locally

**Backend:**
```bash
cd BudgetTracker.Server
dotnet restore
dotnet run
```

**Frontend:**
```bash
cd budgettracker.client
npm install
npm run dev
```

The application will be available at:
- Frontend: `https://localhost:53608`
- Backend API: `https://localhost:7134`
- OpenAPI: `https://localhost:7134/openapi/v1.json` (in development mode)

### 🏛️ Project Structure

```
BudgetTracker/
├── budgettracker.client/          # React SPA (TypeScript + Vite)
├── BudgetTracker.Core/            # Domain models (no dependencies)
├── BudgetTracker.Application/     # Repository interfaces
├── BudgetTracker.Infrastructure/  # Data access with Dapper
└── BudgetTracker.Server/          # ASP.NET Core Minimal APIs
```

### 🔄 Clean Architecture Layers

```
┌─────────────────────────────────────┐
│  Presentation (Server + Client)    │
├─────────────────────────────────────┤
│  Application (Interfaces)          │
├─────────────────────────────────────┤
│  Domain (Core Models)              │
├─────────────────────────────────────┤
│  Infrastructure (Data Access)      │
└─────────────────────────────────────┘
        Dependencies flow inward →
```

### 📡 API Endpoints

All endpoints are prefixed with `/api`:

**Expenses:**
- `GET /api/expenses/{id}` - Get expense by ID
- `GET /api/expenses/user/{userId}` - Get all expenses for a user
- `POST /api/expenses` - Create new expense
- `PUT /api/expenses/{id}` - Update expense
- `DELETE /api/expenses/{id}` - Delete expense

**Categories:**
- `GET /api/categories/{id}` - Get category by ID
- `GET /api/categories/user/{userId}` - Get all categories for a user
- `POST /api/categories` - Create new category
- `PUT /api/categories/{id}` - Update category
- `DELETE /api/categories/{id}` - Delete category

**Users:**
- `GET /api/users/{id}` - Get user by ID
- `GET /api/users/cognito/{cognitoId}` - Get user by Cognito ID
- `POST /api/users` - Create new user
- `PUT /api/users/{id}` - Update user
- `DELETE /api/users/{id}` - Delete user

### 🛠️ Technology Stack

#### Frontend
- **React 19.1.1** - UI framework
- **TypeScript 5.9** - Type safety
- **Vite 5.4** - Build tool and dev server
- **TanStack Query 5.90** - Server state management
- **Material UI 7.3** - Component library
- **Tailwind CSS 3.4** - Utility-first CSS
- **Axios 1.13** - HTTP client
- **React Router 7.10** - Client-side routing

#### Backend
- **ASP.NET Core 9.0** - Web framework
- **C# .NET 9.0** - Programming language
- **Dapper 2.1** - Micro-ORM
- **Microsoft SQL Server** - Database
- **Scrutor 6.1** - Assembly scanning for DI
- **OpenAPI/Swagger** - API documentation

### ⚠️ Current Status

This application has a **solid architectural foundation** but requires several improvements for production readiness:

**✅ Implemented:**
- Clean Architecture structure
- Repository pattern
- Modern React with TypeScript
- Minimal APIs with proper routing
- Development environment with SPA proxy

**⚠️ To Be Implemented (High Priority):**
- Authentication & Authorization (AWS Cognito)
- Input validation
- Error handling middleware
- Structured logging
- CORS configuration
- Health checks
- Unit tests

See [ARCHITECTURE.md](ARCHITECTURE.md) for detailed recommendations.

### 🚀 Deployment

The application can be deployed to:
- **Azure**: Static Web Apps (frontend) + App Service (backend) + Azure SQL
- **AWS**: S3 + CloudFront (frontend) + ECS Fargate (backend) + RDS SQL Server
- **Containers**: Docker + Kubernetes

See [ARCHITECTURE.md](ARCHITECTURE.md) for detailed deployment architectures.

### 🔒 Security Notes

**⚠️ WARNING**: The current implementation is **NOT production-ready** from a security perspective:
- No authentication/authorization implemented
- APIs are open without protection
- Connection strings in plain text
- No rate limiting
- No CORS policy

**Do not deploy to production** without implementing security measures outlined in the [Architecture Documentation](ARCHITECTURE.md).

### 📊 Database Schema

```
Users
  ├── Id (PK)
  ├── CognitoUserId
  ├── Email
  └── CreatedAt

Categories
  ├── Id (PK)
  ├── UserId (FK → Users)
  └── Name

Expenses
  ├── Id (PK)
  ├── UserId (FK → Users)
  ├── CategoryId (FK → Categories)
  ├── Amount
  ├── Date
  ├── Merchant
  ├── Notes
  └── CreatedAt
```

### 🤝 Contributing

1. Review the [Architecture Documentation](ARCHITECTURE.md)
2. Follow the Clean Architecture principles
3. Maintain the existing project structure
4. Add unit tests for new features
5. Update documentation as needed

### 📝 License

[Add your license here]

---

**For detailed architecture information, design decisions, and improvement recommendations, see [ARCHITECTURE.md](ARCHITECTURE.md)**
>>>>>>> main
