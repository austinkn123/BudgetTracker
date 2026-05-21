---
description: Scaffold a new IDesign vertical slice (Model + Accessor + Engine + Manager + Endpoint + DTO + Zod schema)
argument-hint: <FeatureName>
---

Scaffold a complete IDesign vertical slice for `$ARGUMENTS` (PascalCase, singular noun — e.g., `Goal`, `Reminder`, `RecurringTransaction`).

If `$ARGUMENTS` is empty or invalid, ask the user for a name first.

**Before writing any code, ask the user:**
1. What fields should `$ARGUMENTS` have? (name + type for each, e.g., `Title: string (max 100)`, `TargetAmount: decimal`, `DueDate: DateTime`)
2. Does it belong to a User? (almost always yes — `UserId` FK)
3. Any other foreign keys? (e.g., `CategoryId`, `BudgetPlanId`)
4. Any unique constraints?

After confirmation, study the existing patterns by reading these template files:
- Model: [BudgetTracker.Domain/Models/Category.cs](BudgetTracker.Domain/Models/Category.cs)
- Configuration: [BudgetTracker.Domain/Data/Configurations/CategoryConfiguration.cs](BudgetTracker.Domain/Data/Configurations/CategoryConfiguration.cs)
- Accessor + interface: [BudgetTracker.Domain/Accessors/CategoryAccessor.cs](BudgetTracker.Domain/Accessors/CategoryAccessor.cs) + [BudgetTracker.Domain/Interfaces/Accessors/ICategoryAccessor.cs](BudgetTracker.Domain/Interfaces/Accessors/ICategoryAccessor.cs)
- Engine + interface: [BudgetTracker.Domain/Engines/CategoryEngine.cs](BudgetTracker.Domain/Engines/CategoryEngine.cs) + [BudgetTracker.Domain/Interfaces/Engines/ICategoryEngine.cs](BudgetTracker.Domain/Interfaces/Engines/ICategoryEngine.cs)
- Manager + interface: [BudgetTracker.Server/Managers/CategoryManager.cs](BudgetTracker.Server/Managers/CategoryManager.cs) + [BudgetTracker.Domain/Interfaces/Managers/ICategoryManager.cs](BudgetTracker.Domain/Interfaces/Managers/ICategoryManager.cs)
- Endpoint: [BudgetTracker.Server/Endpoints/CategoryEndpoints.cs](BudgetTracker.Server/Endpoints/CategoryEndpoints.cs)
- Zod schema: [budgettracker.client/src/shared/validation/categorySchema.ts](budgettracker.client/src/shared/validation/categorySchema.ts)
- Service: [budgettracker.client/src/shared/services/categoryService.ts](budgettracker.client/src/shared/services/categoryService.ts)

**Generate (with `$ARGUMENTS` as the feature name, `$args` as lowercase first letter):**

Backend — `BudgetTracker.Domain`:
1. `Models/$ARGUMENTS.cs`
2. `Data/Configurations/${ARGUMENTS}Configuration.cs`
3. `Interfaces/Accessors/I${ARGUMENTS}Accessor.cs`
4. `Accessors/${ARGUMENTS}Accessor.cs`
5. `Interfaces/Engines/I${ARGUMENTS}Engine.cs`
6. `Engines/${ARGUMENTS}Engine.cs` (basic validation — at minimum check required fields and ownership)
7. `Interfaces/Managers/I${ARGUMENTS}Manager.cs`

Backend — `BudgetTracker.Server`:
8. `Managers/${ARGUMENTS}Manager.cs`
9. `Endpoints/${ARGUMENTS}Endpoints.cs` (CRUD: GET all, GET by id, POST, PUT, DELETE)

Backend — wiring:
10. Add `modelBuilder.ApplyConfiguration(new ${ARGUMENTS}Configuration());` (or rely on `ApplyConfigurationsFromAssembly` if already used) in `BudgetTrackerDbContext.OnModelCreating`
11. Add `app.Map${ARGUMENTS}Endpoints();` (or equivalent) in `Program.cs` next to the other endpoint registrations
12. Add `DbSet<$ARGUMENTS> ${ARGUMENTS}s { get; set; }` to `BudgetTrackerDbContext`

Frontend — `budgettracker.client/src`:
13. Extend `shared/types/api.ts` with the `$ARGUMENTS` type
14. `shared/validation/${args}Schema.ts` (Zod schema mirroring backend validation)
15. `shared/services/${args}Service.ts` (axios CRUD)
16. `features/${args}s/hooks/use${ARGUMENTS}s.ts` (TanStack Query hook)

**After scaffolding:**
- Run `dotnet build` to confirm everything compiles. Fix any errors.
- Tell the user the next step: `/migrate Add$ARGUMENTS`
- Do NOT run the migration automatically — let the user review the scaffold first.

Keep all generated code consistent with the existing style. Use `Result<T>` for Manager returns. Honor `ICurrentUserProvider` for user scoping in Accessors.
