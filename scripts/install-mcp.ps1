# Install TwinCAT MCP Server to VS Code
# Usage: .\scripts\install-mcp.ps1
#
# This script registers the MCP server globally in VS Code so it works in any workspace.
# Supports both VS Code and VS Code Insiders.

param(
    [switch]$Insiders,      # Force VS Code Insiders installation
    [switch]$Workspace,     # Install to current workspace instead of globally
    [string]$InstallPath    # Override the MCP server path (for portable installs)
)

$ErrorActionPreference = "Stop"

Write-Host "=== Installing TwinCAT MCP Server ===" -ForegroundColor Cyan
Write-Host ""

# Determine the server.py path
if ($InstallPath) {
    $serverPath = $InstallPath
} else {
    $serverPath = (Resolve-Path "$PSScriptRoot\..\mcp-server\server.py").Path
}
$serverPath = $serverPath -replace '\\', '/'

Write-Host "Server path: $serverPath" -ForegroundColor Gray

# Verify server.py exists
if (-not (Test-Path $serverPath)) {
    Write-Host "❌ server.py not found at: $serverPath" -ForegroundColor Red
    Write-Host "   Run setup.ps1 first to build the project" -ForegroundColor Gray
    exit 1
}

# Verify TcAutomation.exe exists
$exePath = Join-Path $PSScriptRoot "..\TcAutomation\bin\Release\TcAutomation.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "❌ TcAutomation.exe not found. Running build..." -ForegroundColor Yellow
    & "$PSScriptRoot\build.ps1"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Build failed. Cannot install MCP server." -ForegroundColor Red
        exit 1
    }
}

# Verify Python is available
try {
    $pythonPath = (Get-Command python -ErrorAction Stop).Source
    Write-Host "✅ Python found: $pythonPath" -ForegroundColor Green
} catch {
    Write-Host "❌ Python not found in PATH" -ForegroundColor Red
    Write-Host "   Install Python 3.10+ and ensure it's in your PATH" -ForegroundColor Gray
    exit 1
}

# Check MCP package is installed
$mcpCheck = pip show mcp 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Installing MCP Python package..." -ForegroundColor Yellow
    pip install mcp
    if ($LASTEXITCODE -ne 0) {
        Write-Host "❌ Failed to install MCP package" -ForegroundColor Red
        exit 1
    }
}
Write-Host "✅ MCP Python package installed" -ForegroundColor Green

# Build MCP server JSON configuration (escape quotes for cmd.exe)
$mcpServerJson = '{\"name\":\"twincat-automation\",\"type\":\"stdio\",\"command\":\"python\",\"args\":[\"' + $serverPath + '\"]}'

if ($Workspace) {
    # Install to workspace .vscode folder
    $mcpJsonPath = Join-Path (Get-Location) ".vscode\mcp.json"
    $vscodeDir = Join-Path (Get-Location) ".vscode"
    
    if (-not (Test-Path $vscodeDir)) {
        New-Item -ItemType Directory -Path $vscodeDir -Force | Out-Null
    }
    
    # For workspace, we still need to create the file manually
    $mcpConfig = @"
{
	"servers": {
		"twincat-automation": {
			"type": "stdio",
			"command": "python",
			"args": ["$serverPath"]
		}
	}
}
"@
    Set-Content -Path $mcpJsonPath -Value $mcpConfig -Encoding UTF8
    Write-Host ""
    Write-Host "✅ Installed to workspace: $mcpJsonPath" -ForegroundColor Green
} else {
    # Install globally using VS Code CLI --add-mcp command
    
    # Detect VS Code variant
    $vsCodeInsiders = Get-Command "code-insiders" -ErrorAction SilentlyContinue
    $vsCodeStable = Get-Command "code" -ErrorAction SilentlyContinue
    
    $installed = $false
    
    if (-not $Insiders -and $vsCodeStable) {
        Write-Host "Installing to VS Code..." -ForegroundColor Gray
        $result = cmd /c "code --add-mcp `"$mcpServerJson`"" 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Installed to VS Code" -ForegroundColor Green
            $installed = $true
        } else {
            Write-Host "⚠️ Failed to install to VS Code: $result" -ForegroundColor Yellow
        }
    }
    
    if ($Insiders -or $vsCodeInsiders) {
        Write-Host "Installing to VS Code Insiders..." -ForegroundColor Gray
        $result = cmd /c "code-insiders --add-mcp `"$mcpServerJson`"" 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Installed to VS Code Insiders" -ForegroundColor Green
            $installed = $true
        } else {
            Write-Host "⚠️ Failed to install to VS Code Insiders: $result" -ForegroundColor Yellow
        }
    }
    
    if (-not $installed) {
        Write-Host "❌ VS Code not found. Install VS Code first." -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "=== Installation Complete! ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Restart VS Code (or press Ctrl+Shift+P -> 'Developer: Reload Window')"
Write-Host "2. Press Ctrl+Shift+P -> 'MCP: List Servers'"
Write-Host "3. Click on 'twincat-automation' to start the server"
Write-Host "4. In Copilot Chat, ask: 'Build my TwinCAT project at C:\path\to\solution.sln'"
Write-Host ""
