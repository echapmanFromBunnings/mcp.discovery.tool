# MCP Discovery & Security Analysis Tool

A comprehensive command-line tool that scans .NET assemblies to discover MCP (Model Context Protocol) server capabilities and analyze them for security vulnerabilities. It performs deep reflection-based analysis to identify tools, resources, and prompts, while detecting potential security risks including prompt injection, tool poisoning, toxic flows, hardcoded secrets, and missing security controls.

## What This Repository Contains

This repository includes:

- **mcp-discover-dotnet** - The main discovery tool that scans assemblies for MCP metadata
- **mcp-discover.tests** - A working test MCP server with examples of all capability types

## What the Tool Does

The `mcp-discover` tool provides comprehensive analysis of .NET MCP server assemblies:

### MCP Discovery Capabilities

1. **Scanning** all assemblies in a specified directory
2. **Discovering** classes and methods decorated with MCP attributes:
   - `[McpServerToolType]` / `[McpServerTool]` - Executable functions/tools
   - `[McpServerResourceType]` / `[McpServerResource]` - Static content/resources
   - `[McpServerPromptType]` / `[McpServerPrompt]` - AI prompt templates
3. **Extracting** metadata including names, descriptions, and audience targeting
4. **Generating** structured output in multiple formats (JSON, SARIF, CSV, Markdown)

### Security Analysis Capabilities

5. **Vulnerability Detection** across 4 categories (20+ heuristics)
6. **Secrets Detection** for hardcoded credentials (API keys, passwords, tokens)
7. **Input Validation Tracking** to reduce false positives
8. **Audit Logging Analysis** to identify missing security logging
9. **CWE Mapping** for industry-standard vulnerability classification
10. **Remediation Guidance** with code examples and documentation links
11. **CI/CD Integration** with configurable severity thresholds and exit codes
12. **File-Based Filtering** to ignore specific files for certain vulnerability categories

### Output Formats

The tool supports multiple output formats:

**JSON (default)** - `mcp-metadata.json`:
- Assembly information
- Discovered classes with MCP attributes
- Methods/members with their metadata (name, title, description, audiences)
- Security analysis findings with CWE mappings and remediation guidance
- Timestamp of generation

**Markdown** - `mcp-metadata.md` (with `--markdown` flag):
- Human-readable summary
- Tables organized by capability type (Tools, Resources, Prompts)
- Security findings grouped by severity (Critical, High, Medium, Low)
- Assembly details and statistics

**SARIF 2.1.0** - `security-report.sarif` (with `--format sarif`):
- Static Analysis Results Interchange Format
- Industry-standard vulnerability reporting
- Integration with GitHub Code Scanning and other SAST tools
- CWE mappings, severity levels, and remediation guidance

**CSV** - `security-report.csv` (with `--format csv`):
- Excel-compatible tabular format
- Columns: Category, Severity, CWE, Title, Description, Location, Recommendation, Evidence
- Easy filtering and sorting for security review

## Installation & Usage

### Prerequisites

- .NET 10.0 SDK or later

### Building from Source

```bash
# Clone the repository
git clone <repository-url>
cd mcp.discovery.tool

# Build the discovery tool
cd mcp-discover-dotnet
dotnet build -c Release

# The executable will be at:
# ./bin/Release/net10.0/mcp-discover-dotnet.exe (Windows)
# ./bin/Release/net10.0/mcp-discover-dotnet (Linux/macOS)
```

### Using as a .NET Tool (Recommended)

The tool can be installed as a global .NET tool as its available on nuget.org:

```bash
# Install globally
dotnet tool install --global --add-source ./bin/Release mcp-discover-dotnet

# Use from anywhere
mcp-discover-dotnet <input-directory> <output-directory>
```

### Direct Usage

```bash
# Run directly
mcp-discover-dotnet.exe <input-directory> <output-directory>

# Or with dotnet
dotnet run --project mcp-discover-dotnet -- <input-directory> <output-directory>
```

