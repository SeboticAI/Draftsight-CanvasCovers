#Requires -RunAsAdministrator

param(
    [string]$DeployDir = "C:\BesiaCAD\CanvasCovers",
    [switch]$StopDraftSight
)

$ErrorActionPreference = "Continue"

$regAsm = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"
$deployedDll = Join-Path $DeployDir "CanvasCovers.dll"
$sourceDll = Join-Path (Split-Path -Parent $PSScriptRoot) "CanvasCovers\bin\Release\net48\CanvasCovers.dll"
$configPaths = @(
    "C:\ProgramData\Dassault Systemes\DraftSight\addinConfigs\CanvasCovers.xml",
    "C:\ProgramData\DassaultSystemes\DraftSight\addinConfigs\CanvasCovers.xml"
)

if ($StopDraftSight) {
    Stop-Process -Name DraftSight -Force -ErrorAction SilentlyContinue
}

if (Test-Path $deployedDll) {
    & $regAsm $deployedDll /unregister
}

if (Test-Path $sourceDll) {
    & $regAsm $sourceDll /unregister
}

foreach ($configPath in $configPaths) {
    if (Test-Path $configPath) {
        Rename-Item -Force $configPath "$configPath.disabled"
        Write-Host "Disabled $configPath"
    }
}

Write-Host "CanvasCovers registration rollback complete. Confirm DraftSight opens before trying another load."
