# mcp-discover-dotnet

A .NET command-line tool for discovering MCP (Model Context Protocol) capabilities in compiled assemblies through reflection.

## What It Does

`mcp-discover-dotnet` scans .NET assemblies (DLLs) and extracts MCP server metadata by analyzing custom attributes. It identifies:

- **Tools** - Executable functions decorated with `[McpServerToolType]` and `[McpServerTool]`
- **Resources** - Content providers decorated with `[McpServerResourceType]` and `[McpServerResource]`
- **Prompts** - Template providers decorated with `[McpServerPromptType]` and `[McpServerPrompt]`

The tool generates a structured JSON file (`mcp-metadata.json`) containing all discovered capabilities with their metadata.

## Installation

### As a Global .NET Tool (Recommended)

```bash
dotnet tool install -g mcp-discover-dotnet
```

### From Source

```bash
git clone <repository-url>
cd mcp.discovery.tool/mcp-discover-dotnet
dotnet build -c Release
```

The executable will be at `bin/Release/net10.0/mcp-discover-dotnet.exe` (Windows) or `bin/Release/net10.0/mcp-discover-dotnet` (Linux/macOS).

## Usage

### Command Syntax

```bash
mcp-discover-dotnet <input-directory> <output-directory> [options]
```

**Arguments:**
- `input-directory` - Directory containing .dll assemblies to scan (scans recursively)
- `output-directory` - Directory where `mcp-metadata.json` will be written

**Options:**
- `-h, --help` - Display help information
- `-m, --markdown` - Generate markdown report alongside JSON output
- `-o, --omit-path` - Omit base path from assembly paths in output (use relative paths)

### Examples

**Basic usage:**
```bash
mcp-discover-dotnet ./bin/Release/net10.0 ./metadata
```

**Generate markdown report:**
```bash
mcp-discover-dotnet ./bin/Release/net10.0 ./metadata --markdown
```

**Use relative paths in output:**
```bash
mcp-discover-dotnet ./bin/Release/net10.0 ./metadata --omit-path
```

**Combine options:**
```bash
mcp-discover-dotnet ./bin/Release/net10.0 ./metadata --markdown --omit-path
```

**With .NET tool:**
```bash
dotnet tool run mcp-discover-dotnet ./bin/Release/net10.0 ./metadata -m -o
```

**Scan current build output:**
```bash
mcp-discover-dotnet . ./mcp-output
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

**Note on Assembly Paths:**
- By default, `AssemblyPath` contains the full absolute path to each assembly
- With `--omit-path`, paths are relative to the input directory (e.g., `bin\Debug\MyAssembly.dll` instead of `C:\Projects\MyApp\bin\Debug\MyAssembly.dll`)
- This is useful for creating portable metadata files that can be shared across different environments

### Markdown Report Format (Optional)

When using the `--markdown` flag, the tool also generates a human-readable markdown report (`mcp-metadata.md`) that includes:

- **Summary section** with counts of discovered capabilities
- **Tools section** with tables organized by tool type
- **Resources section** with tables organized by resource type
- **Prompts section** with tables organized by prompt type
- **Assembly details** with breakdown by assembly

Example markdown table output:

```markdown
## 🔧 Tools

### Mcp.TestServer.CalculatorTools

*Basic arithmetic operations*

| Name | Title | Description | Audiences |
|------|-------|-------------|-----------|
| `add` | Add Numbers | Adds two integers | developers |
| `subtract` | Subtract Numbers | Subtracts integers | developers |
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
    <Message Text="Running MCP Discovery Tool..." Importance="high" />

    <Message Text="Restoring first..." Importance="high" />
    <!-- Run the restore -->
    <Exec Command="dotnet tool restore"
          IgnoreExitCode="false"
          ContinueOnError="true"
    >
        <Output TaskParameter="ConsoleOutput" PropertyName="McpDiscoveryOutput" />
    </Exec>
    
    <Message Text="$(McpDiscoveryOutput)" Importance="high" />
    
    <!-- Define paths -->
    <PropertyGroup>
        <McpDiscoverTool>dotnet tool run mcp-discover-dotnet</McpDiscoverTool>
        <InputDir>$(MSBuildProjectDirectory)\$(OutputPath)</InputDir>
        <OutputDir>$(MSBuildProjectDirectory)\mcp-metadata</OutputDir>
    </PropertyGroup>

    <!-- Run the discovery tool -->
    <Exec Command="$(McpDiscoverTool) $(InputDir) $(OutputDir)"
          IgnoreExitCode="false"
          ContinueOnError="true"
    >
        <Output TaskParameter="ConsoleOutput" PropertyName="McpDiscoveryOutput" />
    </Exec>

    <Message Text="$(McpDiscoveryOutput)" Importance="high" />
    <Message Text="MCP metadata generated at: $(OutputDir)" Importance="high" />
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
mcp-discover-dotnet ./bin/Release/net10.0 ./metadata

# Output: ./mcp-metadata/mcp-metadata.json
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
    dotnet tool install -g mcp-discover-dotnet
    mcp-discover-dotnet ./bin/Release/net10.0 ./metadata
    
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

## Author

echapmanFromBunnings

## Related

- [Repository Root README](../README.md) - Complete repository documentation
- [Test Server](../mcp-discover.tests) - Working example MCP server
- [Model Context Protocol](https://modelcontextprotocol.io/) - MCP standard documentation

## Support

For issues, feature requests, or questions, please refer to the main repository.

