# ImageCrystal modifications by xenon, 2026-10-18; see NOTICE.ImageCrystal.txt.
param([string]$ImagePath, [switch]$CollectAfterLoad)
$ErrorActionPreference = 'Stop'
$appPath = Join-Path $PSScriptRoot 'ImageCrystal.exe'
if (!(Test-Path -LiteralPath $appPath)) { throw 'Run this script from the portable app folder.' }
$appArguments = @()
if ($ImagePath) {
    $appArguments += (Resolve-Path -LiteralPath $ImagePath).Path
}
$previousLog = $env:IMAGECRYSTAL_MEMORY_LOG
$previousGc = $env:IMAGECRYSTAL_MEMORY_GC
try {
    $env:IMAGECRYSTAL_MEMORY_LOG = Join-Path $PSScriptRoot 'memory-logs'
    $env:IMAGECRYSTAL_MEMORY_GC = if ($CollectAfterLoad) { '1' } else { '0' }
    & $appPath @appArguments
    Write-Host "Diagnostic logs: $(Join-Path $PSScriptRoot 'memory-logs')"
} finally {
    $env:IMAGECRYSTAL_MEMORY_LOG = $previousLog
    $env:IMAGECRYSTAL_MEMORY_GC = $previousGc
}
