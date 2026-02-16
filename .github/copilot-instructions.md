# Copilot Instructions for BudgetTracker

You are an expert developer working on the BudgetTracker application. This project uses a specific tech stack and architectural patterns that you must follow.

## Tech Stack
- **Backend**: .NET 8, ASP.NET Core Web API
- **Frontend**: React + TypeScript + Vite
- **Database**: Microsoft SQL Server
- **ORM**: Dapper (Micro-ORM) - *Do not use Entity Framework unless explicitly requested.*
- **Styling**: Tailwind CSS
- **Authentication**: AWS Cognito (In progress)

## Architecture
- Follow the Clean Architecture principles found in `project-docs/ARCHITECTURE.md`.
- **Core**: Contains domain models and business logic.
- **Application**: Contains interfaces and service logic.
- **Infrastructure**: Contains data access implementation (Repositories, DapperContext).
- **Server**: API Controllers and configuration.
- **Client**: React frontend.

## Coding Standards
- **C#**: Use modern C# features (records, pattern matching). Prefer cleaner, concise code.
- **React**: Use Functional Components and Hooks. Avoid class components.
- **SQL**: Write raw SQL queries for Dapper. Ensure all user input is parameterized.
- **Testing**: Write unit tests for business logic in the `Application` layer.

## Context
- Always check `project-docs/` for architectural decisions before suggesting major changes.
- Refer to `.github/agents/productManager.md` for product vision if unclear on requirements.
