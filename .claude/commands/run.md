---
description: Launch backend and frontend dev servers in parallel
---

Start the BudgetTracker development environment.

Steps:
1. Launch the .NET backend in the background using the Bash tool with `run_in_background: true`:
   `dotnet run --project BudgetTracker.Server --launch-profile https`
2. In parallel, launch the Vite frontend in the background:
   `npm --prefix budgettracker.client run dev`
3. Wait ~5 seconds for both to come up, then check both background processes with the BashOutput tool. Report:
   - Backend status (expecting `Now listening on: https://localhost:7134`)
   - Frontend status (expecting `Local: https://localhost:53608`)

If either fails to start, diagnose and suggest a fix. Common issues:
- **Port already in use** → another instance is running. Suggest stopping it.
- **LocalDB not running** → `sqllocaldb start MSSQLLocalDB`
- **Missing dev cert** → `dotnet dev-certs https --trust`
- **Build error** → show the relevant error excerpt and stop; do not retry blindly.
- **`Invalid column name 'X'`** → DB out of sync. Suggest `/migrate` or `dotnet ef database update`.

Do NOT kill the background processes when reporting — leave them running so the user can use the app. Tell the user the URL to open: `https://localhost:53608`.
