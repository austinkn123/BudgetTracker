# Claude Instructions for BudgetTracker

You are an expert developer working on the BudgetTracker application. This project uses a specific tech stack and architectural patterns that you must follow.

## Tech Stack
- **Backend**: .NET 9, ASP.NET Core Minimal APIs
- **Frontend**: React + TypeScript + Vite
- **Styling**: Tailwind CSS + Material UI
- **Forms/Validation**: React Hook Form + Zod (`@hookform/resolvers`)
- **Database**: Microsoft SQL Server
- **ORM**: Entity Framework Core (Code-first with Fluent API configurations)
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
- **React Forms**: Use React Hook Form with Zod for validation. Keep reusable schemas in `budgettracker.client/src/shared/validation/` and prefer shared schema imports over inline schema duplication.
- **EF Core**: Use LINQ queries through `BudgetTrackerDbContext`. Use `AsNoTracking()` for read-only queries. Define entity configurations using Fluent API in `BudgetTracker.Domain/Data/Configurations/`. Use EF migrations (`dotnet ef migrations add`) for schema changes.
- **Testing**: Follow TDD (Red-Green-Refactor). Unit test Engines directly. Test Managers with mocked Engines/Accessors.
- **Contracts first**: Define interfaces in `BudgetTracker.Domain/Interfaces/` before implementing.

## Context
- Always check `project-docs/` for architectural decisions before suggesting major changes.
- Refer to `.claude/agents/chrissy-product-manager.md` for product vision if unclear on requirements.
- Jira project: **BUD** on `austinkn123.atlassian.net` — check for existing issues before creating new ones, and link completed work to its ticket when relevant.
- **Jira access via MCP**: The Atlassian MCP server is configured in `.mcp.json` and provides direct Jira access. When asked about any BUD ticket, **ALWAYS call the Atlassian MCP tools** — never say "I can't connect to Jira" without first attempting the call. Common tools: `mcp__atlassian__getIssue` (fetch a specific ticket), `mcp__atlassian__searchIssues` (JQL search), `mcp__atlassian__getAccessibleAtlassianResources` (list resources). If the call fails, report the actual error returned by the tool, not a pre-emptive refusal.
- Local database: **SQL Server LocalDB** — `Server=(localdb)\MSSQLLocalDB;Database=BudgetTracker;Trusted_Connection=True;TrustServerCertificate=True;` — use the `mssql` MCP server to inspect schema, run queries, and verify migrations directly.

## Agent Roster

| Agent | Role | Use when |
|---|---|---|
| chrissy | Product Manager | Requirements, user stories, backlog priority, "what should we build" |
| tony | Software Architect | Service boundaries, IDesign decisions, new feature impact analysis |
| richie | Senior DBA | Schema design, indexes, query tuning, EF migration review |
| paulie | Senior Backend Dev | C#/.NET implementation, TDD, bug fixes |
| silvio | Senior UI Dev | React/TS/Tailwind/MUI implementation, design system, component library |
| johnny | Senior QA | Test plans, coverage review, TDD verification, edge cases |
| bobby | Docs Specialist | README and standalone .md files only — never code |

## Common Workflows

Default agent sequences. Delegate via the Task tool; chain by invoking the next agent with the prior agent's output as context.

- **New feature**: chrissy (story + AC) → tony (service boundaries) → silvio (frontend) + paulie (backend) → johnny (test review) → bobby (README/docs touch-up if user-facing)
- **Bug fix**: paulie or silvio (failing test + fix) → johnny (regression coverage review)
- **Schema change**: tony (architectural impact) → richie (schema + index design) → paulie (EF migration + code) → johnny (data + integration tests)
- **Docs-only update**: bobby
- **Pre-commit review**: tony + johnny in parallel (already automated via `/review`)

## Auto-delegation Rules

- Default to delegating. The main thread is a router, not an implementer.
- Only handle inline when the task is a trivial single-file edit (typo, one-line config tweak, rename) AND no agent description matches.
- When multiple agents match, prefer the earliest in the relevant workflow chain.
- For multi-step work, delegate each step to its specialist rather than asking one agent to cross lanes.
- Surface the chosen agent and reason in one short sentence before invoking (e.g., "Routing to paulie — TDD implementation work").

## Guardrails

Three layers of safety. Tagged actions surface a confirmation prompt; nothing is hard-blocked from override.

**Destructive ops (require confirmation):** force pushes, hard resets, branch deletes, working-tree discards, stash drops, recursive deletes, process kills, EF migration removal, EF database drop. Enforced via the `permissions.ask` list in `.claude/settings.json`.

**Sensitive files (require confirmation):** `.env*`, `appsettings.Production*.json`, `*.pfx/*.key/*.pem/*.p12`, anything under `.git/`, `.mcp.json`, `.claude/settings.json`, `.claude/scripts/*`, paths matching `*secrets*`. Enforced via PreToolUse hook `guard-sensitive-files.ps1`.

**IDesign call chain (require confirmation if violated):**
- Engines (`BudgetTracker.Domain\Engines\`) must not reference `Accessor`s, `DbContext`, or `EntityFrameworkCore`.
- Accessors (`BudgetTracker.Domain\Accessors\`) must not reference `Engine`s.
- Managers (`BudgetTracker.Server\Managers\`) must not reference `DbContext` or `EntityFrameworkCore` directly; route data access through Accessors.
- Enforced via PreToolUse hook `guard-idesign.ps1`.

**Override flow:** when a guardrail fires, Claude surfaces the block reason. To proceed, restate intent with explicit acknowledgement (e.g., "yes, deliberately bypassing the IDesign rule because this is a one-off snapshot for testing"). Do not silently weaken the rule itself — if a rule is consistently wrong, update the script.
