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
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$YtDlp = Join-Path $PSScriptRoot "yt-dlp.exe"
$DenoExe = Join-Path $PSScriptRoot "deno.exe"
$FfmpegDir = $PSScriptRoot

$YtDlpArgs = @()

function Install-MissingTools {
    <#
    .SYNOPSIS
        Downloads yt-dlp.exe, deno.exe, ffmpeg.exe and ffprobe.exe into the
        script folder if any of them are missing.
    #>
    $needYtDlp  = -not (Test-Path -LiteralPath $YtDlp)
    $needDeno   = -not (Test-Path -LiteralPath $DenoExe)
    $needFfmpeg = -not (Test-Path -LiteralPath (Join-Path $FfmpegDir "ffmpeg.exe")) -or
                  -not (Test-Path -LiteralPath (Join-Path $FfmpegDir "ffprobe.exe"))

    if (-not ($needYtDlp -or $needDeno -or $needFfmpeg)) { return }

    Write-Host "Some required tools are missing. Downloading them now (one-time setup)..." -ForegroundColor Yellow

    $Tmp = Join-Path $env:TEMP ("getvideo-setup-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $Tmp -Force | Out-Null

    try {
        if ($needYtDlp) {
            Write-Host "  -> yt-dlp.exe" -ForegroundColor DarkGray
            Invoke-WebRequest -Uri "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe" `
                -OutFile $YtDlp -UseBasicParsing
        }

        if ($needDeno) {
            Write-Host "  -> deno.exe" -ForegroundColor DarkGray
            $zip = Join-Path $Tmp "deno.zip"
            Invoke-WebRequest -Uri "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip" `
                -OutFile $zip -UseBasicParsing
            Expand-Archive -LiteralPath $zip -DestinationPath (Join-Path $Tmp "deno") -Force
            Copy-Item -LiteralPath (Join-Path $Tmp "deno\deno.exe") -Destination $DenoExe
        }

        if ($needFfmpeg) {
            Write-Host "  -> ffmpeg.exe + ffprobe.exe (large download, please wait)" -ForegroundColor DarkGray
            $zip = Join-Path $Tmp "ffmpeg.zip"
            Invoke-WebRequest -Uri "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip" `
                -OutFile $zip -UseBasicParsing
            $extractDir = Join-Path $Tmp "ffmpeg"
            Expand-Archive -LiteralPath $zip -DestinationPath $extractDir -Force
            $ffmpegExe = Get-ChildItem -Path $extractDir -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
            Copy-Item -LiteralPath $ffmpegExe.FullName -Destination (Join-Path $FfmpegDir "ffmpeg.exe")
            Copy-Item -LiteralPath (Join-Path $ffmpegExe.DirectoryName "ffprobe.exe") -Destination (Join-Path $FfmpegDir "ffprobe.exe")
        }

        Write-Host "Setup complete." -ForegroundColor Green
    }
    finally {
        Remove-Item -LiteralPath $Tmp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

try {
    Install-MissingTools
} catch {
    Write-Host "`nCould not download the required tools. Check your internet connection and try again." -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

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
