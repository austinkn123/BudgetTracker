# Observability

The logging, monitoring, and health-check plan for BudgetTracker: what exists today, what we've
decided, and the phased work to get there.

> **Status: Phases 1 and 2 and the email alerts are implemented.** Phase 3 and the multi-user
> checklist (§8) are still to do. Items marked *Proposed* inside the plan still need approval.

BudgetTracker runs as a single AWS Lambda behind a Function URL (`deploy/template.yaml`), backed
by Neon Postgres. Lambda output goes to the CloudWatch log group `/aws/lambda/<function>`, kept
for 14 days. See [`deploy/README.md`](../deploy/README.md) for the hosting setup.

---

## 1. Current state

### Backend

- **Wiring:** lives in `BudgetTracker.Server/Utilities/Observability/`, through
  `AddBudgetTrackerObservability()`, `UseBudgetTrackerObservability()`, and
  `UseBudgetTrackerRequestUser()`. The last one sits between `UseAuthentication` and
  `UseAuthorization`.
- **Logging:** built-in `Microsoft.Extensions.Logging` with the JSON console formatter and scopes
  on. Each scope carries `CorrelationId`, `AwsRequestId`, `XRayTraceId`, and `UserId`. No Serilog,
  no OpenTelemetry.
- **Log messages:** every message is a source-generated `[LoggerMessage]` method with a stable
  event ID. They live in four catalogues: `ObservabilityLog` (1000s), `PlaidLog` (2000s), and
  `WebullLog` (3000s) in `BudgetTracker.Server/Utilities/Observability/`, and `DataLog` (4000s) in
  `BudgetTracker.Domain/Common/`. IDs are never reused or renumbered.
- **Where `ILogger` is used:** only in Managers and background services — `PlaidManager` (which
  also covers sandbox seeding), `WebullManager`, `WebullTradesManager`, `PlaidSyncSweepService`,
  and `WebullCloseSnapshotService`. Endpoints, Engines, and Accessors don't log.
- **Errors:** `UseExceptionHandler` plus ProblemDetails responses that carry `correlationId` and
  no stack trace.
- **Correlation IDs:** middleware reads `X-Correlation-ID` and keeps it if it is 1–64 characters
  of `[A-Za-z0-9-_.]`; otherwise it generates a new GUID. The ID is echoed on every response.
- **Error shapes:** `Result<T>.Error` is still free text, and endpoints still return errors in
  mixed shapes.
- **Health:** anonymous `/health`, with no database call. `/health/ready` requires sign-in and
  checks that the database answers (see §4).
- **Security signal:** the `BudgetTracker.Security` log category records failed JWT
  authentication and 401/403 responses: status, method, and path. For exceptions, only the type
  name is logged.
- **Production log levels:** EF `Database.Command` and `HttpClient` are capped at `Warning`
  outside Development. This is set in code.
- **EF Core:** sensitive-data logging is not enabled. This is good, and we keep it that way.
- **Redaction:** on. A `[LoggerMessage]` parameter tagged `[FinancialData]`, `[CredentialData]`,
  or `[PersonalData]` (`DataClassifications.cs`) is erased before it is written. No log message
  carries a tagged field today; this is the safety net for when one does.
- **Slow queries:** `SlowQueryInterceptor` warns (event 1006) when a database command takes at
  least `Observability:SlowQuery:ThresholdMs` (default 500). It logs the duration, statement type,
  and table names, never the SQL text or parameter values.
- **Sync metrics:** one Information event per sync with outcome, counts, and duration, and no
  financial data: Plaid transaction sync (2017), Webull order-history sync (3005), and Webull
  portfolio fetch (3006).
- **Tracing:** the Lambda has `Tracing: Active`, so the Lambda service records an X-Ray trace per
  invocation. The app uses no X-Ray SDK: it is in maintenance mode and ends support on
  2027-02-25. Traces therefore have no per-call timings for Plaid, Webull, or the database; that
  waits for OpenTelemetry (Phase 3).
- **Saved queries:** seven Logs Insights queries deploy with the stack (`deploy/template.yaml`),
  under "BudgetTracker/" in Saved queries.

### Frontend

- The API client ([`budgettracker.client/src/shared/api.ts`](../budgettracker.client/src/shared/api.ts))
  sends a per-request `X-Correlation-ID`. Failed requests are logged to the console as method,
  path (no query string), status, and correlation ID.
- An `ErrorBoundary` (`src/shared/components/ErrorBoundary.tsx`) wraps each routed page and the
  whole app. A render error shows a fallback with "Try again" and "Reload page"; navigating to
  another page clears it. The error stays in the browser console.

---

## 2. Decisions

| Decision | Choice | Why |
|---|---|---|
| Logging library | Built-in `Microsoft.Extensions.Logging` with the JSON console formatter | Lambda sends stdout to CloudWatch, which can query JSON fields directly. |
| Serilog | Not used | The built-in logger and JSON formatter cover what we need. |
| OpenTelemetry | Not for now | It adds to Lambda startup time and brings extra moving parts. Revisit if we leave CloudWatch. |
| Local viewer | Seq or the Aspire dashboard, optional | Easier to read structured logs locally. Not required to run the app. |

---

## 3. Data protection

This is the most important section. BudgetTracker holds financial data and bank credentials, so
the default is to log **less**.

- **Redaction:** use `Microsoft.Extensions.Compliance.Redaction` to classify and redact fields.
- **Never log:**
  - amounts
  - merchant or payee names
  - account names
  - Plaid access tokens
  - Webull secrets
  - `Authorization` headers
  - email addresses
  - request or response bodies
