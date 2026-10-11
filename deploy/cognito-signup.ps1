<#
.SYNOPSIS
    Turn self sign-up on or off for the BudgetTracker Cognito user pool.

.DESCRIPTION
    UpdateUserPool resets every setting it is not sent back to its default, so a bare
    "flip the sign-up flag" call would silently wipe the password policy, verification, recovery, etc.
    This script reads the live pool, backs the full description up to $env:TEMP, and re-sends the
    settable fields unchanged except AdminCreateUserConfig.AllowAdminCreateUserOnly.

    Use a current AWS CLI v2. An old CLI can't see fields newer than itself (e.g. UserPoolTier,
    IssuerConfiguration), so it can't send them back; AWS keeps omitted new fields for older clients,
    but a current CLI round-trips them explicitly.

.PARAMETER Enable
    Allow anyone to sign up. Omit it to restrict sign-up to admin-created users.

.PARAMETER UserPoolId
    Cognito user pool to update.

.PARAMETER Region
    AWS region of the pool.

.EXAMPLE
    ./deploy/cognito-signup.ps1 -Enable    # open sign-up
    ./deploy/cognito-signup.ps1            # admin-created users only
#>
[CmdletBinding()]
param(
    [switch]$Enable,
    [string]$UserPoolId = 'us-east-2_SNf5BlMBR',
    [string]$Region = 'us-east-2'
)

$ErrorActionPreference = 'Stop'

# Read-only fields DescribeUserPool returns that UpdateUserPool rejects. Everything else is sent back
# as-is. A blocklist (not an allowlist) means settings AWS adds later are preserved; an unknown
# read-only field makes the CLI reject the request before anything changes, which fails safe.
# Name is excluded because renaming isn't this script's job (UpdateUserPool calls it PoolName).
$readOnlyFields = @(
    'Id', 'Name', 'Arn', 'CreationDate', 'LastModifiedDate', 'Status', 'SchemaAttributes', 'AliasAttributes',
    'UsernameAttributes', 'UsernameConfiguration', 'EstimatedNumberOfUsers', 'Domain', 'CustomDomain',
    'SmsConfigurationFailure', 'EmailConfigurationFailure'
)

function Get-UserPool {
    $json = aws cognito-idp describe-user-pool --region $Region --user-pool-id $UserPoolId --output json
    if ($LASTEXITCODE -ne 0) { throw "describe-user-pool failed (exit $LASTEXITCODE)." }
    $json -join "`n"
}

function Test-Empty($value) {
    if ($null -eq $value) { return $true }
    if ($value -is [string]) { return $value.Length -eq 0 }
    if ($value -is [array]) { return $value.Count -eq 0 }
    if ($value -is [System.Management.Automation.PSCustomObject]) { return @($value.PSObject.Properties).Count -eq 0 }
    return $false
}

# 1. Back up the current pool so a bad update can be reverted by hand.
$describeJson = Get-UserPool
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupPath = Join-Path $env:TEMP "cognito-$UserPoolId-$timestamp.json"
[System.IO.File]::WriteAllText($backupPath, $describeJson)
Write-Host "Backed up current pool to $backupPath"

$pool = ($describeJson | ConvertFrom-Json).UserPool

# 2. Rebuild the update request from every settable field, dropping empty ones.
$request = [ordered]@{ UserPoolId = $UserPoolId }
foreach ($property in $pool.PSObject.Properties) {
    if ($readOnlyFields -notcontains $property.Name -and -not (Test-Empty $property.Value)) {
        $request[$property.Name] = $property.Value
    }
}

# 3. Flip the flag. UnusedAccountValidityDays is deprecated and rejected alongside
#    Policies.PasswordPolicy.TemporaryPasswordValidityDays, so it is dropped.
$adminConfig = [ordered]@{}
if ($pool.AdminCreateUserConfig) {
    foreach ($property in $pool.AdminCreateUserConfig.PSObject.Properties) {
        if ($property.Name -ne 'UnusedAccountValidityDays' -and -not (Test-Empty $property.Value)) {
            $adminConfig[$property.Name] = $property.Value
        }
    }
}
$adminConfig['AllowAdminCreateUserOnly'] = -not $Enable
$request['AdminCreateUserConfig'] = $adminConfig

# 4. Send it. Written without a BOM: the AWS CLI rejects BOM-prefixed JSON.
$requestPath = Join-Path $env:TEMP "cognito-$UserPoolId-$timestamp-update.json"
[System.IO.File]::WriteAllText($requestPath, ($request | ConvertTo-Json -Depth 20))
# --output is explicit: with no default output format configured, the CLI applies the update and then exits 255.
aws cognito-idp update-user-pool --region $Region --output json --cli-input-json "file://$requestPath"
if ($LASTEXITCODE -ne 0) { throw "update-user-pool failed (exit $LASTEXITCODE). Request: $requestPath. Backup: $backupPath" }

# 5. Confirm from the live pool rather than trusting the request.
$after = (Get-UserPool | ConvertFrom-Json).UserPool.AdminCreateUserConfig.AllowAdminCreateUserOnly
$state = if ($after) { 'DISABLED (admin-created users only)' } else { 'ENABLED' }
Write-Host "AllowAdminCreateUserOnly = $after -> self sign-up $state"