### Command-Line Syntax

```
mcp-discover-dotnet <input-directory> <output-directory> [options]

Arguments:
  input-directory   Directory containing .dll assemblies to scan
  output-directory  Directory where output files will be written

Basic Options:
  -h, --help         Show help information
  -m, --markdown     Generate markdown report alongside JSON
  -o, --omit-path    Omit base path from assembly paths in output
  -n, --no-timestamp Omit timestamp from output (for version control)
  -s, --security     Perform security vulnerability analysis

Security Analysis Options:
  --config <path>           Load configuration from JSON file
  --format <format>         Export format: json (default), sarif, csv, or all
  --min-severity <level>    Minimum severity to report: Low, Medium, High, Critical
  --exclude <categories>    Comma-separated categories to exclude
  --fail-on-critical        Exit with code 1 if critical vulnerabilities found
  --fail-on-high            Exit with code 1 if high+ vulnerabilities found
  --verbose                 Display detailed security findings with CWE mappings
```

### Examples

```bash
# Basic MCP discovery
mcp-discover-dotnet ./bin/Release/net10.0 ./metadata

# Generate both JSON and markdown reports
mcp-discover-dotnet ./bin/Release/net10.0 ./metadata --markdown

# Perform security vulnerability scan
mcp-discover-dotnet ./bin/Release/net10.0 ./metadata --security

# Security scan with SARIF export for CI/CD integration
mcp-discover-dotnet ./bin/Release/net10.0 ./metadata --security --format sarif

# Export in all formats (JSON, SARIF, CSV)
mcp-discover-dotnet ./bin/Release/net10.0 ./metadata --security --format all

# Filter to show only High and Critical findings
mcp-discover-dotnet ./bin/Release/net10.0 ./metadata --security --min-severity High

# Exclude specific vulnerability categories
mcp-discover-dotnet ./bin/Release/net10.0 ./metadata --security --exclude PromptInjection,ToxicFlow

# CI/CD integration: fail build if critical vulnerabilities found
mcp-discover-dotnet ./bin/Release/net10.0 ./metadata --security --fail-on-critical

# Comprehensive analysis with verbose output
mcp-discover-dotnet ./bin/Release/net10.0 ./metadata --markdown --security --format all --verbose

# Use configuration file for custom patterns and thresholds
mcp-discover-dotnet ./bin/Release/net10.0 ./metadata --security --config security-config.json

# Use relative paths in output instead of full paths
mcp-discover-dotnet ./bin/Release/net10.0 ./metadata --omit-path --security
```

## Security Vulnerability Detection

The tool performs comprehensive security analysis of MCP servers with pattern-based heuristics and enhanced detection:

### Vulnerability Categories

1. **Prompt Injection Attacks** 💉 (CWE-74)
   - Detects prompts that may accept unsanitized user input
   - Identifies string concatenation patterns in prompt descriptions
   - Flags missing input validation
   - Tracks methods with validation to reduce false positives
   
2. **Tool Poisoning Attacks** ☠️ (CWE-494)
   - Identifies dangerous operations (file system, command execution, database access)
   - Detects file system operations vulnerable to path traversal
   - Flags database operations that may be vulnerable to SQL injection
   - Warns about external API calls susceptible to SSRF
   - Reduces severity when validation methods are detected

3. **Toxic Flow Issues** ⚡ (CWE-693)
   - Detects async operations without timeout configuration
   - Identifies expensive operations without rate limiting
   - Flags resource exhaustion risks

4. **General Security Issues** 🛡️ (CWE-312, CWE-778)
   - **Secrets Detection**: Identifies hardcoded credentials (API keys, passwords, tokens, bearer tokens, AWS secrets, private keys)
   - **Audit Logging**: Detects missing audit logging in sensitive operations
   - Identifies missing authorization controls on sensitive operations
   - Detects lack of audience restrictions
   - Flags tools accessing external resources without validation

### Enhanced Detection Features

