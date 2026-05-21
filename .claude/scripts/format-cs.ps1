$ErrorActionPreference = 'SilentlyContinue'

try {
    $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
} catch {
    exit 0
}

$path = $payload.tool_input.file_path
if (-not $path) { exit 0 }
if ($path -notmatch '\.cs$') { exit 0 }
if (-not (Test-Path $path)) { exit 0 }

$sln = Get-ChildItem -Filter *.sln -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $sln) { exit 0 }

Start-Job -ScriptBlock {
    param($slnPath, $filePath)
    & dotnet format whitespace $slnPath --include $filePath --no-restore --verbosity quiet 2>$null
} -ArgumentList $sln.FullName, $path | Out-Null
