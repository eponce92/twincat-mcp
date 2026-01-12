@echo off
REM Quick setup script for TwinCAT MCP Server
REM Double-click this file or run from command prompt

echo ============================================
echo    TwinCAT MCP Server - Quick Setup
echo ============================================
echo.

REM Check if running from correct directory
if not exist "scripts\setup.ps1" (
    echo ERROR: Please run this script from the twincat-mcp folder
    echo Example: cd C:\path\to\twincat-mcp
    echo          setup.bat
    pause
    exit /b 1
)

echo Running PowerShell setup script...
echo.

powershell -ExecutionPolicy Bypass -File "scripts\setup.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Setup failed! Check the errors above.
    pause
    exit /b 1
)

echo.
echo ============================================
echo    Installing MCP Server to VS Code...
echo ============================================
echo.

powershell -ExecutionPolicy Bypass -File "scripts\install-mcp.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo MCP installation failed! Check the errors above.
    pause
    exit /b 1
)

echo.
echo ============================================
echo    Setup Complete!
echo ============================================
echo.
echo Now:
echo 1. Restart VS Code
echo 2. Press Ctrl+Shift+P, type "MCP: List Servers"
echo 3. Click "twincat-automation" to start
echo 4. Ask Copilot: "Build my TwinCAT project at C:\path\to\solution.sln"
echo.
pause