- **Input Validation Tracking**: Analyzes methods for validation patterns (validate, sanitize, check, verify, whitelist) to reduce false positives
- **CWE Mapping**: Maps all findings to Common Weakness Enumeration IDs for industry-standard classification
- **Remediation Guidance**: Provides actionable code examples and OWASP documentation links
- **Secrets Patterns**: 13 patterns for detecting hardcoded credentials
- **Validation Patterns**: 7 patterns for identifying input validation
- **Logging Patterns**: 5 patterns for detecting audit logging

### Severity Levels

- **Critical**: Immediate security risk requiring urgent attention
- **High**: Significant security concern that should be addressed soon
- **Medium**: Potential security issue worth reviewing
- **Low**: Minor security consideration or best practice improvement

### Security Scan Output

When `--security` is used, the JSON output includes a comprehensive `SecurityAnalysis` section:

```json
{
  "SecurityAnalysis": {
    "TotalFindings": 12,
    "CriticalCount": 2,
    "HighCount": 5,
    "MediumCount": 3,
    "LowCount": 2,
    "Findings": [
      {
        "Category": "GeneralSecurity",
        "Severity": "Critical",
        "CWE": "CWE-312",
        "Title": "Hardcoded Credentials Detected",
        "Description": "Method contains hardcoded secrets or credentials",
        "Location": "AuthService.GetApiKey",
        "Recommendation": "Use secure secret management (Azure Key Vault, AWS Secrets Manager)",
        "Evidence": "Pattern 'api_key' detected",
        "CodeExample": "// Use environment variables or secure vaults\nvar apiKey = Environment.GetEnvironmentVariable(\"API_KEY\");",
        "DocumentationLink": "https://owasp.org/www-community/vulnerabilities/Use_of_hard-coded_credentials"
      },
      {
        "Category": "ToolPoisoning",
        "Severity": "High",
        "CWE": "CWE-494",
        "Title": "Potentially Dangerous Tool Operation",
        "Description": "Tool contains dangerous operation pattern",
        "Location": "FileTools.DeleteFile",
        "Recommendation": "Implement strict input validation, path sanitization, and authorization checks",
        "Evidence": "Pattern 'Delete' detected",
        "CodeExample": "// Validate and sanitize file paths\nvar safePath = Path.GetFullPath(userInput);\nif (!safePath.StartsWith(allowedDirectory)) throw new SecurityException();",
        "DocumentationLink": "https://owasp.org/www-community/attacks/Path_Traversal"
      }
    ]
  }
}
```

### SARIF 2.1.0 Export

Use `--format sarif` for industry-standard Static Analysis Results Interchange Format:

```bash
mcp-discover-dotnet ./bin/Release/net10.0 ./output --security --format sarif
```

This generates `security-report.sarif` compatible with:
- GitHub Code Scanning
- Azure DevOps Security Analysis
- SonarQube
- Other SAST tools

### CSV Export

Use `--format csv` for Excel-compatible tabular export:

```bash
mcp-discover-dotnet ./bin/Release/net10.0 ./output --security --format csv
```

Columns: Category, Severity, CWE, Title, Description, Location, Recommendation, Evidence

### Markdown Report

With `--markdown --security`, the report includes a detailed security section with findings grouped by severity and color-coded output.

## Configuration File

Create a JSON configuration file to customize security scanning behavior:

```json
{
  "MinimumSeverity": null,
  "ExcludeCategories": [],
  "Patterns": {
    "SecretsPatterns": [
      "api_key",
      "password",
      "custom_secret"
    ],
    "ValidationPatterns": [
      "validate",
      "sanitize"
    ],
    "LoggingPatterns": [
      "log",
      "audit"
    ]
  },
  "Thresholds": {
    "CriticalThreshold": 0,
    "HighThreshold": 5
  },
  "Suppressions": [
    {
      "Location": "TestClass.TestMethod",
      "Reason": "False positive - method is for testing only"
    }
  ],
  "IgnoreFiles": {
    "PromptInjection": [
      "**/Tests/**",
      "**/TestData/**"
    ],
    "GeneralSecurity": [
      "**/Mock*.dll"
    ]
  }
}
```

