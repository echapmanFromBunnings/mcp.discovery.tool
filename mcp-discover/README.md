# mcp-discover

A .NET command-line tool for discovering MCP (Model Context Protocol) capabilities in compiled assemblies through reflection.

## What It Does

`mcp-discover` scans .NET assemblies (DLLs) and extracts MCP server metadata by analyzing custom attributes. It identifies:

- **Tools** - Executable functions decorated with `[McpServerToolType]` and `[McpServerTool]`
- **Resources** - Content providers decorated with `[McpServerResourceType]` and `[McpServerResource]`
- **Prompts** - Template providers decorated with `[McpServerPromptType]` and `[McpServerPrompt]`

The tool generates a structured JSON file (`mcp-metadata.json`) containing all discovered capabilities with their metadata.

## Installation

### As a Global .NET Tool (Recommended)

```bash
dotnet tool install -g mcp-discover
```

### From Source

```bash
git clone <repository-url>
cd mcp.discovery.tool/mcp-discover
dotnet build -c Release
```

The executable will be at `bin/Release/net10.0/mcp-discover.exe` (Windows) or `bin/Release/net10.0/mcp-discover` (Linux/macOS).

## Usage

### Command Syntax

```bash
mcp-discover <input-directory> <output-directory>
```

**Arguments:**
- `input-directory` - Directory containing .dll assemblies to scan (scans recursively)
- `output-directory` - Directory where `mcp-metadata.json` will be written

**Options:**
- `-h, --help` - Display help information

### Examples

**Basic usage:**
```bash
mcp-discover ./bin/Release/net10.0 ./metadata
```

**With .NET tool:**
```bash
dotnet tool run mcp-discover ./bin/Release/net10.0 ./metadata
```

**Scan current build output:**
```bash
mcp-discover . ./mcp-output
```

## Output Format

The tool generates a JSON file with the following structure:

```json
{
  "GeneratedAtUtc": "2025-11-22T10:30:00Z",
  "Assemblies": [
    {
      "AssemblyPath": "/path/to/assembly.dll",
      "Classes": [
        {
          "TypeName": "Namespace.ClassName",
          "Kind": "ToolType|ResourceType|PromptType",
          "Description": "Class description",
          "Audiences": ["audience1", "audience2"],
          "Members": [
            {
              "MethodName": "MethodName",
              "Kind": "Tool|Resource|Prompt",
              "Name": "capability-name",
              "Title": "Display Title",
              "Description": "Detailed description",
              "Audiences": ["audience"]
            }
          ]
        }
      ]
    }
  ]
}
```

## Required Attributes

Your .NET assemblies must use these custom attributes for discovery:

### Class-Level Attributes

```csharp
[McpServerToolType("group-name", "Display Title")]
public class MyTools { }

[McpServerResourceType("group-name", "Display Title")]
public class MyResources { }

[McpServerPromptType("group-name", "Display Title")]
public class MyPrompts { }
```

### Method-Level Attributes

```csharp
[McpServerTool("tool-name", "Display Title")]
[Description("What this tool does")]
[McpAudience("developers", "qa")]
public static int MyTool(int param) { return param; }

[McpServerResource("resource-name", "Display Title")]
public string GetResource() { return "content"; }

[McpServerPrompt("prompt-name", "Display Title")]
public string GetPrompt() { return "prompt template"; }
```

### Metadata Attributes

```csharp
[Description("Detailed description")]
[McpAudience("audience1", "audience2")]
```

See the [mcp-discover.tests](../mcp-discover.tests) project for complete examples.

## Integration with MSBuild

Add this target to your `.csproj` file to automatically run discovery after build:

```xml
<Target Name="RunMcpDiscovery" AfterTargets="Build">
  <PropertyGroup>
    <McpDiscoverTool>mcp-discover</McpDiscoverTool>
    <InputDir>$(MSBuildProjectDirectory)\$(OutputPath)</InputDir>
    <OutputDir>$(MSBuildProjectDirectory)\$(OutputPath)mcp-metadata</OutputDir>
  </PropertyGroup>
  
  <Exec Command="$(McpDiscoverTool) &quot;$(InputDir)&quot; &quot;$(OutputDir)&quot;" />
</Target>
```

## How It Works

1. **Scans** all `*.dll` files in the input directory recursively
2. **Loads** each assembly in an isolated `AssemblyLoadContext` (safe, collectible)
3. **Reflects** over all types looking for MCP attributes
4. **Extracts** metadata from attribute constructor arguments and properties
5. **Serializes** the results to JSON
6. **Unloads** the assembly context to prevent memory leaks

## Requirements

- .NET 10.0 or later
- Target assemblies must be .NET assemblies (not native code)
- Assemblies should be built before scanning

## Error Handling

The tool handles common issues gracefully:

- **Missing assemblies** - Warning logged, scanning continues
- **Load failures** - Assembly skipped with warning message
- **No attributes found** - Assembly excluded from output
- **Invalid metadata** - Null/empty values normalized or omitted

## Exit Codes

- `0` - Success (metadata generated)
- `1` - Error (invalid arguments, input directory not found)

## Examples

### Scan After Build

```bash
# Build your MCP server
dotnet build -c Release

# Run discovery
mcp-discover ./bin/Release/net10.0 ./metadata

# Output: ./metadata/mcp-metadata.json
```

### View Results

```bash
# Windows (PowerShell)
Get-Content ./metadata/mcp-metadata.json | ConvertFrom-Json | ConvertTo-Json -Depth 10

# Linux/macOS
cat ./metadata/mcp-metadata.json | jq .
```

### CI/CD Pipeline

```yaml
# GitHub Actions example
- name: Build and Discover
  run: |
    dotnet build -c Release
    dotnet tool install -g mcp-discover
    mcp-discover ./bin/Release/net10.0 ./metadata
    
- name: Upload Metadata
  uses: actions/upload-artifact@v3
  with:
    name: mcp-metadata
    path: ./metadata/mcp-metadata.json
```

## Troubleshooting

**Problem:** `error: input directory not found`  
**Solution:** Ensure the path exists and is accessible. Use absolute paths if needed.

**Problem:** `warning: no assemblies found`  
**Solution:** Check that .dll files exist in the input directory.

**Problem:** `warning: failed to scan 'assembly.dll'`  
**Solution:** Assembly may have dependencies not in the same directory, or may be incompatible.

**Problem:** Empty output / no capabilities found  
**Solution:** Verify your assemblies have the correct MCP attributes applied.

## Version

Current version: **1.0.0**

## Author

echapmanFromBunnings

## Related

- [Repository Root README](../README.md) - Complete repository documentation
- [Test Server](../mcp-discover.tests) - Working example MCP server
- [Model Context Protocol](https://modelcontextprotocol.io/) - MCP standard documentation

## Support

For issues, feature requests, or questions, please refer to the main repository.

