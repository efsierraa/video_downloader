<#
.SYNOPSIS
    Compiles "Video Downloader.cs" into "Video Downloader.exe".

.DESCRIPTION
    Uses the C# compiler that ships with Windows (.NET Framework 4.x),
    so no Visual Studio or .NET SDK installation is needed.

.EXAMPLE
    .\Build-App.ps1
#>

$ErrorActionPreference = "Stop"

$Csc = Join-Path "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319" "csc.exe"
if (-not (Test-Path -LiteralPath $Csc)) {
    $Csc = Join-Path "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319" "csc.exe"
}
if (-not (Test-Path -LiteralPath $Csc)) {
    Write-Error "csc.exe (.NET Framework 4.x) not found on this machine."
    exit 1
}

$Source = Join-Path $PSScriptRoot "Video Downloader.cs"
$Output = Join-Path $PSScriptRoot "Video Downloader.exe"

# WebView2 SDK assemblies (needed for the Facebook login window).
$Wv2Files = @(
    "Microsoft.Web.WebView2.Core.dll",
    "Microsoft.Web.WebView2.WinForms.dll",
    "WebView2Loader.dll"
)
$needWv2 = $false
foreach ($f in $Wv2Files) {
    if (-not (Test-Path -LiteralPath (Join-Path $PSScriptRoot $f))) { $needWv2 = $true }
}
if ($needWv2) {
    Write-Host "Downloading WebView2 SDK (one-time)..." -ForegroundColor Yellow
    $Tmp = Join-Path $env:TEMP ("wv2sdk-" + [guid]::NewGuid().ToString("N"))
    New-Item -ItemType Directory -Path $Tmp -Force | Out-Null
    try {
        $zip = Join-Path $Tmp "wv2.zip"
        Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/Microsoft.Web.WebView2" `
            -OutFile $zip -UseBasicParsing
        $x = Join-Path $Tmp "x"
        Expand-Archive -LiteralPath $zip -DestinationPath $x -Force
        Copy-Item -LiteralPath (Join-Path $x "lib\net462\Microsoft.Web.WebView2.Core.dll") -Destination $PSScriptRoot
        Copy-Item -LiteralPath (Join-Path $x "lib\net462\Microsoft.Web.WebView2.WinForms.dll") -Destination $PSScriptRoot
        Copy-Item -LiteralPath (Join-Path $x "runtimes\win-x64\native\WebView2Loader.dll") -Destination $PSScriptRoot
    }
    finally {
        Remove-Item -LiteralPath $Tmp -Recurse -Force -ErrorAction SilentlyContinue
    }
}

$Refs = @(
    "/r:System.dll",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.IO.Compression.dll",
    "/r:System.IO.Compression.FileSystem.dll",
    "/r:$(Join-Path $PSScriptRoot 'Microsoft.Web.WebView2.Core.dll')",
    "/r:$(Join-Path $PSScriptRoot 'Microsoft.Web.WebView2.WinForms.dll')"
)

& $Csc /nologo /target:winexe /utf8output "/win32icon:$PSScriptRoot\icon.ico" "/out:$Output" @Refs "$Source"

if ($LASTEXITCODE -eq 0) {
    Write-Host "Built: $Output" -ForegroundColor Green
} else {
    Write-Host "Build failed (exit code $LASTEXITCODE)." -ForegroundColor Red
    exit $LASTEXITCODE
}
