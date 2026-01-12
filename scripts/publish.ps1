# Publish TcAutomation.exe for distribution
# Usage: .\scripts\publish.ps1

$ErrorActionPreference = "Stop"

Write-Host "Publishing TcAutomation..." -ForegroundColor Cyan

Push-Location "$PSScriptRoot\..\TcAutomation"

try {
    # Publish as framework-dependent (requires .NET 8 runtime on target)
    dotnet publish -c Release -r win-x64 --self-contained false -o publish
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Publish succeeded!" -ForegroundColor Green
        Write-Host "Executable: TcAutomation\publish\TcAutomation.exe"
        
        # Show the published files
        Write-Host "`nPublished files:" -ForegroundColor Yellow
        Get-ChildItem publish | Format-Table Name, Length -AutoSize
    } else {
        Write-Host "❌ Publish failed with exit code $LASTEXITCODE" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}