### Configuration Options

**MinimumSeverity** (null | "Low" | "Medium" | "High" | "Critical")
- Filter findings below this severity level
- `null` shows all findings

**ExcludeCategories** (array of strings)
- Skip entire vulnerability categories
- Valid values: "PromptInjection", "ToolPoisoning", "ToxicFlow", "GeneralSecurity"

**Patterns** (object)
- **SecretsPatterns**: Additional patterns for detecting hardcoded credentials
- **ValidationPatterns**: Patterns indicating input validation (reduces false positives)
- **LoggingPatterns**: Patterns for detecting audit logging

**Thresholds** (object)
- **CriticalThreshold**: Maximum critical findings before CI/CD failure (with `--fail-on-critical`)
- **HighThreshold**: Maximum high+ findings before CI/CD failure (with `--fail-on-high`)

**Suppressions** (array of objects)
- **Location**: Method or class to suppress (e.g., "MyClass.MyMethod")
- **Reason**: Documentation of why this finding is suppressed

**IgnoreFiles** (object)
- **Key**: Vulnerability category name
- **Value**: Array of glob patterns for files to ignore
- Supports wildcards: `**` (any directory), `*` (any characters)
- Examples:
  - `"**/Tests/**"` - Ignore all files in any Tests directory
  - `"**/Mock*.dll"` - Ignore DLLs starting with "Mock"
  - `"**/bin/Debug/**"` - Ignore debug builds

### Using Configuration Files

```bash
# Basic usage with config
mcp-discover-dotnet ./bin/Release/net10.0 ./output --security --config security-config.json

# Override config severity with command-line flag
mcp-discover-dotnet ./bin/Release/net10.0 ./output --security --config security-config.json --min-severity High

# Combine with other options
mcp-discover-dotnet ./bin/Release/net10.0 ./output --security --config security-config.json --format sarif --verbose
```

## CI/CD Integration

### Exit Codes

The tool uses exit codes for build automation:

- **Exit 0**: Success (no failures, or thresholds not exceeded)
- **Exit 1**: Failure (thresholds exceeded when using `--fail-on-*` flags)

### GitHub Actions Example

```yaml
name: MCP Security Scan

on: [push, pull_request]

jobs:
  security:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      
      - name: Install MCP Discovery Tool
        run: dotnet tool install --global mcp-discover-dotnet
      
      - name: Build Application
        run: dotnet build -c Release
      
      - name: Run Security Scan
        run: |
          mcp-discover-dotnet ./bin/Release/net10.0 ./security-report \
            --security \
            --format sarif \
            --fail-on-critical \
            --config security-config.json
      
      - name: Upload SARIF to GitHub Security
        uses: github/codeql-action/upload-sarif@v2
        if: always()
        with:
          sarif_file: security-report/security-report.sarif
```

### Azure DevOps Pipeline Example

```yaml
trigger:
  - main

pool:
  vmImage: 'ubuntu-latest'

steps:
- task: UseDotNet@2
  inputs:
    version: '10.0.x'

- script: dotnet tool install --global mcp-discover-dotnet
  displayName: 'Install MCP Discovery Tool'

- script: dotnet build -c Release
  displayName: 'Build Application'

- script: |
    mcp-discover-dotnet ./bin/Release/net10.0 ./security-report \
      --security \
      --format all \
      --fail-on-high \
      --verbose
  displayName: 'Security Scan'

- task: PublishBuildArtifacts@1
  condition: always()
  inputs:
    pathToPublish: 'security-report'
    artifactName: 'security-analysis'
```

### Threshold-Based Gating

