# PostToolUse hook: prints a /migrate reminder when a Model or EF configuration file is edited, since those changes require a migration.

$ErrorActionPreference = 'SilentlyContinue'

try {
    $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
} catch {
    exit 0
}

$path = $payload.tool_input.file_path
if (-not $path) { exit 0 }

$normalized = $path -replace '/', '\'
if ($normalized -match '\\BudgetTracker\.Domain\\(Models|Data\\Configurations)\\.*\.cs$') {
    Write-Host ""
    Write-Host "EF entity/config changed:" -ForegroundColor Yellow
    Write-Host "  $path" -ForegroundColor DarkGray
    Write-Host "Don't forget the migration:" -ForegroundColor Yellow
    Write-Host "  /migrate <Name>" -ForegroundColor White
    Write-Host "  (or: dotnet ef migrations add <Name> --project BudgetTracker.Domain --startup-project BudgetTracker.Server)" -ForegroundColor DarkGray
}
