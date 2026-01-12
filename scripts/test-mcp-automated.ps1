# Automated MCP Server Tests
# Usage: .\scripts\test-mcp-automated.ps1 [-Solution "path\to\solution.sln"]
#
# This script tests the MCP server's core functionality without requiring
# manual interaction. It validates that:
# 1. The CLI executable exists and is callable
# 2. The Python MCP server can find the CLI
# 3. The run_tc_automation function works for both info and build commands
# 4. JSON output is valid and contains expected fields
#
# Run this after making changes to verify nothing is broken.

param(
    [string]$Solution = "F:\Redwood_Work_Repos\rw-tcunit-runner-eponce-cicd_improvements\Twincat_Project_For_Testing_MCP\MPC_Test_playground\TwinCAT Project1.sln",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$script:TestsPassed = 0
$script:TestsFailed = 0

function Write-TestHeader {
    param([string]$Name)
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host " TEST: $Name" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
}

function Write-TestResult {
    param([bool]$Passed, [string]$Message)
    if ($Passed) {
        Write-Host "  ✅ PASS: $Message" -ForegroundColor Green
        $script:TestsPassed++
    } else {
        Write-Host "  ❌ FAIL: $Message" -ForegroundColor Red
        $script:TestsFailed++
    }
}

function Write-TestInfo {
    param([string]$Message)
    Write-Host "  ℹ️  $Message" -ForegroundColor Gray
}

# ============================================================================
# Setup
# ============================================================================

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$TcAutomationExe = Join-Path $ProjectRoot "TcAutomation\bin\Release\TcAutomation.exe"
$McpServerDir = Join-Path $ProjectRoot "mcp-server"
$McpServerPy = Join-Path $McpServerDir "server.py"

Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "║         TwinCAT MCP Server - Automated Test Suite             ║" -ForegroundColor Magenta
Write-Host "╚═══════════════════════════════════════════════════════════════╝" -ForegroundColor Magenta
Write-Host ""
Write-Host "Project Root: $ProjectRoot" -ForegroundColor Gray
Write-Host "Test Solution: $Solution" -ForegroundColor Gray
Write-Host ""

# ============================================================================
# Test 1: CLI Executable Exists
# ============================================================================

Write-TestHeader "CLI Executable Exists"

$cliExists = Test-Path $TcAutomationExe
Write-TestResult $cliExists "TcAutomation.exe exists at expected path"

if (-not $cliExists) {
    Write-Host ""
    Write-Host "Build the CLI first: .\scripts\build.ps1" -ForegroundColor Yellow
    exit 1
}

# ============================================================================
# Test 2: CLI --help works
# ============================================================================

Write-TestHeader "CLI --help Command"

$helpOutput = (& $TcAutomationExe --help 2>&1) -join "`n"
$helpWorks = $helpOutput -match "TwinCAT Automation CLI"
Write-TestResult $helpWorks "CLI responds to --help"
Write-TestInfo "Output contains 'TwinCAT Automation CLI'"

# ============================================================================
# Test 3: Test Solution Exists
# ============================================================================

Write-TestHeader "Test Solution Exists"

$solutionExists = Test-Path $Solution
Write-TestResult $solutionExists "Test solution file exists"

if (-not $solutionExists) {
    Write-Host ""
    Write-Host "Test solution not found: $Solution" -ForegroundColor Yellow
    Write-Host "Specify a different solution with: -Solution 'path\to\solution.sln'" -ForegroundColor Yellow
    exit 1
}

# ============================================================================
# Test 4: CLI Info Command
# ============================================================================

Write-TestHeader "CLI Info Command"

$infoOutput = & $TcAutomationExe info --solution $Solution 2>&1 | Out-String
try {
    $infoJson = $infoOutput | ConvertFrom-Json
    $infoWorks = $true
} catch {
    $infoWorks = $false
    $infoJson = $null
}

Write-TestResult $infoWorks "CLI info returns valid JSON"

if ($infoJson) {
    $hasTcVersion = -not [string]::IsNullOrEmpty($infoJson.tcVersion)
    Write-TestResult $hasTcVersion "Response contains tcVersion: $($infoJson.tcVersion)"
    
    $hasPlcProjects = $null -ne $infoJson.plcProjects
    Write-TestResult $hasPlcProjects "Response contains plcProjects array"
    
    if ($infoJson.plcProjects) {
        Write-TestInfo "Found PLCs: $($infoJson.plcProjects.name -join ', ')"
    }
}

# ============================================================================
# Test 5: CLI Build Command (if not skipped)
# ============================================================================

if (-not $SkipBuild) {
    Write-TestHeader "CLI Build Command"
    Write-Host "  ⏳ Building solution (this may take 30-60 seconds)..." -ForegroundColor Yellow
    
    $buildOutput = & $TcAutomationExe build --solution $Solution 2>&1 | Out-String
    try {
        $buildJson = $buildOutput | ConvertFrom-Json
        $buildWorks = $true
    } catch {
        $buildWorks = $false
        $buildJson = $null
    }
    
    Write-TestResult $buildWorks "CLI build returns valid JSON"
    
    if ($buildJson) {
        $hasSuccess = $null -ne $buildJson.success
        Write-TestResult $hasSuccess "Response contains success field: $($buildJson.success)"
        
        $hasErrors = $null -ne $buildJson.errors
        Write-TestResult $hasErrors "Response contains errors array"
        
        $hasWarnings = $null -ne $buildJson.warnings
        Write-TestResult $hasWarnings "Response contains warnings array"
        
        Write-TestInfo "Build result: $($buildJson.summary)"
        Write-TestInfo "Errors: $($buildJson.errorCount), Warnings: $($buildJson.warningCount)"
        
        if ($buildJson.errors -and $buildJson.errors.Count -gt 0) {
            Write-TestInfo "First error: $($buildJson.errors[0].description)"
        }
    }
} else {
    Write-Host ""
    Write-Host "Skipping build test (-SkipBuild specified)" -ForegroundColor Yellow
}

# ============================================================================
# Test 6: Python MCP Server - find_tc_automation_exe
# ============================================================================

Write-TestHeader "MCP Server - find_tc_automation_exe()"

Push-Location $McpServerDir
$pythonTest = (python -c "import sys; sys.path.insert(0,'.'); from server import find_tc_automation_exe; print(find_tc_automation_exe())" 2>&1) -join "`n"
Pop-Location

$findExeWorks = $pythonTest -match "TcAutomation.exe"
Write-TestResult $findExeWorks "MCP server can find CLI executable"
Write-TestInfo "Found: $pythonTest"

# ============================================================================
# Test 7: Python MCP Server - run_tc_automation (info)
# ============================================================================

Write-TestHeader "MCP Server - run_tc_automation('info')"

Push-Location $McpServerDir
$pythonInfoTest = python -c @"
import sys
import json
sys.path.insert(0,'.')
from server import run_tc_automation
r = run_tc_automation('info', ['--solution', r'$Solution'])
print(json.dumps({'tcVersion': r.get('tcVersion'), 'plcCount': len(r.get('plcProjects', []))}))
"@ 2>&1
Pop-Location

try {
    $pythonInfoJson = $pythonInfoTest | ConvertFrom-Json
    $pythonInfoWorks = -not [string]::IsNullOrEmpty($pythonInfoJson.tcVersion)
} catch {
    $pythonInfoWorks = $false
}

Write-TestResult $pythonInfoWorks "run_tc_automation('info') returns valid data"
if ($pythonInfoWorks) {
    Write-TestInfo "TC Version: $($pythonInfoJson.tcVersion), PLC Count: $($pythonInfoJson.plcCount)"
}

# ============================================================================
# Test 8: Python MCP Server - run_tc_automation (build) - if not skipped
# ============================================================================

if (-not $SkipBuild) {
    Write-TestHeader "MCP Server - run_tc_automation('build')"
    Write-Host "  ⏳ Running build through MCP server..." -ForegroundColor Yellow
    
    Push-Location $McpServerDir
    $pythonBuildTest = python -c @"
import sys
import json
sys.path.insert(0,'.')
from server import run_tc_automation
r = run_tc_automation('build', ['--solution', r'$Solution', '--clean'])
print(json.dumps({'success': r.get('success'), 'errorCount': r.get('errorCount'), 'warningCount': r.get('warningCount')}))
"@ 2>&1
    Pop-Location
    
    try {
        $pythonBuildJson = $pythonBuildTest | ConvertFrom-Json
        $pythonBuildWorks = $null -ne $pythonBuildJson.success
    } catch {
        $pythonBuildWorks = $false
    }
    
    Write-TestResult $pythonBuildWorks "run_tc_automation('build') returns valid data"
    if ($pythonBuildWorks) {
        Write-TestInfo "Success: $($pythonBuildJson.success), Errors: $($pythonBuildJson.errorCount), Warnings: $($pythonBuildJson.warningCount)"
    }
}

# ============================================================================
# Summary
# ============================================================================

Write-Host ""
Write-Host "╔═══════════════════════════════════════════════════════════════╗" -ForegroundColor Magenta
Write-Host "║                        TEST SUMMARY                            ║" -ForegroundColor Magenta
Write-Host "╚═══════════════════════════════════════════════════════════════╝" -ForegroundColor Magenta
Write-Host ""

$totalTests = $script:TestsPassed + $script:TestsFailed
$allPassed = $script:TestsFailed -eq 0

if ($allPassed) {
    Write-Host "  ✅ All $totalTests tests passed!" -ForegroundColor Green
} else {
    Write-Host "  ❌ $($script:TestsFailed) of $totalTests tests failed" -ForegroundColor Red
}

Write-Host ""
Write-Host "  Passed: $($script:TestsPassed)" -ForegroundColor Green
Write-Host "  Failed: $($script:TestsFailed)" -ForegroundColor $(if ($script:TestsFailed -gt 0) { "Red" } else { "Gray" })
Write-Host ""

exit $(if ($allPassed) { 0 } else { 1 })
