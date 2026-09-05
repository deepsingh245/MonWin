#Requires -Version 5.1
<#
.SYNOPSIS
    Publishes MonWin as a self-contained, single-file, ReadyToRun win-x64 executable.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\SystemMonitor\SystemMonitor.csproj"
$outDir = Join-Path $root "publish"

Write-Host "Publishing self-contained win-x64 build to $outDir ..." -ForegroundColor Cyan

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishReadyToRun=true `
    -o $outDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "Published to $outDir" -ForegroundColor Green
Get-ChildItem $outDir | Format-Table Name, Length
