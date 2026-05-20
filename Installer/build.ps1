#Requires -Version 5.1
<#
.SYNOPSIS
    Build BesiaCAD-CanvasCovers-Setup-<version>.exe via Inno Setup.

.DESCRIPTION
    1. Refuses to run while DraftSight is open (file locks on the addin DLL).
    2. dotnet build -c Release of the CanvasCovers project.
    3. Compiles Installer\CanvasCovers.iss with ISCC, pointing PayloadDir at
       the freshly-built bin\Release\net48 folder.
    4. Drops the installer EXE in Installer\Output\.

    Does NOT require admin to *build* -- only to *run* the resulting EXE.

.PARAMETER Configuration
    Build configuration. Defaults to Release.

.PARAMETER IsccPath
    Override the ISCC.exe location. Default probes the standard install
    locations and PATH.
#>
param(
    [string]$Configuration = "Release",
    [string]$IsccPath
)

$ErrorActionPreference = "Stop"

$installerDir = $PSScriptRoot
$repoRoot     = Split-Path -Parent $installerDir
$projectPath  = Join-Path $repoRoot "CanvasCovers\CanvasCovers.csproj"
$payloadDir   = Join-Path $repoRoot "CanvasCovers\bin\$Configuration\net48"
$issPath      = Join-Path $installerDir "CanvasCovers.iss"
$outputDir    = Join-Path $installerDir "Output"

if (Get-Process -Name DraftSight -ErrorAction SilentlyContinue) {
    throw "Close DraftSight before building the installer (DLL is locked while it is open)."
}

if (-not $IsccPath) {
    $candidates = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) { if (Test-Path $c) { $IsccPath = $c; break } }
    if (-not $IsccPath) {
        $onPath = Get-Command iscc -ErrorAction SilentlyContinue
        if ($onPath) { $IsccPath = $onPath.Source }
    }
}

if (-not $IsccPath -or -not (Test-Path $IsccPath)) {
    throw "ISCC.exe (Inno Setup 6 compiler) not found. Install from https://jrsoftware.org/isdl.php or pass -IsccPath."
}

Write-Host "Using ISCC: $IsccPath"
Write-Host "Building $projectPath ($Configuration)..."
dotnet build $projectPath -c $Configuration | Write-Host
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed." }

if (-not (Test-Path (Join-Path $payloadDir "CanvasCovers.dll"))) {
    throw "Expected CanvasCovers.dll in $payloadDir -- build did not produce it."
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

Write-Host "Compiling $issPath..."
& $IsccPath "/DPayloadDir=$payloadDir" $issPath
if ($LASTEXITCODE -ne 0) { throw "ISCC compile failed." }

$exe = Get-ChildItem -Path $outputDir -Filter "BesiaCAD-CanvasCovers-Setup-*.exe" |
       Sort-Object LastWriteTime -Descending | Select-Object -First 1

if (-not $exe) {
    throw "ISCC reported success but no installer EXE found in $outputDir."
}

Write-Host ""
Write-Host "Installer built:"
Write-Host "  $($exe.FullName)"
Write-Host ""
Write-Host "Run it as administrator to install / upgrade CanvasCovers."
Write-Host "Uninstall via Settings -> Apps -> 'BesiaCAD Canvas Covers'."
