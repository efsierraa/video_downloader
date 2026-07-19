<#
.SYNOPSIS
    Downloads videos from Facebook and YouTube using yt-dlp.

.DESCRIPTION
    Saves the video with its original title in the best available quality.

.EXAMPLE
    .\Get-Video.ps1 "https://www.facebook.com/watch/?ref=saved&v=1306600044919943"

.EXAMPLE
    .\Get-Video.ps1 "https://www.youtube.com/watch?v=XXXX" -OutputDir "D:\Videos"

.EXAMPLE
    .\Get-Video.ps1 -Update
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false, Position = 0)]
    [string]$Url,

    [Parameter(Mandatory = $false)]
    [string]$OutputDir = ".\downloads",

    [Parameter(Mandatory = $false)]
    [string]$Format = "bv*+ba/b",

    [Parameter(Mandatory = $false)]
    [switch]$Update
)

$ErrorActionPreference = "Stop"
$YtDlp = Join-Path $PSScriptRoot "yt-dlp.exe"
$DenoExe = Join-Path $PSScriptRoot "deno.exe"
$FfmpegDir = $PSScriptRoot

$YtDlpArgs = @()

if (-not (Test-Path -LiteralPath $YtDlp)) {
    Write-Error "yt-dlp.exe not found in $PSScriptRoot"
    exit 1
}

if (Test-Path -LiteralPath $DenoExe) {
    $YtDlpArgs += "--js-runtimes", "deno:$DenoExe"
}

if (Test-Path -LiteralPath (Join-Path $FfmpegDir "ffmpeg.exe")) {
    $YtDlpArgs += "--ffmpeg-location", $FfmpegDir
}

# Self-update mode: .\Get-Video.ps1 -Update
if ($Update) {
    & $YtDlp -U
    exit $LASTEXITCODE
}

if ([string]::IsNullOrWhiteSpace($Url)) {
    $Url = Read-Host "Paste the video URL (Facebook or YouTube)"
}

if (-not (Test-Path -LiteralPath $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$OutTemplate = Join-Path $OutputDir "%(title)s.%(ext)s"

Write-Host "Downloading: $Url" -ForegroundColor Cyan
Write-Host "Saving to:   $OutputDir" -ForegroundColor Cyan

& $YtDlp $YtDlpArgs --format $Format --output $OutTemplate $Url

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nDownload completed successfully." -ForegroundColor Green
} else {
    Write-Host "`nDownload failed (exit code $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}