```bash
# Fail build if ANY critical vulnerabilities found
mcp-discover-dotnet ./bin ./output --security --fail-on-critical

# Fail build if ANY high or critical vulnerabilities found  
mcp-discover-dotnet ./bin ./output --security --fail-on-high

# Use config file to set numeric thresholds
# security-config.json: { "Thresholds": { "CriticalThreshold": 0, "HighThreshold": 5 } }
mcp-discover-dotnet ./bin ./output --security --config security-config.json --fail-on-high
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
    <McpDiscoverTool>path\to\mcp-discover-dotnet.exe</McpDiscoverTool>
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
├── mcp-discover-dotnet/       # Discovery tool source code
│   ├── Program.cs             # Main scanning logic
│   └── mcp-discover-dotnet.csproj    # Tool project file
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

### MCP Development
- **Automated Capability Discovery**: Generate capability metadata during build
- **Documentation Generation**: Extract and document available tools/resources/prompts
- **Validation**: Verify that MCP attributes are correctly applied
- **Dynamic Discovery**: Discover capabilities in MCP server assemblies at runtime

### Security & Compliance
- **Vulnerability Detection**: Identify security risks in MCP servers before deployment
- **CI/CD Security Gating**: Block builds with critical vulnerabilities
- **SARIF Integration**: Feed findings into GitHub Code Scanning, Azure DevOps, SonarQube
- **Compliance Reporting**: Generate CSV/Excel reports for security audits
- **CWE Mapping**: Track vulnerabilities using industry-standard classification

### DevSecOps
- **Shift-Left Security**: Catch vulnerabilities early in development
- **Automated Scanning**: Integrate into build pipelines for continuous security analysis
- **False Positive Reduction**: Input validation tracking reduces noise
- **Secrets Detection**: Prevent hardcoded credentials from reaching production
- **Security Metrics**: Track Critical/High/Medium/Low findings over time

### Testing & Quality Assurance
- **Security Testing**: Validate MCP server implementations for security best practices
- **Regression Detection**: Track security findings across versions
- **Suppression Management**: Document and manage known false positives

## How It Works

### MCP Discovery
1. **Assembly Loading**: Uses isolated `AssemblyLoadContext` to safely load assemblies
2. **Reflection**: Scans types and methods for MCP attributes
3. **Metadata Extraction**: Reads attribute constructor arguments and properties
4. **Output Generation**: Serializes discovered metadata to structured formats

### Security Analysis
1. **Pattern Matching**: Applies 20+ heuristics across 4 vulnerability categories
2. **Validation Tracking**: Identifies methods with input validation to reduce false positives
3. **Secrets Detection**: Scans for 13 patterns of hardcoded credentials
4. **Audit Logging**: Detects missing security logging in sensitive operations
5. **CWE Mapping**: Maps findings to Common Weakness Enumeration IDs
6. **Remediation**: Provides code examples and OWASP documentation links
7. **Filtering**: Applies severity, category, and file-based filtering
8. **Export**: Generates JSON, SARIF 2.1.0, CSV, and Markdown reports
9. **CI/CD**: Returns exit codes based on configurable severity thresholds

## Requirements

- **.NET 10.0 SDK** or later
- **Windows, Linux, or macOS**
- Assemblies must be compiled .NET assemblies (.dll files)

## Contributing

Contributions are welcome! Areas for improvement:

### Features
- Support for additional metadata attributes
- More vulnerability detection heuristics
- Custom rule engine for security analysis
- Integration with additional SAST tools

### Performance
- Performance optimizations for large assembly sets
- Parallel processing for multi-assembly analysis
- Incremental scanning for unchanged files

### Export & Integration
- Additional output formats (XML, HTML reports)
- Integration examples for other CI/CD systems (GitLab CI, Jenkins)
- VS Code extension for inline security warnings
- NuGet package for programmatic API access

## Author

echapmanFromBunnings

## Related Resources

- [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) - Learn about the MCP standard
- [Test Server Documentation](./mcp-discover.tests/README.md) - Detailed example implementation
- [Verification Guide](./mcp-discover.tests/VERIFICATION.md) - Testing and validation steps

---

**Need Help?** Check the test server in `mcp-discover.tests/` for a complete working example!