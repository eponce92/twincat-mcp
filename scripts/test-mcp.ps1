# Test MCP Server with Inspector
# Usage: .\scripts\test-mcp.ps1

$ErrorActionPreference = "Stop"

Write-Host "Starting MCP Inspector..." -ForegroundColor Cyan
Write-Host "This will open a web UI to test the MCP server tools." -ForegroundColor Yellow
Write-Host ""

Push-Location "$PSScriptRoot\.."

try {
    npx @modelcontextprotocol/inspector -- python mcp-server/server.py
}
finally {
    Pop-Location
}
