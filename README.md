# BudgetTracker

BudgetTracker is a full-stack personal finance application with a React frontend and an ASP.NET Core backend.

## Overview

- Frontend: React + TypeScript + Vite
- Backend: ASP.NET Core Minimal APIs on .NET 9
- Database: SQL Server
- Data access: Entity Framework Core
- Backend structure: BudgetTracker.Domain + BudgetTracker.Server

## Current Architecture

The backend is organized into two projects:

- `BudgetTracker.Domain`
  - Models
  - Interfaces
  - Engines for business rules
  - Accessors for EF Core-based data access
  - `BudgetTrackerDbContext` and Fluent API entity configurations
  - EF migrations
  - Shared types such as `Result<T>`
- `BudgetTracker.Server`
  - ASP.NET Core host
  - Minimal API endpoint mappings
  - Managers for orchestration
  - App-level utilities

Request flow follows a consistent path:

`Endpoints -> Managers -> Engines + Accessors`

This keeps business rules out of the HTTP layer and keeps data access out of the engine layer.

## Repository Layout

```text
BudgetTracker/
|-- budgettracker.client/      React SPA
|-- BudgetTracker.Domain/      Domain models, interfaces, engines, accessors
|-- BudgetTracker.Server/      API host, endpoints, managers, utilities
|-- DatabaseSetup.sql          Database bootstrap script
`-- BudgetTracker.sln          Solution file
```

## Getting Started

### Prerequisites

- .NET 9 SDK
- Node.js 18+
- SQL Server

### 1. Configure the database

Update the connection string in `BudgetTracker.Server/appsettings.Development.json` for your local SQL Server instance.

Then run `DatabaseSetup.sql` against that database to create the schema and seed data.

### 2. Run the backend

```bash
cd BudgetTracker.Server
dotnet restore
dotnet run
```

By default, the API is available from the ASP.NET Core development URL configured for the server project. OpenAPI is exposed in development.

### 3. Run the frontend

```bash
cd budgettracker.client
npm install
npm run dev
```

Vite will start the client development server and proxy API calls according to the client configuration.

## API Surface

The API is organized under `/api` with resource groups for:

- `/api/transactions`
- `/api/categories`
- `/api/users`

Endpoint mappings live in `BudgetTracker.Server/Endpoints/`.

## Technology Stack

### Frontend

- React
- TypeScript
- Vite
- Tailwind CSS
- Material UI

### Backend

- ASP.NET Core Minimal APIs
- .NET 9
- Entity Framework Core
- SQL Server
- Scrutor for DI registration

## Status

The repository has already gone through a major backend simplification:

- The old multi-project backend split was consolidated into `BudgetTracker.Domain` and `BudgetTracker.Server`
- Domain concerns are now separated into managers, engines, accessors, and utilities
- The root README is now aligned to the current repository layout instead of the removed `Core`, `Application`, and `Infrastructure` projects

Authentication is not documented as active in the current setup. Treat the app as a development-focused local environment unless and until auth is wired back in.

## Development Notes

- Use Entity Framework Core with LINQ for persistence work
- Use `AsNoTracking()` for read-only queries
- Manage schema changes through EF migrations (`dotnet ef migrations add`)
- Keep managers orchestration-only
- Keep engines free of data access concerns
- Keep accessors focused on database interaction

## Related Files

- `BudgetTracker.Server/Program.cs`
- `BudgetTracker.Server/Endpoints/`
- `BudgetTracker.Domain/Interfaces/`
- `DatabaseSetup.sql`
- `budgettracker.client/README.md`

## Contributing

When updating the backend:

- Preserve the `Endpoints -> Managers -> Engines + Accessors` flow
- Add new contracts in the domain project first
- Keep documentation in sync with structural changes
