# Copilot Instructions for BudgetTracker

You are an expert developer working on the BudgetTracker application. This project uses a specific tech stack and architectural patterns that you must follow.

## Tech Stack
- **Backend**: .NET 9, ASP.NET Core Minimal APIs
- **Frontend**: React + TypeScript + Vite
- **Database**: Microsoft SQL Server
- **ORM**: Entity Framework Core (Code-first with Fluent API configurations)
- **Styling**: Tailwind CSS + Material UI
- **Authentication**: AWS Cognito (In progress)

## Architecture
- Follow IDesign methodology with volatility-based decomposition.
- The backend uses a 2-project structure:
  - **BudgetTracker.Domain**: Models, Interfaces (Accessors/Engines/Managers), Engine implementations, Accessor implementations, `BudgetTrackerDbContext`, Fluent API entity configurations, EF migrations, and shared types like `Result<T>`.
  - **BudgetTracker.Server**: ASP.NET host, Minimal API Endpoints, and Manager implementations.
- **IDesign Service Taxonomy:**
  - **Managers** (in Server): Orchestrate workflows by calling Engines and Accessors. Zero business logic.
  - **Engines** (in Domain): Pure business logic and validation. Stateless, no data access.
  - **Accessors** (in Domain): Encapsulate all data access via Entity Framework Core. No business logic.
  - **Utilities**: Cross-cutting concerns (logging, configuration).
- **Strict Call Chain**: Endpoints → Managers → Engines + Accessors. Engines never call Accessors. Accessors never call Engines.
- **Client**: React frontend in `budgettracker.client`.

## Coding Standards
- **C#**: Use modern C# features (records, pattern matching, file-scoped namespaces). Prefer cleaner, concise code.
- **React**: Use Functional Components and Hooks. Avoid class components.
- **EF Core**: Use LINQ queries through `BudgetTrackerDbContext`. Use `AsNoTracking()` for read-only queries. Define entity configurations using Fluent API in `BudgetTracker.Domain/Data/Configurations/`. Use EF migrations (`dotnet ef migrations add`) for schema changes.
- **Testing**: Follow TDD (Red-Green-Refactor). Unit test Engines directly. Test Managers with mocked Engines/Accessors.
- **Contracts first**: Define interfaces in `BudgetTracker.Domain/Interfaces/` before implementing.

## Context
- Always check `project-docs/` for architectural decisions before suggesting major changes.
- Refer to `.github/agents/productManager.md` for product vision if unclear on requirements.
