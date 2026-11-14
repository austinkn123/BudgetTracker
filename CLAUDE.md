# Claude Instructions for BudgetTracker

You are an expert developer working on the BudgetTracker application. This project uses a specific tech stack and architectural patterns that you must follow.

## Tech Stack
- **Backend**: .NET 9, ASP.NET Core Minimal APIs
- **Frontend**: React + TypeScript + Vite
- **Styling**: Tailwind CSS + Radix primitives (design tokens in `budgettracker.client/src/shared/theme/`; see `project-docs/design.md`)
- **Forms/Validation**: React Hook Form + Zod (`@hookform/resolvers`)
- **Database**: PostgreSQL (Neon) via Npgsql (`Npgsql.EntityFrameworkCore.PostgreSQL`)
- **ORM**: Entity Framework Core (Code-first with Fluent API configurations)
- **Authentication**: AWS Cognito (In progress)
- **Hosting**: a single AWS Lambda behind a Function URL, deployed with SAM (`deploy/template.yaml`; see `deploy/README.md`). Logs go to CloudWatch (14-day retention).

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
- **Testing**: Follow TDD (Red-Green-Refactor). Unit test Engines directly. Test Managers with mocked Engines/Accessors. Accessor tests that need a relational provider (e.g. `ExecuteUpdate`) use EF Core SQLite in-memory — the EF InMemory provider can't translate it. There is no committed real-Postgres test fixture yet.
- **Contracts first**: Define interfaces in `BudgetTracker.Domain/Interfaces/` before implementing.

## Commits & Pull Requests
- **Every commit uses Conventional Commits**: `<type>(<optional scope>): <imperative summary>` — lowercase type, summary ≤ 72 chars, no trailing period.
  - Types: `feat`, `fix`, `perf`, `refactor`, `test`, `docs`, `style`, `build`, `ci`, `chore`, `revert`.
  - Scopes (optional, use the area touched): `transactions`, `plaid`, `categories`, `budget`, `cash-flow`, `money-map`, `dashboard`, `webull`, `auth`, `db`, `client`, `server`, `deploy`.
  - Breaking API/contract change: add `!` after the type/scope (`feat(transactions)!: …`) and a `BREAKING CHANGE:` footer.
  - Body (optional): what and why, wrapped at 72 chars.
  - Examples: `feat(transactions): add infinite-scroll ledger feed`, `fix(plaid): revert windowed transfer pairing`, `feat(db): add Users.DataVersion change counter`.
- One logical change per commit; don't mix unrelated types in one commit.
- **PR titles** follow the same format, since PRs are squash-merged and the title becomes the commit on `main`.

## Context
- Always check `project-docs/` for architectural decisions before suggesting major changes.
- If requirements are unclear, ask the user rather than guessing.
- Jira project: **BUD** on `austinkn123.atlassian.net` — check for existing issues before creating new ones, and link completed work to its ticket when relevant.
- **Jira access via MCP**: The Atlassian MCP server is configured in `.mcp.json` and provides direct Jira access. When asked about any BUD ticket, **ALWAYS call the Atlassian MCP tools** — never say "I can't connect to Jira" without first attempting the call. Common tools: `mcp__atlassian__getIssue` (fetch a specific ticket), `mcp__atlassian__searchIssues` (JQL search), `mcp__atlassian__getAccessibleAtlassianResources` (list resources). If the call fails, report the actual error returned by the tool, not a pre-emptive refusal.
- Local database: **PostgreSQL**. Local dev reads `ConnectionStrings:BudgetTrackerConnection` from `BudgetTracker.Server/appsettings.Development.json` (points at Postgres on `localhost`); the gitignored `appsettings.Local.json` can override it. Production uses Neon, with the connection string in SSM (see `deploy/README.md`). Use the `postgres` MCP server (`.mcp.json`, Postgres MCP Pro via `uvx`, **read-only** `--access-mode=restricted`) to inspect schema, run read queries, and verify migrations. It connects to `BUDGETTRACKER_DATABASE_URI` if set (use a read-only role for Neon), otherwise to the local dev database.

## How to Work
- Do the work in the main thread. Don't spawn subagents unless the user asks, or for a broad read-only search where only the conclusion matters.
- Before backend, test, database, frontend or Jira work, read the matching section of `project-docs/engineering-standards.md` (once per session, not per step).
- `/review` runs the read-only `reviewer` agent only for large diffs; small diffs are reviewed inline.

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
