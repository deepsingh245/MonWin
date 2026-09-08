#Requires -Version 5.1
<#
.SYNOPSIS
    Zips the published output into a portable distributable
    (dist\MonWin-<version>-win-x64.zip). Run publish.ps1 first.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "publish"
$distDir = Join-Path $root "dist"

if (-not (Test-Path $publishDir)) {
    throw "publish/ not found - run scripts/publish.ps1 first."
}

New-Item -ItemType Directory -Force -Path $distDir | Out-Null

$version = (Get-Item (Join-Path $publishDir "SystemMonitor.exe")).VersionInfo.ProductVersion
if ([string]::IsNullOrWhiteSpace($version)) { $version = "1.0.0" }
# .NET's deterministic build appends "+<git commit sha>" as informational-version
# metadata; strip it so the zip name stays a clean "MonWin-1.0.0-win-x64.zip" instead
# of embedding a 40-character hash.
$version = $version.Split('+')[0]

$zipPath = Join-Path $distDir "MonWin-$version-win-x64.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath

Write-Host "Packaged: $zipPath" -ForegroundColor Green
