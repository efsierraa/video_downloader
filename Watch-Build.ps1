<#
.SYNOPSIS
    Watches "Video Downloader.cs" and automatically recompiles on changes.

.EXAMPLE
    .\Watch-Build.ps1
#>

$sourceFile = Join-Path $PSScriptRoot "Video Downloader.cs"
$buildScript = Join-Path $PSScriptRoot "Build-App.ps1"

if (-not (Test-Path -LiteralPath $sourceFile)) {
    Write-Error "Source file not found: $sourceFile"
    exit 1
}

$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = $PSScriptRoot
$watcher.Filter = "Video Downloader.cs"
$watcher.NotifyFilter = [System.IO.NotifyFilters]::LastWrite
$watcher.EnableRaisingEvents = $true

$action = {
    $path = $Event.SourceEventArgs.FullPath
    $changeType = $Event.SourceEventArgs.ChangeType
    Write-Host "$(Get-Date -Format 'HH:mm:ss') $changeType: $path" -ForegroundColor DarkGray
    Write-Host "Recompiling..." -ForegroundColor Yellow
    powershell -ExecutionPolicy Bypass -File $buildScript
}

Register-ObjectEvent $watcher "Changed" -Action $action | Out-Null

Write-Host "Watching for changes to 'Video Downloader.cs'..." -ForegroundColor Green
Write-Host "Press Ctrl+C to stop." -ForegroundColor Green

try {
    while ($true) { Start-Sleep -Seconds 1 }
}
finally {
    $watcher.EnableRaisingEvents = $false
    $watcher.Dispose()
}
