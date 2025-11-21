# MCP Test Server - Verification Guide

## Quick Verification Steps

### 1. Build the Project
```powershell
cd D:\github\mcp.discovery.tool\mcp-discover.tests
dotnet build -c Release
```

**Expected Result:** Build succeeds (warnings about discovery tool are expected but not critical)

### 2. View Generated Metadata
```powershell
.\view-metadata.ps1
```

**Expected Output:**
- 3 classes discovered (CalculatorTools, DocumentationResources, PromptCatalog)
- 8 total members (4 tools, 2 resources, 2 prompts)
- Detailed information about each capability

### 3. Run the Test Server
```powershell
.\run.ps1
```

**Expected Output:**
```
MCP Test Server Runner
======================

Starting MCP Test Server...
Press Ctrl+C to stop

MCP Test Server Started
Server Info: v1.0.0
Available capabilities: tools, resources, prompts
```

The server will run indefinitely. Press Ctrl+C to stop.

## What Was Implemented

### Core Components

1. **McpAttributes.cs** - Defines MCP attribute types:
   - `McpServerToolType`, `McpServerTool` - For tool definitions
   - `McpServerResourceType`, `McpServerResource` - For resource definitions
   - `McpServerPromptType`, `McpServerPrompt` - For prompt definitions
   - `Description`, `McpAudience` - For metadata

2. **SampleServer.cs** - Main server implementation with:
   - `Program.Main()` - Entry point that starts the server
   - `CalculatorTools` - Example tool group with 4 operations
   - `DocumentationResources` - Example resource group with 2 resources
   - `PromptCatalog` - Example prompt group with 2 prompts

3. **mcp-discover.tests.csproj** - Project configuration:
   - Builds as executable (OutputType=Exe)
   - Auto-runs discovery tool after build
   - Generates metadata JSON file

### Utility Scripts

1. **run.ps1** - Convenience script to run the server
   - Optional `-Build` flag to build before running
   - Configuration parameter (Debug/Release)
   - Clear output formatting

2. **view-metadata.ps1** - Displays generated metadata
   - Color-coded output
   - Hierarchical display of capabilities
   - Summary statistics

## File Structure

```
mcp-discover.tests/
├── mcp-discover.tests.csproj   # Project file with MSBuild targets
├── McpAttributes.cs             # Attribute definitions
├── SampleServer.cs              # Server implementation
├── README.md                    # Documentation
├── run.ps1                      # Server runner script
├── view-metadata.ps1            # Metadata viewer script
├── VERIFICATION.md              # This file
└── bin/
    └── Release/
        └── net10.0/
            ├── mcp-test-server.exe
            ├── mcp-test-server.dll
            └── mcp-metadata/
                └── mcp-metadata.json

```

## Capabilities Summary

### Tools (Calculator)
| Name     | Description                          | Audience   |
|----------|--------------------------------------|------------|
| add      | Adds two integers and returns sum    | developers |
| subtract | Subtracts second integer from first  | developers |
| multiply | Multiplies two integers              | developers |
| slow     | Simulates async operation with delay | ops        |

### Resources (Documentation)
| Name    | Description                      | Audience         |
|---------|----------------------------------|------------------|
| readme  | Returns server README content    | docs, developers |
| version | Returns version and capabilities | docs, developers |

### Prompts (Ideas)
| Name       | Description                    | Audience           |
|------------|--------------------------------|--------------------|
| brainstorm | Creative brainstorming prompt  | ideation, creative |
| analyze    | Code analysis prompt           | ideation, creative |

## Build Process Flow

1. **Compile** - C# code is compiled to assembly
2. **Discovery** - MCP discovery tool scans the compiled assembly
3. **Metadata** - JSON metadata file is generated with all discovered capabilities
4. **Output** - Executable and metadata are placed in bin directory

## Known Issues

- Build warnings about discovery tool exit code (harmless - metadata is generated correctly)
- Server runs indefinitely - must use Ctrl+C to stop
- Console output only (no actual MCP protocol implementation)

## Next Steps

To extend this test server:

1. Add new tool/resource/prompt methods to SampleServer.cs
2. Decorate them with appropriate MCP attributes
3. Build the project
4. View updated metadata with `.\view-metadata.ps1`
5. Run and test with `.\run.ps1`

## Troubleshooting

### "Server executable not found"
- Run `dotnet build -c Release` first
- Check that build succeeded without errors

### "Metadata file not found"
- Ensure mcp-discover tool is built: `cd ..\mcp-discover; dotnet build -c Release`
- Rebuild the test project: `dotnet build -c Release`

### Server doesn't start
- Check that .NET 10 SDK is installed
- Run directly: `.\bin\Release\net10.0\mcp-test-server.exe`
- Check for port conflicts or other runtime issues

