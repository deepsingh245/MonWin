#Requires -Version 5.1
<#
.SYNOPSIS
    Publishes MonWin as a self-contained, single-file, ReadyToRun win-x64 executable.
#>
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\SystemMonitor\SystemMonitor.csproj"
$outDir = Join-Path $root "publish"

$exePath = Join-Path $outDir "SystemMonitor.exe"

# A running copy of the previously published exe locks the file, which makes the
# single-file bundler fail with "The process cannot access the file". Close only that
# copy (not a Debug build or one running from elsewhere), and relaunch it afterwards.
$running = @(Get-Process -Name SystemMonitor -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and ([IO.Path]::GetFullPath($_.Path) -ieq [IO.Path]::GetFullPath($exePath)) })
if ($running.Count -gt 0) {
    Write-Host "Closing running MonWin from $outDir so it can be replaced ..." -ForegroundColor Yellow
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
}

# Windows can keep the image file locked briefly after the process exits (and antivirus
# may scan it), so wait until it can actually be opened exclusively before bundling.
if (Test-Path $exePath) {
    $deadline = (Get-Date).AddSeconds(15)
    while ($true) {
        try {
            [IO.File]::Open($exePath, 'Open', 'ReadWrite', 'None').Close()
            break
        }
        catch {
            if ((Get-Date) -gt $deadline) {
                throw "$exePath is still in use by another process. Close MonWin (tray icon > Exit) and try again."
            }
            Start-Sleep -Milliseconds 250
        }
    }
}

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

if ($running.Count -gt 0) {
    Write-Host "Restarting MonWin with the new build ..." -ForegroundColor Cyan
    Start-Process $exePath
}
