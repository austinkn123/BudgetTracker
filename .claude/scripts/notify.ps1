$ErrorActionPreference = 'SilentlyContinue'
try {
    [System.Media.SystemSounds]::Asterisk.Play()
} catch {
    [Console]::Beep(800, 150)
}
