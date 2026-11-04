<#
.SYNOPSIS
    Create (or refresh) a demo login filled with fictional data, so the app can be shown without real finances.

.DESCRIPTION
    Creates the Cognito user if it is missing (admin-created, verified, no invite email) and sets its password,
    then runs demo-seed.sql for that user's sub. Re-run it before a demo: the seed wipes the demo user's data and
    rebuilds it relative to today. The seed refuses to touch a user with a real Plaid link.

    Don't connect Webull or link a bank while signed in as the demo user: those use your own credentials.

.PARAMETER Password
    Password to set on the demo login. Cognito requires 8+ characters with upper, lower, number and symbol.

.PARAMETER ConnectionString
    Npgsql connection string of the database to seed. Defaults to the one in appsettings.Local.json.
    For the deployed app, pass the Neon string ("Host=...;SSL Mode=Require").

.EXAMPLE
    ./deploy/demo-account.ps1 -Password 'Choose-1-Now!'
    ./deploy/demo-account.ps1 -Password 'Choose-1-Now!' -ConnectionString $neon
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Password,
    [string]$Email = 'demo@example.com',
    [string]$ConnectionString,
    [string]$UserPoolId = 'us-east-2_SNf5BlMBR',
    [string]$Region = 'us-east-2'
)

$ErrorActionPreference = 'Stop'

function Get-DemoSub {
    # "User does not exist" is an expected answer here; under Stop, Windows PowerShell would throw on its stderr.
    $ErrorActionPreference = 'Continue'
    # --output is explicit throughout: with no default output format configured, the CLI exits 255.
    aws cognito-idp admin-get-user --region $Region --user-pool-id $UserPoolId --username $Email `
        --query "UserAttributes[?Name=='sub'].Value | [0]" --output text 2>$null
}

# 1. Cognito login
$sub = Get-DemoSub
if ($LASTEXITCODE -ne 0) {
    aws cognito-idp admin-create-user --region $Region --user-pool-id $UserPoolId --username $Email `
        --user-attributes Name=email,Value=$Email Name=email_verified,Value=true --message-action SUPPRESS --output json | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "admin-create-user failed (exit $LASTEXITCODE)." }
    $sub = Get-DemoSub
    if ($LASTEXITCODE -ne 0) { throw "admin-get-user failed (exit $LASTEXITCODE)." }
    Write-Host "Created Cognito user $Email"
}

aws cognito-idp admin-set-user-password --region $Region --user-pool-id $UserPoolId --username $Email `
    --password $Password --permanent --output json | Out-Null
if ($LASTEXITCODE -ne 0) { throw "admin-set-user-password failed (exit $LASTEXITCODE)." }

# 2. Seed. psql takes libpq settings, so the Npgsql string is mapped onto PG* environment variables.
if (-not $ConnectionString) {
    $settings = Get-Content (Join-Path $PSScriptRoot '..\BudgetTracker.Server\appsettings.Local.json') -Raw | ConvertFrom-Json
    $ConnectionString = $settings.ConnectionStrings.BudgetTrackerConnection
}

$pgVars = @{ 'host' = 'PGHOST'; 'port' = 'PGPORT'; 'database' = 'PGDATABASE'; 'username' = 'PGUSER'; 'password' = 'PGPASSWORD'; 'ssl mode' = 'PGSSLMODE' }
foreach ($pair in $ConnectionString.Split(';', [StringSplitOptions]::RemoveEmptyEntries)) {
    $key, $value = $pair.Split('=', 2)
    $name = $pgVars[$key.Trim().ToLowerInvariant()]
    if ($name -eq 'PGSSLMODE') { $value = $value.ToLowerInvariant() }
    if ($name) { Set-Item "env:$name" $value.Trim() }
}

$psql = (Get-Command psql -ErrorAction SilentlyContinue).Source
if (-not $psql) { $psql = (Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' | Select-Object -Last 1).FullName }
if (-not $psql) { throw 'psql not found. Install the PostgreSQL client tools or put psql on PATH.' }

& $psql -v ON_ERROR_STOP=1 --single-transaction -q -v "sub=$sub" -v "today=$(Get-Date -Format yyyy-MM-dd)" -f (Join-Path $PSScriptRoot 'demo-seed.sql')
if ($LASTEXITCODE -ne 0) { throw "Seeding failed (psql exit $LASTEXITCODE)." }

Write-Host "Demo login ready on $env:PGHOST/$env:PGDATABASE -> $Email"
