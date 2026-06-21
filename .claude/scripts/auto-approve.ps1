# PreToolUse hook: auto-approves safe read-only and MCP tool calls; blocks destructive patterns so they still prompt.

$ErrorActionPreference = 'SilentlyContinue'

try {
    $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
    $toolName = [string]$payload.tool_name
    if (-not $toolName) { exit 0 }

    $commandText = ''
    if ($toolName -in @('Bash', 'PowerShell')) {
        $commandText = [string]$payload.tool_input.command
    }

    $destructivePatterns = @(
        'git\s+(push\s+(-f|--force|--force-with-lease)|reset\s+--hard|branch\s+-D|clean\s+-f|checkout\s+--|restore\s+|stash\s+(drop|clear))',
        'rm\s+-r(f|\s)',
        'Remove-Item\s+(-Recurse|-Force)',
        'Stop-Process',
        'dotnet\s+ef\s+(database\s+drop|migrations\s+remove)',
        'dotnet\s+nuget\s+delete',
        'npm\s+publish',
        'DROP\s+(TABLE|DATABASE|SCHEMA|INDEX|VIEW|PROCEDURE)',
        'TRUNCATE\s+TABLE',
        'DELETE\s+FROM'
    )

    if ($commandText) {
        foreach ($pattern in $destructivePatterns) {
            if ($commandText -match $pattern) { exit 0 }
        }
    }

    $approve = $false

    if ($toolName -like 'mcp__*') {
        $approve = $true
    }
    elseif ($toolName -in @('Bash', 'PowerShell')) {
        $approve = $true
    }
    elseif ($toolName -in @('WebFetch', 'WebSearch', 'Glob', 'Grep', 'Read', 'BashOutput', 'TodoWrite', 'ListMcpResourcesTool', 'ReadMcpResourceTool')) {
        $approve = $true
    }

    if (-not $approve) { exit 0 }

    $logDir = Join-Path (Get-Location) '.claude\logs'
    if (-not (Test-Path $logDir)) {
        New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    }
    $logPath = Join-Path $logDir 'auto-approvals.log'

    $inputJson = ($payload.tool_input | ConvertTo-Json -Compress -Depth 4)
    if ($inputJson.Length -gt 200) {
        $inputJson = $inputJson.Substring(0, 200) + '...'
    }
    $timestamp = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ssK')
    Add-Content -Path $logPath -Value "[$timestamp] $toolName :: $inputJson" -Encoding utf8

    $decision = @{ decision = 'approve'; reason = 'auto-approve hook' } | ConvertTo-Json -Compress
    Write-Output $decision
    exit 0
} catch {
    exit 0
}
