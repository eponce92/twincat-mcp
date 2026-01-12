# TwinCAT MCP Server

An MCP (Model Context Protocol) server that enables AI assistants like GitHub Copilot to build and validate TwinCAT projects directly from VS Code.

## Features

- **Build TwinCAT Solutions** - Compile projects and get detailed error/warning reports
- **Project Info** - Get TwinCAT version, PLC list, and configuration details
- **Works Globally** - Once installed, works in any VS Code workspace
- **Coming Soon**: Deploy to PLCs, activate configurations, manage boot projects

## Prerequisites

| Software | Version | Download |
|----------|---------|----------|
| Windows | 10/11 | Required for COM interop |
| Visual Studio 2022 | Community+ | [Download](https://visualstudio.microsoft.com/) - Install with **".NET desktop development"** workload |
| .NET Framework 4.7.2 | Developer Pack | [Download](https://dotnet.microsoft.com/download/dotnet-framework/net472) |
| TwinCAT XAE | 3.1.4024+ | [Beckhoff](https://www.beckhoff.com/en-en/products/automation/twincat/) |
| Python | 3.10+ | [Download](https://www.python.org/downloads/) - Check "Add to PATH" during install |
| VS Code | Latest | [Download](https://code.visualstudio.com/) - With GitHub Copilot extension |

> **Important**: This project uses **MSBuild.exe** (from Visual Studio), not `dotnet build`.  
> TwinCAT COM references require the full Visual Studio MSBuild.

---

## Installation

### Option 1: Quick Setup (Recommended)

Double-click `setup.bat` or run from PowerShell:

```powershell
cd C:\path\to\twincat-mcp
.\setup.bat
```

This will:
1. ✅ Check all prerequisites
2. ✅ Build TcAutomation.exe with MSBuild
3. ✅ Install Python dependencies
4. ✅ Register MCP server in VS Code globally

### Option 2: Step-by-Step Manual Setup

#### Step 1: Build the CLI Tool

```powershell
cd C:\path\to\twincat-mcp
.\scripts\setup.ps1
```

#### Step 2: Install MCP Server to VS Code

```powershell
# Install globally (works in all workspaces)
.\scripts\install-mcp.ps1

# Or install to current workspace only
.\scripts\install-mcp.ps1 -Workspace

# For VS Code Insiders specifically
.\scripts\install-mcp.ps1 -Insiders
```

#### Step 3: Start the MCP Server

1. **Restart VS Code** (or `Ctrl+Shift+P` → "Developer: Reload Window")
2. Press `Ctrl+Shift+P` → **"MCP: List Servers"**
3. Click **"twincat-automation"** to start the server
4. If prompted, click **"Trust"** to allow the server to run

### Option 3: Manual MSBuild (Advanced)

If the scripts don't work, build manually:

```powershell
# Find MSBuild
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$msbuild = Join-Path $vsPath "MSBuild\Current\Bin\MSBuild.exe"

# Build
& $msbuild "TcAutomation\TcAutomation.csproj" /p:Configuration=Release /p:Platform=x64 /restore
```

Then manually add to VS Code's MCP config (`Ctrl+Shift+P` → "MCP: Open User Configuration"):

```json
{
  "servers": {
    "twincat-automation": {
      "type": "stdio",
      "command": "python",
      "args": ["C:/path/to/twincat-mcp/mcp-server/server.py"]
    }
  }
}
```

---

## Usage

Once installed and started, you can use the TwinCAT tools in **any VS Code workspace**.

### In Copilot Chat

Ask natural language questions:

- *"Build my TwinCAT project at C:\Projects\MyMachine\Solution.sln"*
- *"Check for syntax errors in my TwinCAT solution"*
- *"What TwinCAT version is my project using?"*
- *"List the PLCs in my TwinCAT project"*

### Example Output

**Build with errors:**
```
❌ Build failed

🔴 Errors:
  - C:\Projects\MyProject\PLC\POUs\MAIN.TcPOU (Decl):4: C0077: Unknown type: 'DINT2'
  - C:\Projects\MyProject\PLC\POUs\FB_Motor.TcPOU (Impl):15: C0035: Program name expected
```

**Project info:**
```
📋 TwinCAT Project Info
Solution: C:\Projects\MyProject.sln
TwinCAT Version: 3.1.4026.17
Visual Studio Version: 17.0
Target Platform: TwinCAT RT (x64)

PLC Projects:
  - PLC_Main (AMS Port: 851)
  - PLC_Safety (AMS Port: 852)
```

---

## Available MCP Tools

| Tool | Description |
|------|-------------|
| `twincat_build` | Build a TwinCAT solution and return compile errors/warnings with file paths and line numbers |
| `twincat_get_info` | Get project info: TwinCAT version, Visual Studio version, target platform, PLC list |

---

---

## Troubleshooting

### MCP Server Not Appearing in VS Code

1. Run `Ctrl+Shift+P` → **"MCP: List Servers"**
2. If "twincat-automation" appears but shows "Stopped", click it to start
3. If prompted, click **"Trust"** to allow execution
4. If not listed, run `.\scripts\install-mcp.ps1` again

### MCP Server Won't Start

Check the server works manually:
```powershell
python mcp-server/server.py
```
- If it hangs waiting for input, that's correct (stdio mode)
- Press `Ctrl+C` to exit
- If you see errors, check Python dependencies: `pip install mcp`

### Build Error: "MSB4803: ResolveComReference not supported"
You're using `dotnet build` instead of MSBuild.exe. Use the build script:
```powershell
.\scripts\build.ps1
```

### Build Error: "MSB3644: Reference assemblies not found"
Install the .NET Framework 4.7.2 Developer Pack:
https://dotnet.microsoft.com/download/dotnet-framework/net472

### "TwinCAT/Visual Studio not found"
Make sure TwinCAT XAE is installed and try specifying the version:
```
--tcVersion "3.1.4026.17"
```

### "COM object error"
Run VS Code as Administrator, or ensure TwinCAT is properly registered.

---

## Moving to Another PC

When you clone or copy this folder to another computer:

1. Install all prerequisites (see table above)
2. Run `setup.bat` or:
   ```powershell
   .\scripts\setup.ps1      # Build the CLI tool
   .\scripts\install-mcp.ps1 # Register with VS Code
   ```
3. Restart VS Code and start the MCP server

The `install-mcp.ps1` script automatically detects the folder location and updates VS Code's config.

---

## Project Structure

```
twincat-mcp/
├── setup.bat               # One-click setup (double-click to run)
├── README.md               # This file
├── PLAN.md                 # Architecture documentation
├── PROGRESS.md             # Development progress
├── TcAutomation/           # .NET CLI tool (COM automation)
│   ├── TcAutomation.csproj # Classic format, .NET Framework 4.7.2
│   ├── Program.cs          # CLI entry point
│   ├── Core/               # COM wrappers, VS instance management
│   ├── Commands/           # Build, Info command implementations
│   └── Models/             # JSON output models
├── mcp-server/             # Python MCP server
│   ├── server.py           # MCP protocol implementation
│   └── requirements.txt    # Python dependencies
├── scripts/                # Helper scripts
│   ├── setup.ps1           # Check prerequisites + build
│   ├── build.ps1           # Build with MSBuild
│   ├── install-mcp.ps1     # Register MCP server in VS Code
│   ├── test-cli.ps1        # Test CLI directly
│   ├── test-mcp.ps1        # Test with MCP Inspector
│   └── test-mcp-automated.ps1  # Automated test suite
└── .vscode/                # VS Code integration
    └── mcp.json            # Workspace MCP config (optional)
```

## Testing

### Automated Tests

Run the full test suite to verify everything works:

```powershell
# Full test including build (~60 seconds)
.\scripts\test-mcp-automated.ps1

# Quick test, skip build (~5 seconds)
.\scripts\test-mcp-automated.ps1 -SkipBuild

# Use a specific TwinCAT solution
.\scripts\test-mcp-automated.ps1 -Solution "C:\path\to\your\solution.sln"
```

### Manual Testing

```powershell
# Test CLI directly
.\TcAutomation\bin\Release\TcAutomation.exe info --solution "C:\path\to\solution.sln"
.\TcAutomation\bin\Release\TcAutomation.exe build --solution "C:\path\to\solution.sln"

# Test with MCP Inspector (opens browser UI)
npx @modelcontextprotocol/inspector python mcp-server/server.py
```

---

## Development

See [PLAN.md](PLAN.md) for architecture details and [PROGRESS.md](PROGRESS.md) for development history.

### Adding New Tools

1. Add command to `TcAutomation/Commands/`
2. Register in `Program.cs`
3. Add tool in `mcp-server/server.py`
4. Update tests in `scripts/test-mcp-automated.ps1`

---

## License

MIT
