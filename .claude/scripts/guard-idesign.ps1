$ErrorActionPreference = 'SilentlyContinue'

try {
    $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
} catch {
    exit 0
}

$path = $payload.tool_input.file_path
if (-not $path) { exit 0 }

$content = ''
if ($payload.tool_input.new_string) { $content = $payload.tool_input.new_string }
elseif ($payload.tool_input.content) { $content = $payload.tool_input.content }
if (-not $content) { exit 0 }

$normalized = ($path -replace '/', '\').ToLowerInvariant()

$violation = $null

if ($normalized -match '\\budgettracker\.domain\\engines\\.*\.cs$') {
    if ($content -match 'Interfaces\.Accessors|BudgetTrackerDbContext|Microsoft\.EntityFrameworkCore|\bnew\s+\w*Accessor\s*\(') {
        $violation = "Engine references Accessor/DbContext/EF Core. Engines must be pure business logic — route data access through a Manager + Accessor."
    }
}
elseif ($normalized -match '\\budgettracker\.domain\\accessors\\.*\.cs$') {
    if ($content -match 'Interfaces\.Engines|BudgetTracker\.Domain\.Engines') {
        $violation = "Accessor references an Engine. Accessors must only encapsulate data access — business logic belongs in Engines, orchestration in Managers."
    }
}
elseif ($normalized -match '\\budgettracker\.server\\managers\\.*\.cs$') {
    if ($content -match 'Microsoft\.EntityFrameworkCore|BudgetTrackerDbContext') {
        $violation = "Manager references DbContext/EF Core directly. Managers orchestrate Engines + Accessors — data access must go through an Accessor."
    }
}

if ($violation) {
    $reason = "IDesign violation in $path`: $violation Confirm if this is an intentional exception."
    $output = @{ decision = 'block'; reason = $reason } | ConvertTo-Json -Compress
    Write-Output $output
    exit 0
}

exit 0