# TwinCAT MCP Server - Project Plan

## 📋 Project Overview

This project creates an **MCP (Model Context Protocol) server** that enables AI assistants (like GitHub Copilot) to interact with TwinCAT projects through the TwinCAT Automation Interface.

### Primary Goals
1. **Build TwinCAT projects** and retrieve build errors/warnings
2. **Validate syntax** before deployment
3. **Future**: Activate configurations, deploy boot projects, manage PLCs

### Architecture Decision
TwinCAT Automation Interface is COM-based and requires .NET with STA threading. MCP servers run in Python/TypeScript. 

**Solution**: Hybrid architecture with:
- **TcAutomation.exe** - .NET Framework CLI tool that wraps TwinCAT COM interfaces
- **MCP Server (Python)** - Handles MCP protocol and calls the CLI tool

> **IMPORTANT**: The .NET CLI tool uses **classic .csproj format** and **.NET Framework 4.7.2** because:
> - COM references (TCatSysManagerLib, EnvDTE) require MSBuild's `ResolveComReference` task
> - This task is NOT supported by `dotnet build` (only by MSBuild.exe from Visual Studio)
> - See "Build Configuration" section below for details

```
┌─────────────────────────────────────────────────────────────────┐
│                    VS Code / Copilot Chat                        │
│                         (MCP Client)                             │
└─────────────────────────┬───────────────────────────────────────┘
                          │ stdio / JSON-RPC
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                 twincat_mcp_server.py (Python)                   │
│  - Handles MCP protocol (tools/list, tools/call)                │
│  - Validates inputs                                              │
│  - Calls TcAutomation.exe via subprocess                        │
│  - Formats responses for Copilot                                │
└─────────────────────────┬───────────────────────────────────────┘
                          │ subprocess (JSON output)
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│          TcAutomation.exe (.NET Framework 4.7.2 CLI)             │
│  Commands:                                                       │
│    build    - Build solution, return errors as JSON             │
│    info     - Get project info (TC version, PLCs, etc.)         │
│    activate - Activate config on target                         │
│    deploy   - Deploy boot project                               │
└─────────────────────────┬───────────────────────────────────────┘
                          │ COM Interop
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│           TcXaeShell.DTE.17.0 / VisualStudio.DTE                │
│              + TCatSysManagerLib (COM)                           │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🎯 MCP Tools Specification

### Phase 1: Build & Validate (CURRENT FOCUS)

#### Tool: `twincat_build`
```json
{
  "name": "twincat_build",
  "description": "Build a TwinCAT solution and return any compile errors or warnings. Use this to validate TwinCAT/PLC code changes.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "solutionPath": {
        "type": "string",
        "description": "Full path to the TwinCAT .sln file"
      },
      "clean": {
        "type": "boolean",
        "description": "Clean solution before building",
        "default": true
      },
      "tcVersion": {
        "type": "string",
        "description": "Force specific TwinCAT version (e.g., '3.1.4026.17'). Optional - uses project version if not specified."
      }
    },
    "required": ["solutionPath"]
  }
}
```

**Returns:**
```json
{
  "success": true,
  "buildTime": "5.2s",
  "errors": [],
  "warnings": [
    {
      "code": "TC0001",
      "message": "Unused variable 'x'",
      "file": "MAIN.TcPOU",
      "line": 15
    }
  ],
  "summary": "Build succeeded with 0 errors and 1 warning"
}
```

#### Tool: `twincat_get_info`
```json
{
  "name": "twincat_get_info",
  "description": "Get information about a TwinCAT solution including version, PLC projects, and configuration.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "solutionPath": {
        "type": "string",
        "description": "Full path to the TwinCAT .sln file"
      }
    },
    "required": ["solutionPath"]
  }
}
```

**Returns:**
```json
{
  "solutionPath": "C:\\Projects\\MyProject.sln",
  "tcVersion": "3.1.4026.17",
  "tcVersionPinned": true,
  "visualStudioVersion": "17.0",
  "plcProjects": [
    {
      "name": "MyPLC",
      "amsPort": 851,
      "taskCount": 1
    }
  ],
  "targetPlatform": "TwinCAT RT (x64)"
}
```

### Phase 2: Deployment (FUTURE)

#### Tool: `twincat_activate`
```json
{
  "name": "twincat_activate",
  "description": "Activate TwinCAT configuration on a target PLC",
  "inputSchema": {
    "type": "object",
    "properties": {
      "solutionPath": { "type": "string" },
      "targetAmsNetId": { "type": "string", "description": "Target AMS Net ID (e.g., '5.22.157.86.1.1')" },
      "restartRuntime": { "type": "boolean", "default": true }
    },
    "required": ["solutionPath", "targetAmsNetId"]
  }
}
```

#### Tool: `twincat_deploy_boot`
```json
{
  "name": "twincat_deploy_boot",
  "description": "Deploy boot project to target PLC with autostart",
  "inputSchema": {
    "type": "object",
    "properties": {
      "solutionPath": { "type": "string" },
      "targetAmsNetId": { "type": "string" },
      "plcName": { "type": "string", "description": "Specific PLC name or omit for all" },
      "enableAutostart": { "type": "boolean", "default": true }
    },
    "required": ["solutionPath", "targetAmsNetId"]
  }
}
```

### Phase 3: Advanced (FUTURE)

- `twincat_set_variant` - Set active project variant
- `twincat_list_tasks` - List real-time tasks
- `twincat_configure_task` - Enable/disable tasks
- `twincat_disable_io` - Disable I/O devices for testing

---

## 📁 Project Structure

```
twincat-mcp/
├── PLAN.md                    # This file - full project plan
├── PROGRESS.md                # Progress tracking across sessions
├── README.md                  # User-facing setup/usage guide
│
├── TcAutomation/              # .NET CLI Tool
│   ├── TcAutomation.csproj    # Classic format, .NET Framework 4.7.2
│   ├── Program.cs             # Entry point with command routing
│   ├── Commands/
│   │   ├── BuildCommand.cs    # Build solution command
│   │   ├── InfoCommand.cs     # Get project info command
│   │   ├── ActivateCommand.cs # Activate configuration (Phase 2)
│   │   └── DeployCommand.cs   # Deploy boot project (Phase 2)
│   ├── Core/
│   │   ├── VisualStudioInstance.cs   # VS DTE management
│   │   ├── AutomationInterface.cs    # TwinCAT automation wrapper
│   │   ├── MessageFilter.cs          # COM message filter
│   │   └── TcFileUtilities.cs        # File parsing utilities
│   ├── Models/
│   │   ├── BuildResult.cs     # Build result model
│   │   └── ProjectInfo.cs     # Project info model
│   └── bin/Release/           # Build output (gitignored)
│
├── mcp-server/                # Python MCP Server
│   ├── server.py              # Main MCP server
│   └── requirements.txt       # Python dependencies
│
├── .vscode/
│   ├── mcp.json               # MCP server configuration for VS Code
│   ├── tasks.json             # Build tasks
│   └── launch.json            # Debug configurations
│
├── scripts/
│   ├── setup.ps1              # First-time setup (builds + installs)
│   ├── build.ps1              # Build with MSBuild
│   ├── test-cli.ps1           # Test CLI directly
│   ├── test-mcp.ps1           # Test with MCP Inspector
│   └── test-mcp-automated.ps1 # Automated test suite
│
└── .gitignore                 # Ignores bin/, obj/, __pycache__/
```

---

## 🧪 Testing Strategy

### Automated Test Suite

Location: `scripts/test-mcp-automated.ps1`

```powershell
# Quick test (skips build)
.\scripts\test-mcp-automated.ps1 -SkipBuild

