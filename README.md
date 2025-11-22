# MCP Discovery Tool

A command-line tool that scans .NET assemblies to discover and extract MCP (Model Context Protocol) server capabilities through reflection. It identifies tools, resources, and prompts defined via custom attributes and generates structured JSON metadata.

## What This Repository Contains

This repository includes:

- **mcp-discover** - The main discovery tool that scans assemblies for MCP metadata
- **mcp-discover.tests** - A working test MCP server with examples of all capability types

## What the Tool Does

The `mcp-discover` tool analyzes compiled .NET assemblies (DLLs) and extracts MCP server capabilities by:

1. **Scanning** all assemblies in a specified directory
2. **Discovering** classes and methods decorated with MCP attributes:
   - `[McpServerToolType]` / `[McpServerTool]` - Executable functions/tools
   - `[McpServerResourceType]` / `[McpServerResource]` - Static content/resources
   - `[McpServerPromptType]` / `[McpServerPrompt]` - AI prompt templates
3. **Extracting** metadata including names, descriptions, and audience targeting
4. **Generating** a structured JSON file with all discovered capabilities

### Output Format

The tool generates a `mcp-metadata.json` file with:
- Assembly information
- Discovered classes with MCP attributes
- Methods/members with their metadata (name, title, description, audiences)
- Timestamp of generation

Optionally, with the `--markdown` flag, it also generates a `mcp-metadata.md` file with:
- Human-readable summary
- Tables organized by capability type (Tools, Resources, Prompts)
- Assembly details and statistics

## Installation & Usage

### Prerequisites

- .NET 10.0 SDK or later

### Building from Source

```bash
# Clone the repository
git clone <repository-url>
cd mcp.discovery.tool

# Build the discovery tool
cd mcp-discover
dotnet build -c Release

# The executable will be at:
# ./bin/Release/net10.0/mcp-discover.exe (Windows)
# ./bin/Release/net10.0/mcp-discover (Linux/macOS)
```

### Using as a .NET Tool (Recommended)

The tool can be installed as a global .NET tool:

```bash
# Pack the tool
dotnet pack -c Release

# Install globally
dotnet tool install --global --add-source ./bin/Release mcp-discover

# Use from anywhere
mcp-discover <input-directory> <output-directory>
```

### Direct Usage

```bash
# Run directly
mcp-discover.exe <input-directory> <output-directory>

# Or with dotnet
dotnet run --project mcp-discover -- <input-directory> <output-directory>
```

### Command-Line Syntax

```
mcp-discover <input-directory> <output-directory>

Arguments:
  input-directory   Directory containing .dll assemblies to scan
  output-directory  Directory where mcp-metadata.json will be written

Options:
  -h, --help       Show help information
  -m, --markdown   Generate markdown report alongside JSON
```

### Example

```bash
# Scan assemblies in bin/Release/net10.0 and output to metadata folder
mcp-discover ./bin/Release/net10.0 ./metadata

# Generate both JSON and markdown reports
mcp-discover ./bin/Release/net10.0 ./metadata --markdown

# Output: ./metadata/mcp-metadata.json
#         ./metadata/mcp-metadata.md (if --markdown used)
```

## Quick Start with Test Server

The repository includes a complete test server that demonstrates all MCP capability types:

```bash
# Navigate to the test server
cd mcp-discover.tests

# Build (automatically runs discovery tool)
dotnet build -c Release

# Run the server
.\run.ps1

# View generated metadata
.\view-metadata.ps1
```

See [mcp-discover.tests/README.md](./mcp-discover.tests/README.md) for detailed documentation.

## MCP Attributes Reference

To make your .NET assemblies discoverable, use these attributes:

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
[Description("Detailed description of what this tool does")]
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

## Example Output

```json
{
  "GeneratedAtUtc": "2025-11-22T10:30:00Z",
  "Assemblies": [
    {
      "AssemblyPath": "C:\\path\\to\\server.dll",
      "Classes": [
        {
          "TypeName": "MyApp.CalculatorTools",
          "Kind": "ToolType",
          "Description": "Math operations",
          "Audiences": ["developers"],
          "Members": [
            {
              "MethodName": "Add",
              "Kind": "Tool",
              "Name": "add",
              "Title": "Add Numbers",
              "Description": "Adds two integers",
              "Audiences": ["developers"]
            }
          ]
        }
      ]
    }
  ]
}
```

## Integration with Build Process

You can integrate the discovery tool into your MSBuild process:

```xml
<Target Name="RunMcpDiscovery" AfterTargets="Build">
  <PropertyGroup>
    <McpDiscoverTool>path\to\mcp-discover.exe</McpDiscoverTool>
    <InputDir>$(MSBuildProjectDirectory)\$(OutputPath)</InputDir>
    <OutputDir>$(MSBuildProjectDirectory)\$(OutputPath)mcp-metadata</OutputDir>
  </PropertyGroup>
  
  <Exec Command="&quot;$(McpDiscoverTool)&quot; &quot;$(InputDir)&quot; &quot;$(OutputDir)&quot;" />
</Target>
```

See [mcp-discover.tests/mcp-discover.tests.csproj](./mcp-discover.tests/mcp-discover.tests.csproj) for a working example.

## Repository Structure

```
mcp.discovery.tool/
├── mcp-discover/              # Discovery tool source code
│   ├── Program.cs             # Main scanning logic
│   └── mcp-discover.csproj    # Tool project file
│
├── mcp-discover.tests/        # Test MCP server
│   ├── SampleServer.cs        # Example server implementation
│   ├── McpAttributes.cs       # Attribute definitions
│   ├── run.ps1                # Server runner script
│   ├── view-metadata.ps1      # Metadata viewer script
│   └── README.md              # Test server documentation
│
├── mcp.discovery.tool.sln     # Solution file
└── README.md                  # This file
```

## Use Cases

- **MCP Server Development**: Automatically generate capability metadata during build
- **Documentation**: Extract and document available tools/resources/prompts
- **Validation**: Verify that MCP attributes are correctly applied
- **Discovery**: Dynamically discover capabilities in MCP server assemblies
- **Testing**: Validate MCP server implementations

## How It Works

1. **Assembly Loading**: Uses isolated `AssemblyLoadContext` to safely load assemblies
2. **Reflection**: Scans types and methods for MCP attributes
3. **Metadata Extraction**: Reads attribute constructor arguments and properties
4. **JSON Generation**: Serializes discovered metadata to structured JSON
5. **Error Handling**: Gracefully handles missing or incompatible assemblies

## Requirements

- **.NET 10.0 SDK** or later
- **Windows, Linux, or macOS**
- Assemblies must be compiled .NET assemblies (.dll files)

## Contributing

Contributions are welcome! Areas for improvement:
- Support for additional metadata attributes
- Performance optimizations for large assembly sets
- Additional output formats (XML, YAML, etc.)
- Integration examples for other build systems

## Author

echapmanFromBunnings

## Related Resources

- [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) - Learn about the MCP standard
- [Test Server Documentation](./mcp-discover.tests/README.md) - Detailed example implementation
- [Verification Guide](./mcp-discover.tests/VERIFICATION.md) - Testing and validation steps

---

**Need Help?** Check the test server in `mcp-discover.tests/` for a complete working example!

