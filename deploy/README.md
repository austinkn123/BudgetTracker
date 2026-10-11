# Deploying BudgetTracker to AWS Lambda (personal account)

One Lambda function serves both the API (`/api/*`) and the React SPA (`wwwroot`) behind a public Function URL.
The database is Neon Postgres. Everything runs in **us-east-2**.

| Piece | Choice | Why |
|---|---|---|
| Runtime | `provided.al2023`, arm64, self-contained .NET 9, executable named `bootstrap` | Lambda has no managed .NET 9 zip runtime (managed .NET is 8 and 10). |
| Front door | Lambda Function URL, `AuthType: NONE` | Auth happens in the app: Cognito JWT on `/api`, Plaid-signed JWT on the webhook. |
| Secrets | SSM Parameter Store under `/budgettracker`, read at cold start | CloudFormation can't put `ssm-secure` (SecureString) values into Lambda environment variables. |
| Background sync | Disabled on Lambda | The client calls `POST /api/plaid/sync?staleAfterHours=N` instead of the hosted sweep. |
| Data Protection keys | `DataProtectionKeys` table | Every instance shares one key ring, so encrypted Plaid tokens survive cold starts. |

## Prerequisites

- AWS CLI v2, configured for your account (`aws configure`)
- AWS SAM CLI (a recent version — it creates both Function URL permissions Lambda now requires)
- .NET 9 SDK, `dotnet-ef`, and Amazon.Lambda.Tools: `dotnet tool install -g Amazon.Lambda.Tools`
- Node/npm. Packaging runs the client's `npm run build` through the `.esproj`, so the client must compile.
- A Neon project. Use the **direct** (non-pooler) connection string.

## 1. Store configuration in SSM

Parameter names map to .NET configuration keys: `/budgettracker/Plaid/Secret` becomes `Plaid:Secret`.
Use `SecureString` for secrets. The app loads everything under the path, decrypting as it goes.

```bash
REGION=us-east-2

aws ssm put-parameter --region $REGION --type SecureString --overwrite \
  --name /budgettracker/ConnectionStrings/BudgetTrackerConnection \
  --value "Host=<neon-host>;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require"

aws ssm put-parameter --region $REGION --type SecureString --overwrite \
  --name /budgettracker/Plaid/ClientId --value "<plaid-client-id>"

aws ssm put-parameter --region $REGION --type SecureString --overwrite \
  --name /budgettracker/Plaid/Secret --value "<plaid-secret>"

aws ssm put-parameter --region $REGION --type String --overwrite \
  --name /budgettracker/Plaid/Environment --value "sandbox"          # or "production"

aws ssm put-parameter --region $REGION --type String --overwrite \
  --name /budgettracker/Plaid/BaseUrl --value "https://sandbox.plaid.com"   # or https://production.plaid.com
```

Cognito settings (`Region`, `UserPoolId`, `ClientId`, `Authority`) are not secret and already ship in
`appsettings.json`. To override them without a rebuild, add parameters under `/budgettracker/Cognito/...`,
for example:

```bash
aws ssm put-parameter --region $REGION --type String --overwrite \
  --name /budgettracker/Cognito/ClientId --value "<app-client-id>"
```

Don't create `/budgettracker/Plaid/WebhookUrl` or `/budgettracker/Plaid/RedirectUri` yet. Neither URL exists until step 5.

## 2. Create the schema in Neon

Run this from the repo root. It applies `InitialCreate`, which includes the `DataProtectionKeys` table.

```bash
dotnet ef database update \
  --project BudgetTracker.Domain --startup-project BudgetTracker.Server \
  --connection "Host=<neon-host>;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require"
```

## 3. Build the Lambda package

```bash
cd BudgetTracker.Server
dotnet lambda package -c Release -f net9.0 --function-architecture arm64 \
  --msbuild-parameters "--self-contained true -p:LambdaCustomRuntime=true -p:DebugType=None" \
  --output-package ../deploy/artifacts/budgettracker.zip
cd ..
```

- `-p:LambdaCustomRuntime=true` renames the executable to `bootstrap` and turns on invariant globalization,
  because the AL2023 base has no ICU. See `BudgetTracker.Server.csproj`.
- `dotnet lambda package` keeps the Unix execute bit on `bootstrap` even when you build on Windows.
  A plain `Compress-Archive` of `dotnet publish` output would drop it, and the function wouldn't start.
- The zip is about 50 MB, which is over the direct-upload limit. `sam deploy` uploads it through S3 (`resolve_s3 = true`).

`sam build` is intentionally not used. The template's `CodeUri` points at this prebuilt zip.

## 4. Deploy

```bash
cd deploy
sam deploy --guided      # first time; accept the samconfig.toml defaults (stack "budgettracker", us-east-2)
# later: sam deploy
```

The stack output `FunctionUrl` is the app's public address, for example `https://abc123.lambda-url.us-east-2.on.aws/`.

## 5. After the first deploy

1. **Plaid webhook and OAuth redirect.** Point Plaid at the function, then deploy again or wait for the next cold
   start. SSM is read once per cold start. `<FunctionUrl>` already ends in `/`:

   ```bash
   aws ssm put-parameter --region us-east-2 --type String --overwrite \
     --name /budgettracker/Plaid/WebhookUrl --value "<FunctionUrl>api/plaid/webhook"
   aws ssm put-parameter --region us-east-2 --type String --overwrite \
     --name /budgettracker/Plaid/RedirectUri --value "<FunctionUrl>plaid-oauth"
   aws lambda update-function-configuration --region us-east-2 --function-name budgettracker \
     --description "plaid urls set $(date +%s)"   # forces fresh instances
   ```

   New links register the webhook automatically. Links made before this point don't: without the hosted sweep,
   nothing calls `/item/webhook/update` for them on Lambda, so relink those banks (or rely on `/sync`).

   If `RedirectUri` is empty or unset, `redirect_uri` is left out of link tokens. OAuth banks (Chase, BofA) still
   work on desktop through a popup. The redirect is the fallback for phones and in-app browsers that block popups.
