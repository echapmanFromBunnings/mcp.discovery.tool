# MCP Test Server

A basic MCP (Model Context Protocol) test server implementation that demonstrates minimal server functionality with attribute-based capability discovery.

## Overview

This test server provides a simple implementation with three capability types:
- **Tools**: Executable functions that perform operations (Calculator operations)
- **Resources**: Static content providers (Documentation)
- **Prompts**: AI prompt templates (Creative prompts)

## Quick Start

### Using the Run Script (Recommended)
```powershell
# Build and run
.\run.ps1 -Build

# Run only (if already built)
.\run.ps1
```

### Using dotnet CLI
```bash
# Build and run
dotnet run -c Release

# Or build first, then run executable
dotnet build -c Release
.\bin\Release\net10.0\mcp-test-server.exe
```

## Build Process

When you build this project, it automatically:
1. Compiles the test server
2. Runs the MCP discovery tool on the output assembly
3. Generates metadata JSON file at `bin\[Configuration]\net10.0\mcp-metadata\mcp-metadata.json`

The metadata file contains the discovered MCP capabilities (tools, resources, prompts) from the compiled assembly.

## Capabilities

### Tools (Calculator)
- `add(a, b)` - Adds two integers
- `subtract(a, b)` - Subtracts second integer from first
- `multiply(a, b)` - Multiplies two integers
- `slow(delayMs)` - Async operation with configurable delay

### Resources (Documentation)
- `readme` - Returns the server README content
- `version` - Returns version and capability information

### Prompts (Ideas)
- `brainstorm` - Creative brainstorming prompt template
- `analyze` - Code analysis prompt template

## Features

- Console logging for all operations
- Async operation support
- MCP attribute-based metadata
- Audience targeting for different user groups
- Detailed descriptions for all capabilities

## Architecture

The server uses attribute-based metadata to define capabilities:
- `[McpServerToolType]` - Defines a tool group
- `[McpServerTool]` - Marks a method as a callable tool
- `[McpServerResourceType]` - Defines a resource group
- `[McpServerResource]` - Marks a method as a resource provider
- `[McpServerPromptType]` - Defines a prompt group
- `[McpServerPrompt]` - Marks a method as a prompt provider

Additional metadata attributes:
- `[Description]` - Provides detailed descriptions
- `[McpAudience]` - Targets specific user groups

## Project Structure

```
mcp-discover.tests/
├── mcp-discover.tests.csproj   # Project file with auto-discovery build target
├── McpAttributes.cs             # MCP attribute definitions
├── SampleServer.cs              # Main server implementation with examples
├── README.md                    # This file
├── run.ps1                      # Convenience script to run the server
├── view-metadata.ps1            # Script to view generated metadata
└── bin/
    └── Release/
        └── net10.0/
            ├── mcp-test-server.exe  # Built executable
            └── mcp-metadata/
                └── mcp-metadata.json # Auto-generated metadata
```

## Generated Metadata

After building, check the generated metadata file to see how the discovery tool extracts MCP capabilities:

### Using the Viewer Script (Recommended)
```powershell
.\view-metadata.ps1
```

### Using PowerShell Directly
```powershell
Get-Content .\bin\Release\net10.0\mcp-metadata\mcp-metadata.json | ConvertFrom-Json
```

The metadata includes:
- Assembly information
- Discovered classes with MCP attributes
- Tools, resources, and prompts with their descriptions
- Audience targeting information
- Method signatures and parameter details

## Development

This is a test/example server for the MCP discovery tool. To add new capabilities:

1. Define attributes in `McpAttributes.cs` (already provided)
2. Create new classes/methods in `SampleServer.cs` decorated with MCP attributes
3. Build the project - metadata will be auto-generated
4. Run the server to test

## Notes

- The server currently runs indefinitely (press Ctrl+C to stop)
- Console logging shows when each capability is accessed
- The discovery tool scans the compiled assembly at build time
- Metadata generation warnings during build are expected if the discovery tool encounters issues
