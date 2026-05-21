---
description: Create and apply a new EF Core migration
argument-hint: <MigrationName>
---

Create and apply an EF Core migration.

Arguments: `$ARGUMENTS`

Steps:

1. **Validate the migration name.** If `$ARGUMENTS` is empty, ask the user for one. Suggest PascalCase like `AddCognitoSubToUser`, `RenameXToY`, `SeedDefaultCategories`. Reject names with spaces or special characters.

2. **Check for locked DLLs.** Try a quick `dotnet build BudgetTracker.Domain` (no full solution). If it fails with `MSB3027` / `file is being used by another process`, stop and tell the user:
   > Visual Studio is debugging the backend. Stop it (Shift+F5) and re-run `/migrate $ARGUMENTS`.
   Do NOT proceed.

3. **Add the migration:**
   `dotnet ef migrations add $ARGUMENTS --project BudgetTracker.Domain --startup-project BudgetTracker.Server`

4. **Show the generated file.** Find the newest file under `BudgetTracker.Domain/Migrations/` and read it. Print a concise summary of the schema changes (added columns, dropped columns, new tables, new indexes). Highlight anything destructive (DropColumn, DropTable) in a warning tone.

5. **Confirm before applying.** Ask: "Apply this migration to the LocalDB now? (yes / no / show me the SQL)". If they ask for SQL, run `dotnet ef migrations script --idempotent --project BudgetTracker.Domain --startup-project BudgetTracker.Server` and show the relevant section.

6. **Apply on confirmation:**
   `dotnet ef database update --project BudgetTracker.Domain --startup-project BudgetTracker.Server`

7. **Verify.** Report the final state — migration applied, DB schema updated. If applying failed, suggest `dotnet ef migrations remove` to roll back the migration file before retrying.

Be terse and pragmatic — this is a solo side project, not a prod deployment.