2. **Cognito.** If the client uses the Hosted UI or OAuth redirects, add the Function URL to the app client's
   callback and sign-out URLs. Direct SRP sign-in from the SPA needs no change.
3. **Plaid dashboard.** Add `<FunctionUrl>plaid-oauth`, exactly as set in SSM, to Allowed redirect URIs. It must be
   HTTPS with no `#`. To test locally on sandbox, also add `https://localhost:53608/plaid-oauth` and set
   `Plaid:RedirectUri` to it in `BudgetTracker.Server/appsettings.Local.json`.
4. **Smoke test.** Open `<FunctionUrl>`, sign in, link a sandbox bank (`user_good` / `pass_good`), then refresh.

## Sign-up

Self sign-up is off on pool `us-east-2_SNf5BlMBR` (since 2026-10-10). The Function URL is public, so with it on,
a stranger could create an account and link banks billed to your Plaid account. Existing users still sign in.

```powershell
./deploy/cognito-signup.ps1           # disable: admin-created users only
./deploy/cognito-signup.ps1 -Enable   # re-enable
```

`UpdateUserPool` resets every field you omit to its default, so the script backs the pool JSON up to `$env:TEMP`
first and sends every setting back unchanged except `AllowAdminCreateUserOnly`. Use a current AWS CLI v2. An older
one (2.18.10, for example) can't see newer fields such as `UserPoolTier` or `IssuerConfiguration`, so it can't send
them back. Console alternative: Cognito → user pool → Sign-up → Self-service sign-up.

Re-enabling also needs the client pages, which `6884d21` removed. Restore the two deleted pages:

```bash
git checkout 6884d21~1 -- budgettracker.client/src/pages/SignUpPage.tsx budgettracker.client/src/pages/ConfirmSignUpPage.tsx
```

The same commit also removed the `/signup` and `/confirm` routes from `App.tsx`, the "Sign up" link from
`LoginPage.tsx`, `signUp`/`confirmSignUp`/`resendCode` from `auth/AuthContext.tsx` (the context type now lives in
`auth/authContextValue.ts`), and `signUpSchema`/`confirmSignUpSchema` from `shared/validation/auth.ts`. Those files
have changed since, so don't check them out from `6884d21~1`: you'd lose later work such as the `/plaid-oauth` route.
Re-add those pieces by hand from `git show 6884d21 -- <path>`.

## Going live (sandbox → real banks)

1. **Plaid plan.** In the Plaid Dashboard, confirm Trial eligibility (teams created on or after 2026-04-15): 10 free
   production Items, and removing an Item doesn't free its slot. Otherwise apply for Pay-as-you-go.
2. **App profile.** Fill in Application display information and Company information. Some banks reject apps with
   empty profiles. On Trial the security questionnaire isn't required, Chase included.
3. **Production secret.** Copy it from the dashboard. It differs from the sandbox secret.
4. **Switch environment.** Set these in SSM and force fresh instances (step 5). To link from your machine instead,
   set the same `Plaid` keys in `BudgetTracker.Server/appsettings.Local.json`.

   ```bash
   aws ssm put-parameter --region us-east-2 --type String --overwrite \
     --name /budgettracker/Plaid/Environment --value "production"
   aws ssm put-parameter --region us-east-2 --type String --overwrite \
     --name /budgettracker/Plaid/BaseUrl --value "https://production.plaid.com"
   aws ssm put-parameter --region us-east-2 --type SecureString --overwrite \
     --name /budgettracker/Plaid/Secret --value "<production-secret>"
   ```
5. **Relink.** Sandbox Items don't carry over.

## Reconnecting a bank

When a bank needs a new sign-in (`ITEM_LOGIN_REQUIRED` on sync, or a `PENDING_DISCONNECT` / `PENDING_EXPIRATION`
webhook), its card in Settings shows "Needs sign-in" with a **Reconnect** button, and the dashboard shows a warning.
Reconnect uses Plaid update mode, which keeps the same Item, so no new Trial slot is used. Unlinking and relinking
would use one. In Development, `POST /api/plaid/sandbox/connections/{plaidItemId}/reset-login` forces the state on a
sandbox Item for testing; the next sync flags it.

## Notes and caveats

- **Stored Plaid tokens from before this change can't be decrypted.** The Data Protection key ring moved from the
  local machine store to the database. The fresh migration starts with no Plaid items anyway; relink banks after deploying.
- **Data Protection keys sit in Postgres unencrypted** (no XML encryptor is configured). Anyone with database read
  access plus the ciphertext can decrypt Plaid access tokens. A token alone can't call Plaid, though: every request
  also needs the `client_id` and `secret`, which live in SSM, not the database. So a database-only leak exposes stored
  transaction data but not live bank access. Still treat the Neon credentials as sensitive.
- `ASPNETCORE_ENVIRONMENT` is `Production`, so the `/api/plaid/sandbox/*` routes and the OpenAPI document return 404.
- `now()` column defaults use the database session time zone. Neon defaults to UTC. Application-written
  timestamps are always `DateTime.UtcNow`.
- Cold start costs one `GetParametersByPath` call and the EF model build. If that hurts, raise memory, since CPU
  scales with it.
- Rotating a secret: update the SSM parameter, then force new instances (any configuration update does it).
