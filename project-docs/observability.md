# Observability

The logging, monitoring, and health-check plan for BudgetTracker: what exists today, what we've
decided, and the phased work to get there.

> **Status: Proposed — not implemented.** Nothing below beyond "Current state" exists in the
> codebase yet. Items marked *Proposed* inside the plan still need approval on top of that.

BudgetTracker runs as a single AWS Lambda behind a Function URL (`deploy/template.yaml`), backed
by Neon Postgres. Lambda output goes to the CloudWatch log group `/aws/lambda/<function>`, kept
for 14 days. See [`deploy/README.md`](../deploy/README.md) for the hosting setup.

---

## 1. Current state

### Backend

- **Logging:** default `Microsoft.Extensions.Logging`. No JSON output, no Serilog, no OpenTelemetry.
- **Where `ILogger` is used:** only in Managers and background services — `PlaidManager`,
  `WebullManager`, `WebullTradesManager`, `SandboxSeedManager`, `PlaidSyncSweepService`, and
  `WebullCloseSnapshotService`. Endpoints, Engines, and Accessors don't log.
- **Errors:** no `UseExceptionHandler` and no ProblemDetails. No correlation IDs.
- **Error shapes:** `Result<T>.Error` is free text, and endpoints return errors in mixed shapes.
- **Health:** no health checks.
- **EF Core:** sensitive-data logging is not enabled. This is good, and we keep it that way.

### Frontend

- Client errors only go to `console.error` ([`budgettracker.client/src/shared/api.ts`](../budgettracker.client/src/shared/api.ts)).
- No React `ErrorBoundary`.

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
| `/health/ready` | Not decided | Includes a database check. | **Manual only.** |

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

### Phase 1

- JSON console logs, with the production level overrides from §3.
- Exception handler plus ProblemDetails responses that carry a correlation ID.
- Correlation-ID middleware. Each log scope includes `CorrelationId`, the Lambda request ID, and
  `UserId`. The client sends `X-Correlation-ID`.
- `/health`.
- CloudWatch alarms on the Lambda `Errors` metric and on a 5xx metric filter, notifying through SNS.
- Log failed sign-ins and denied (401/403) requests with `UserId` where known. This becomes the
  security signal once other people have accounts.

**Tests**

- A ProblemDetails response has a correlation ID and no stack trace.
- `/health` returns 200 without sign-in.
- A `FakeLogger` test (from `Microsoft.Extensions.Diagnostics.Testing`) checks that the correlation fields are present and that no PII appears.

### Phase 2

- Source-generated `LoggerMessage` methods with event IDs.
- Redaction tags on sensitive fields.
- `/health/ready`.
- An EF Core slow-query interceptor.
- AWS X-Ray tracing.
- The `ErrorBoundary` (fallback screen only; see §6).
- Plaid and Webull sync metrics.
- Saved CloudWatch Logs Insights queries.

**Tests:** one for each item above.

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
2. What is the monthly monitoring budget?
3. Is Lambda + Neon the long-term setup?
4. What log retention is right, given Plaid data? 14 days today; at least 30 once multi-user.
5. Where should alerts go: email, SMS, or Slack?
6. How much added cold-start time is acceptable?