- **User identity:** log the internal `UserId`, not the Cognito `sub`.
- **EF Core:** `EnableSensitiveDataLogging` is Development-only, and never on in production.
- **Production log levels:** set EF `Database.Command` and `HttpClient` logging to `Warning`, so
  SQL text, parameters, and outbound URLs don't reach CloudWatch.

---

## 4. Health checks

| Endpoint | Auth | Checks | Caller |
|---|---|---|---|
| `/health` | Anonymous | Process is up. **No database call.** | Anything, including automated probes. |
| `/health/ready` | Signed-in users | The database accepts a connection. 200 or 503, with no detail in the body. | **Manual only.** |

`/health/ready` must not be polled: regular database calls keep Neon from scaling to zero.

---

## 5. IDesign fit

Observability is a cross-cutting concern, so it follows the Utility rules in
[`CLAUDE.md`](../CLAUDE.md).

- **Utility:** logging is configured in `BudgetTracker.Server` through an
  `AddBudgetTrackerObservability()` extension method.
- **Endpoints** don't log. Middleware covers them.
- **Managers** log workflow steps and failures, once each.
- **Accessors** throw typed exceptions rather than logging.
- **Engines do not take `ILogger`.** This is a project choice: IDesign allows Engines to call
  Utilities. Engines return outcomes, and the calling Manager logs them.
- *Proposed — pending approval:* an optional `Code` on `Result`, so that free-text error messages
  stay out of logs.
- *Proposed — pending approval:* a `guard-idesign` rule that flags `ILogger` in
  `BudgetTracker.Domain/Engines/`.

---

## 6. Frontend

- **While single-user:** an `ErrorBoundary` that shows a fallback screen. Errors stay in the
  browser console. No home-grown `/api/client-errors` endpoint: it would be replaced by Sentry
  once the app is multi-user, so it isn't worth building.
- **Before multi-user launch: Sentry, errors only.**
  - Session replay **off**: it would capture balances.
  - `sendDefaultPii: false`.
  - A `beforeSend` scrubber that drops amounts, payees, account names, and emails.

---

## 7. Phases

### Phase 1 — done

- JSON console logs, with the production level overrides from §3.
- Exception handler plus ProblemDetails responses that carry a correlation ID.
- Correlation-ID middleware. Each log scope includes `CorrelationId`, the Lambda request ID, and
  `UserId`. The client sends `X-Correlation-ID`.
- `/health`.
- Log failed sign-ins and denied (401/403) requests with `UserId` where known. This becomes the
  security signal once other people have accounts.

**Tests**

- A ProblemDetails response has a correlation ID and no stack trace.
- `/health` returns 200 without sign-in.
- A `FakeLogger` test (from `Microsoft.Extensions.Diagnostics.Testing`) checks that the correlation fields are present and that no PII appears.

### Phase 2 — done

- Source-generated `LoggerMessage` methods with event IDs.
- Redaction tags on sensitive fields.
- `/health/ready`.
- An EF Core slow-query interceptor.
- AWS X-Ray tracing. Done as Lambda active tracing plus the trace ID on each log line; the X-Ray
  SDK was left out (see §1, Tracing).
- The `ErrorBoundary` (fallback screen only; see §6).
- Plaid and Webull sync metrics.
- Saved CloudWatch Logs Insights queries.

**Tests:** one for each item above.

### Email alerts — done

In `deploy/template.yaml`, all emailing the `budgettracker-alerts` SNS topic:

- `budgettracker-function-errors`: the Lambda `Errors` metric, for crashes and timeouts.
- `budgettracker-server-errors`: a log metric filter counting event 1004, which the exception handler
  writes for every 500 response. A handled 500 still completes the invocation, so `Errors` alone
  would miss it. It counts from the app's logs rather than the Function URL `Url5xxCount` metric,
  whose dimensions are not clearly documented; a wrong dimension would be an alarm that never fires.
- Each alarm fires on the first occurrence in a 5-minute window, and emails again on recovery.
- Optional AWS Budgets cost alert: 80% of actual and 100% of forecast monthly cost. It covers the
  whole account, to catch runaway costs from a bug or abuse of the public Function URL.

The address (`AlertEmail`) and budget (`MonthlyBudgetUsd`) are deploy parameters, so neither is
committed. Empty or 0 means no email subscription and no budget. See `deploy/README.md`.

### Phase 3

- OpenTelemetry export to Grafana or Datadog.
- Dashboards and service-level targets.
- Anything from the multi-user checklist (§8) not already done.

---

## 8. Before multi-user launch

BudgetTracker is **single-user today, and very likely multi-user later**. Don't build
multi-user observability ahead of multi-user features. Instead, the multi-user launch is gated on
this checklist:

- [ ] Sentry for frontend errors, configured as in §6.
- [ ] An audit log of who viewed or changed what (e.g. linked a bank, edited a transaction):
      a table written through an Accessor, schema designed by the DBA.
- [ ] Log retention raised from 14 to at least 30 days, for supporting other users.
- [ ] Account deletion also covers that user's log data. Short retention is the simplest way to
      meet this.
- [ ] CloudWatch alarms on spikes in failed sign-ins and denied requests.

Phase 1 work, `UserId` on every log line, and redaction already assume multiple users, so they
don't wait for this list.

---

## 9. Open questions

1. ~~Is BudgetTracker single-user or multi-user?~~ **Answered:** single-user now, very likely
   multi-user later (see §8).
2. ~~What is the monthly monitoring budget?~~ **Answered:** no fixed budget. A cost alert is
   deferred with the other email alerts (see Phases).
3. Is Lambda + Neon the long-term setup?
4. What log retention is right, given Plaid data? 14 days today; at least 30 once multi-user.
5. ~~Where should alerts go?~~ **Answered:** email, via SNS, once alerts are set up (deferred).
6. How much added cold-start time is acceptable?
