---
description: Self-review the current diff before committing
---

Quick self-review of uncommitted changes before commit.

Steps:

1. **Survey the diff:**
   - `git status` — what's changed
   - `git diff --stat` — size of changes per file
   - `git diff` — the actual diff (read it; don't dump it all to the user)

2. **Verify it builds (run in parallel):**
   - `dotnet build BudgetTracker.sln` — backend
   - `npm --prefix budgettracker.client run build` — frontend (this includes `tsc` type-check)

3. **Delegate to agents (run in parallel via the Agent tool):**
   - `tony-architect` — does anything in the diff violate the IDesign call chain, leak business logic into the wrong layer, or introduce architectural risk? Be specific with file paths and line numbers.
   - `silvio-qa` — what's the test coverage gap in this diff? Are there edge cases or failure modes worth covering before this ships?

4. **Synthesize a final checklist** in this format:
   ```
   ✅ What looks good
   - ...

   ⚠ Worth considering
   - ...

   🚫 Blockers (fix before commit)
   - Build failures
   - Architectural violations Tony flagged
   - Security or data-loss issues
   ```

5. **Tone:** This is a solo side project. Skip pedantic style nitpicks. Focus on real issues — correctness, broken contracts, hidden bugs, security, anything that would break in production. If the diff is small and clean, say so in one line and stop.

If there are no uncommitted changes, say so and exit.
