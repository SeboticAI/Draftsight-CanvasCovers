#Requires -RunAsAdministrator

param(
    [string]$Configuration = "Release",
    [string]$DeployDir = "C:\BesiaCAD\CanvasCovers",
    [switch]$UseNoSpaceProgramDataPath
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "CanvasCovers\CanvasCovers.csproj"
$buildDir = Join-Path $repoRoot "CanvasCovers\bin\$Configuration\net48"
$addinDll = Join-Path $buildDir "CanvasCovers.dll"
$regAsm = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
$programDataRoot = if ($UseNoSpaceProgramDataPath) {
    "C:\ProgramData\DassaultSystemes\DraftSight\addinConfigs"
} else {
    "C:\ProgramData\Dassault Systemes\DraftSight\addinConfigs"
}

if (Get-Process -Name DraftSight -ErrorAction SilentlyContinue) {
    throw "Close DraftSight before deploying CanvasCovers."
}

Write-Host "Rollback command if DraftSight has trouble loading:"
Write-Host "`"$regAsm`" `"$DeployDir\CanvasCovers.dll`" /unregister"
Write-Host ""

dotnet build $projectPath -c $Configuration

if (-not (Test-Path $addinDll)) {
    throw "Build did not produce $addinDll"
}

New-Item -ItemType Directory -Force -Path $DeployDir | Out-Null
New-Item -ItemType Directory -Force -Path $programDataRoot | Out-Null

Copy-Item -Force (Join-Path $buildDir "CanvasCovers.dll") $DeployDir
Copy-Item -Force (Join-Path $buildDir "CanvasCovers.pdb") $DeployDir
Copy-Item -Force (Join-Path $buildDir "CanvasCovers.xml") $DeployDir
Copy-Item -Force (Join-Path $buildDir "DraftSight.Interop.dsAddin.dll") $DeployDir
Copy-Item -Force (Join-Path $buildDir "DraftSight.Interop.dsAutomation.dll") $DeployDir

$resourcesSource = Join-Path $buildDir "Resources"
if (Test-Path $resourcesSource) {
    Copy-Item -Recurse -Force $resourcesSource $DeployDir
}

Copy-Item -Force (Join-Path $DeployDir "CanvasCovers.xml") (Join-Path $programDataRoot "CanvasCovers.xml")

& $regAsm (Join-Path $DeployDir "CanvasCovers.dll") /codebase

Write-Host ""
Write-Host "CanvasCovers deployed with startup=0 XML."
Write-Host "Next: open DraftSight, use the Add-Ins manager to activate CanvasCovers manually, and do not enable Start Up yet."
