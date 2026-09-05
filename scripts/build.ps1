#Requires -Version 5.1
<#
.SYNOPSIS
    Restores and builds MonWin in Release configuration.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$sln = Join-Path $root "SystemMonitor.sln"

Write-Host "Restoring..." -ForegroundColor Cyan
dotnet restore $sln
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }

Write-Host "Building (Release)..." -ForegroundColor Cyan
dotnet build $sln -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

Write-Host "Build succeeded." -ForegroundColor Green
