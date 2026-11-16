# Engineering Standards

Reference for working on BudgetTracker. Read the section for the area you are
touching; you don't need the whole file every time. CLAUDE.md has the short
version.

## Backend: IDesign

**Taxonomy**
- **Managers** (`BudgetTracker.Server/Managers/`): orchestrate and own
  transaction boundaries. Sequencing and conditional flow are fine; domain
  rules and calculations are not.
- **Engines** (`BudgetTracker.Domain/Engines/`): pure domain logic. Stateless,
  no I/O, no transactions. Receive data as parameters, return results.
- **Accessors** (`BudgetTracker.Domain/Accessors/`): every I/O boundary — EF
  Core, external HTTP (Plaid, Webull), Cognito, files, queues.
- **Utilities**: non-I/O cross-cutting only (logging, config, retry, metrics).
  If it does I/O, it is an Accessor.

**Call chain:** Managers → Engines + Accessors. Engines → Utilities only.
Accessors → Utilities only. No Engine ↔ Accessor calls, no Manager → Manager.

**Fetch → Compute → Persist:** the Manager fetches via Accessors, passes the
data into Engines, then persists via Accessors. If an Engine needs more data,
the Manager fetches it and calls the Engine again — Engines never ask for I/O.

**Contracts first:** add the interface in `BudgetTracker.Domain/Interfaces/`
before the implementation. Breaking contract changes get `!` in the commit and
a `BREAKING CHANGE:` footer.

**Security placement:** authentication → Accessor (Cognito); authorization
policy → Engine; audit logging → Utility. Authorization logic in a Manager or
Accessor is a violation.

**Stop and ask the user** before: adding a new bounded context, a Manager
calling another Manager, or a cross-cutting concern that fits no existing
Utility. Don't copy pre-existing violations; leave
`// TODO: <ClassName> violates IDesign layering` instead.

**Feature check (new feature):** 1) which service type owns each
responsibility, 2) do call chains comply, 3) is each service testable in
isolation, 4) does each service have one axis of change.

## Testing

- TDD: Red → Green → Refactor. Green = simplest non-hardcoded code that passes.
- xUnit; behavior-named tests (`Should_ReturnError_When_AmountIsNegative`),
  one behavior per test.
- Minimum set per public method / user action: happy path, each validation
  failure, null/empty inputs, boundary values, error path.
- **Engines:** unit tests, no mocks.
- **Managers:** mocked Engines + Accessors; assert orchestration order.
- **Accessors:** EF InMemory provider by default; SQLite in-memory when a
  relational provider is needed (`ExecuteUpdate`, see
  `DataVersionAccessorTests`). Never mock `DbContext`. Neither covers
  Npgsql-specific SQL, constraints or transactions — call that out when it
  matters.
- **Frontend forms:** each schema in `src/shared/validation/` covers valid
  input, each invalid type and boundaries. Playwright asserts errors render at
  the field level.
- Hard-to-test code is a design smell: fix the boundary, don't mock around it.

## Database (PostgreSQL / EF Core)

- Priority when goals conflict: rollback safety → data integrity → backward
  compatibility → measured performance → normalization purity.
- 3NF by default; document any intentional denormalization and what keeps it
  consistent.
- Prefer additive, reversible migrations. Review every generated migration;
  flag `DropColumn` / `DropTable` / renames.
- `DROP`, `TRUNCATE`, mass `UPDATE`/`DELETE`: rollback plan first, any
  environment.
- Indexes: B-tree by default; consider partial and `INCLUDE` indexes; GIN/BRIN
  only with a reason. Check SARGability before adding indexes.
- Use the `postgres` MCP server (read-only) to read schema and plans rather than
  guessing. Don't `SELECT *` on tables with PII (`Email`, `CognitoSub`, names)
  and don't echo PII values.

## Frontend

- `project-docs/design.md` governs look and behavior.
  `project-docs/ui-modernization.md` is superseded — don't follow it.
- `src/shared/theme/tokens.ts` is the only source for colour, radius, shadow
  and motion. No hex values or hand-written CSS variable names (enforced by
  `designTokenContract.test.ts`); use Tailwind utilities or `cssVar()`.
- Tailwind only. MUI/emotion were removed (BUD-20) and are banned by ESLint.
- Use primitives in `src/shared/components/ui/`. `Input`/`Select`/`Checkbox`
  are react-hook-form-bound; `InlineSelect`/`InlineCheckbox` are standalone.
- Every interactive element is keyboard-reachable with an accessible name.
- Lazy-load routes and heavy components unless the chunk is negligible.
- Inline styles only when no token/utility fits, with a comment saying why.

## Product / Jira

- Story: "As a [user], I want [action], so that [benefit]." AC as numbered
  Given/When/Then; each must be testable.
- Search BUD for duplicates before creating an issue; create only after the
  user approves. Types: Story (feature), Bug, Task (technical), Epic.
- Don't invent metrics or user feedback — ask for the source.