# Full test (includes build)
.\scripts\test-mcp-automated.ps1

# Custom solution
.\scripts\test-mcp-automated.ps1 -Solution "path\to\solution.sln"
```

### Test Coverage

| Layer | Tests |
|-------|-------|
| CLI Executable | Exists, --help responds |
| CLI Commands | info returns JSON with tcVersion, plcProjects |
| CLI Commands | build returns JSON with success, errors, warnings |
| MCP Server | find_tc_automation_exe() locates CLI |
| MCP Server | run_tc_automation('info') returns valid data |
| MCP Server | run_tc_automation('build') returns valid data |

### When to Run Tests

1. **After modifying any C# code** - Run full test
2. **After modifying server.py** - Run with `-SkipBuild`
3. **Before committing** - Run full test
4. **After cloning to new PC** - Run full test to verify setup

---

## 🔧 Technical Details

### Build Configuration

#### Why .NET Framework (not .NET 8)?

The TwinCAT Automation Interface and Visual Studio DTE are **COM components**. Building a project that references them requires MSBuild's `ResolveComReference` task, which:
- ✅ Works with: **MSBuild.exe** (from Visual Studio)
- ❌ Fails with: **dotnet build** (any .NET version)

Error you'll see if you try `dotnet build`:
```
error MSB4803: The task "ResolveComReference" is not supported on the .NET Core version of MSBuild.
```

#### Project Format: Classic vs SDK-style

| Aspect | Classic .csproj | SDK-style .csproj |
|--------|-----------------|-------------------|
| COM References | ✅ Native support | ⚠️ Requires MSBuild.exe |
| File globbing | Manual <Compile> items | Automatic |
| NuGet | PackageReference works | PackageReference works |
| Build tool | MSBuild.exe | dotnet or MSBuild |

We use **classic format** for simplicity - it "just works" with MSBuild.

#### TcAutomation.csproj Key Settings

```xml
<TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>  <!-- Must be installed -->
<LangVersion>8.0</LangVersion>                           <!-- For nullable types -->
<PlatformTarget>x64</PlatformTarget>                     <!-- Match TwinCAT install -->

<!-- COM References -->
<COMReference Include="TCatSysManagerLib">
  <Guid>{3C49D6C3-93DC-11D0-B162-00A0248C244B}</Guid>
