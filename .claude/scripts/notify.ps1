# Stop hook: plays a system sound (or beep fallback) when a Claude session ends.

$ErrorActionPreference = 'SilentlyContinue'
try {
    [System.Media.SystemSounds]::Asterisk.Play()
} catch {
    [Console]::Beep(800, 150)
}
