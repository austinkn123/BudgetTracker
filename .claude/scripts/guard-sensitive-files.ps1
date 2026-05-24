$ErrorActionPreference = 'SilentlyContinue'

try {
    $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
} catch {
    exit 0
}

$path = $payload.tool_input.file_path
if (-not $path) { exit 0 }

$normalized = ($path -replace '/', '\').ToLowerInvariant()

$patterns = @(
    '\\\.env(\.|$)',
    '\\appsettings\.production.*\.json$',
    '\.(pfx|key|pem|p12)$',
    '\\\.git\\',
    '\\\.mcp\.json$',
    '\\\.claude\\settings\.json$',
    '\\\.claude\\settings\.local\.json$',
    '\\\.claude\\scripts\\',
    'secrets'
)

foreach ($pattern in $patterns) {
    if ($normalized -match $pattern) {
        $reason = "Sensitive file: $path. Confirm intent before editing (override by re-stating the request with explicit acknowledgement)."
        $output = @{ decision = 'block'; reason = $reason } | ConvertTo-Json -Compress
        Write-Output $output
        exit 0
    }
}

exit 0