</COMReference>
```

#### Build Command

```powershell
# Find MSBuild from Visual Studio
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$msbuild = Join-Path $vsPath "MSBuild\Current\Bin\MSBuild.exe"

# Build
& $msbuild "TcAutomation.csproj" /p:Configuration=Release /p:Platform=x64 /restore
```

### TwinCAT Version Compatibility
- **Target**: TwinCAT 3.1.4026.x (your installed version)
- **DTE ProgIDs to try** (in order):
  1. `TcXaeShell.DTE.17.0` (TwinCAT XAE Shell - preferred)
  2. `VisualStudio.DTE.17.0` (VS2022 with TwinCAT integration)

### COM Type Library Reference
- **Name**: `Beckhoff TwinCAT XAE Base 3.3 Type Library`
- **ProgID**: `TCatSysManagerLib`
- **Location**: Usually `C:\TwinCAT\3.1\Components\Base\TCatSysManager.dll`

### Key TwinCAT Tree Shortcuts
```csharp
"TIPC"  // PLC Configuration
"TIID"  // I/O Devices
"TIRT"  // Real-Time Tasks
"TIRS"  // Real-Time Settings
"TIRR"  // Route Settings
```

### Prerequisites Summary

| Software | Version | Purpose |
|----------|---------|---------|
| Visual Studio 2022 | Any edition | Provides MSBuild.exe |
| .NET Framework 4.7.2 | Targeting Pack | Build target |
| TwinCAT XAE | 3.1.4024+ | COM libraries |
| Python | 3.10+ | MCP server |
| VS Code | Latest | Copilot integration |

### Python Requirements
- **Python 3.10+**
- **mcp** package (MCP SDK)

---

## 🚀 Setup Instructions (for cloning to new PC)

### Prerequisites
1. **Visual Studio 2022** (Community is free) with ".NET desktop development" workload
2. **.NET Framework 4.7.2 Targeting Pack** (check in Visual Studio Installer → Individual Components)
3. **TwinCAT XAE** 3.1.4024+ installed
4. **Python 3.10+** installed
5. **VS Code** with Copilot extension

### First-time Setup (Recommended)
```powershell
# 1. Clone repository
git clone <repo-url>
cd twincat-mcp

# 2. Run setup script (builds + installs dependencies)
.\scripts\setup.ps1

# 3. Restart VS Code to load MCP server
```

### Manual Setup
```powershell
# 1. Find MSBuild
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$vsPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$msbuild = Join-Path $vsPath "MSBuild\Current\Bin\MSBuild.exe"

# 2. Build .NET CLI tool (do NOT use 'dotnet build')
& $msbuild "TcAutomation\TcAutomation.csproj" /p:Configuration=Release /p:Platform=x64 /restore

# 3. Install Python dependencies
cd mcp-server
pip install -r requirements.txt
cd ..

# 4. Restart VS Code to load MCP server
```

### Testing
```powershell
# Test .NET CLI directly
.\TcAutomation\bin\Release\TcAutomation.exe build --solution "C:\path\to\project.sln"

# Test MCP server with inspector
npx @modelcontextprotocol/inspector -- python mcp-server/server.py
```

---

## 📚 Reference Code

This project is based on analysis of:
- `rw-tcunit-runner-eponce-cicd_improvements/TcUnit-Runner/` - Test runner with full automation
- `rw-tcunit-runner-eponce-cicd_improvements/TcDeploy/` - Deployment tool

Key patterns borrowed:
1. **VisualStudioInstance.cs** - DTE loading with version detection
2. **AutomationInterface.cs** - ITcSysManager wrapper
3. **MessageFilter.cs** - COM retry handling
4. **TcFileUtilities.cs** - Solution/project parsing

---

## ⚠️ Known Limitations

1. **Windows Only** - TwinCAT is Windows-only, so this MCP server only works on Windows
2. **Single Instance** - Only one VS DTE can be active at a time per user session
3. **Build Time** - First build after loading takes longer (VS startup)
4. **COM Threading** - Must run in STA thread (handled by CLI tool)

---

## 📝 Notes for AI Assistant (Context Recovery)

If you're reading this after losing context:

1. **Goal**: Create MCP server so Copilot can build TwinCAT projects and see errors
2. **Architecture**: Python MCP server → calls → .NET CLI tool → COM → TwinCAT
3. **Current Phase**: Phase 1 - Build & Validate
4. **Reference Code**: In `../rw-tcunit-runner-eponce-cicd_improvements/`
5. **User's TwinCAT**: Version 3.1.4026.17-20 installed
6. **Check PROGRESS.md**: For current implementation status

### Critical Build Info
- **DO NOT use `dotnet build`** - COM references will fail (MSB4803)
- **Use MSBuild.exe** from Visual Studio instead
- **Target**: .NET Framework 4.7.2 (not .NET 8)
- **Build script**: `.\scripts\build.ps1`

### Output Location
- Built executable: `TcAutomation\bin\Release\TcAutomation.exe`
