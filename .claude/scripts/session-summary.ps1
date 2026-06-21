# Stop hook: prints a git diff --stat summary of what changed during the session, with a prompt to /review or /run.

$ErrorActionPreference = 'SilentlyContinue'

$changed = git status --short 2>$null
if (-not $changed) { exit 0 }

Write-Host ""
Write-Host "Session changes:" -ForegroundColor Cyan

$stat = git diff --stat HEAD 2>$null
if ($stat) {
    $stat | Select-Object -First 12 | ForEach-Object { Write-Host "  $_" }
} else {
    $changed | Select-Object -First 12 | ForEach-Object { Write-Host "  $_" }
}

Write-Host ""
Write-Host "  Next: /review before committing, or /run to test" -ForegroundColor DarkGray
