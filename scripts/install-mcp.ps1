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

# Create MCP configuration
$mcpConfig = @{
    servers = @{
        "twincat-automation" = @{
            type = "stdio"
            command = "python"
            args = @($serverPath)
        }
    }
} | ConvertTo-Json -Depth 4

if ($Workspace) {
    # Install to workspace .vscode folder
    $mcpJsonPath = Join-Path (Get-Location) ".vscode\mcp.json"
    $vscodeDir = Join-Path (Get-Location) ".vscode"
    
    if (-not (Test-Path $vscodeDir)) {
        New-Item -ItemType Directory -Path $vscodeDir -Force | Out-Null
    }
    
    Set-Content -Path $mcpJsonPath -Value $mcpConfig
    Write-Host ""
    Write-Host "✅ Installed to workspace: $mcpJsonPath" -ForegroundColor Green
} else {
    # Install globally to user settings
    
    # Detect VS Code variant
    $vsCodeInsiders = Test-Path "$env:APPDATA\Code - Insiders"
    $vsCodeStable = Test-Path "$env:APPDATA\Code"
    
    $targets = @()
    
    if ($Insiders -or (-not $vsCodeStable -and $vsCodeInsiders)) {
        $targets += @{
            Name = "VS Code Insiders"
            Path = "$env:APPDATA\Code - Insiders\User\globalStorage\github.copilot-chat\mcp.json"
        }
    }
    
    if (-not $Insiders -and $vsCodeStable) {
        $targets += @{
            Name = "VS Code"
            Path = "$env:APPDATA\Code\User\globalStorage\github.copilot-chat\mcp.json"
        }
    }
    
    if ($vsCodeInsiders -and $vsCodeStable -and -not $Insiders) {
        # Install to both if both are present
        $targets += @{
            Name = "VS Code Insiders"
            Path = "$env:APPDATA\Code - Insiders\User\globalStorage\github.copilot-chat\mcp.json"
        }
    }
    
    if ($targets.Count -eq 0) {
        Write-Host "❌ VS Code not found. Install VS Code first." -ForegroundColor Red
        exit 1
    }
    
    foreach ($target in $targets) {
        $dir = Split-Path $target.Path -Parent
        if (-not (Test-Path $dir)) {
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
        }
        
        # Check if file exists and has other servers
        if (Test-Path $target.Path) {
            try {
                $existing = Get-Content $target.Path -Raw | ConvertFrom-Json
                if ($existing.servers) {
                    # Merge with existing config
                    $existing.servers | Add-Member -NotePropertyName "twincat-automation" -NotePropertyValue (@{
                        type = "stdio"
                        command = "python"
                        args = @($serverPath)
                    }) -Force
                    $mcpConfig = $existing | ConvertTo-Json -Depth 4
                }
            } catch {
                # File exists but invalid JSON, overwrite
            }
        }
        
        Set-Content -Path $target.Path -Value $mcpConfig
        Write-Host "✅ Installed to $($target.Name): $($target.Path)" -ForegroundColor Green
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
