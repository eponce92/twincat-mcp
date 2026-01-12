# Test TcAutomation.exe directly
# Usage: .\scripts\test-cli.ps1 -Solution "C:\path\to\solution.sln"

param(
    [Parameter(Mandatory=$true)]
    [string]$Solution,
    
    [Parameter()]
    [ValidateSet("build", "info")]
    [string]$Command = "build"
)

$ErrorActionPreference = "Stop"

# Find the executable
$exePaths = @(
    "$PSScriptRoot\..\TcAutomation\bin\Release\net8.0-windows\win-x64\TcAutomation.exe",
    "$PSScriptRoot\..\TcAutomation\bin\Release\net8.0-windows\TcAutomation.exe",
    "$PSScriptRoot\..\TcAutomation\bin\Debug\net8.0-windows\TcAutomation.exe",
    "$PSScriptRoot\..\TcAutomation\publish\TcAutomation.exe"
)

$exe = $null
foreach ($path in $exePaths) {
    if (Test-Path $path) {
        $exe = Resolve-Path $path
        break
    }
}

if (-not $exe) {
    Write-Host "❌ TcAutomation.exe not found. Run build.ps1 first." -ForegroundColor Red
    exit 1
}

Write-Host "Using: $exe" -ForegroundColor Cyan
Write-Host "Command: $Command" -ForegroundColor Cyan
Write-Host "Solution: $Solution" -ForegroundColor Cyan
Write-Host ""

& $exe $Command --solution $Solution
