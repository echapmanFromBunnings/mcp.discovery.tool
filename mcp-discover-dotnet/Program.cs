using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Serialization;

return McpDiscoveryApp.Run(args);

internal static class McpDiscoveryApp
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Dictionary<string, string> ClassAttributeKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["McpServerToolType"] = "ToolType",
        ["McpServerResourceType"] = "ResourceType",
        ["McpServerPromptType"] = "PromptType"
    };

    private static readonly Dictionary<string, string> MethodAttributeKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["McpServerTool"] = "Tool",
        ["McpServerResource"] = "Resource",
        ["McpServerPrompt"] = "Prompt"
    };

    public static int Run(string[] args)
    {
        if (ShouldShowHelp(args))
        {
            PrintBanner();
            PrintUsage();
            return 0;
        }

        var generateMarkdown = args.Any(a => 
            string.Equals(a, "-m", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "--markdown", StringComparison.OrdinalIgnoreCase));

        var omitBasePath = args.Any(a => 
            string.Equals(a, "-o", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "--omit-path", StringComparison.OrdinalIgnoreCase));

        var noTimestamp = args.Any(a => 
            string.Equals(a, "-n", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "--no-timestamp", StringComparison.OrdinalIgnoreCase));

        var performSecurityScan = args.Any(a => 
            string.Equals(a, "-s", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "--security", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a, "--security-scan", StringComparison.OrdinalIgnoreCase));

        var filteredArgs = args.Where(a => 
            !string.Equals(a, "-m", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(a, "--markdown", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(a, "-o", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(a, "--omit-path", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(a, "-n", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(a, "--no-timestamp", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(a, "-s", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(a, "--security", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(a, "--security-scan", StringComparison.OrdinalIgnoreCase)).ToArray();

        if (filteredArgs.Length < 2)
        {
            PrintBanner();
            Console.Error.WriteLine();
            Console.Error.WriteLine("❌ Error: input and output directories are required.");
            Console.Error.WriteLine();
            PrintUsage();
            return 1;
        }

        PrintBanner();

        var inputDir = Path.GetFullPath(filteredArgs[0]);
        var outputDir = Path.GetFullPath(filteredArgs[1]);

        Console.WriteLine();
        Console.WriteLine($"📂 Input:  {inputDir}");
        Console.WriteLine($"📁 Output: {outputDir}");
        Console.WriteLine();

        if (!Directory.Exists(inputDir))
        {
            Console.Error.WriteLine($"❌ Error: input directory '{inputDir}' was not found.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("💡 Tip: Make sure the path exists and try using an absolute path.");
            return 1;
        }

        Directory.CreateDirectory(outputDir);

        var assemblyPaths = Directory.EnumerateFiles(inputDir, "*.dll", SearchOption.AllDirectories).ToList();
        if (assemblyPaths.Count == 0)
        {
            Console.WriteLine($"⚠️  Warning: no assemblies found under '{inputDir}'.");
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine($"🔍 Found {assemblyPaths.Count} assemblies to scan...");
            Console.WriteLine();
        }

        var result = new McpDiscoveryResult
        {
            GeneratedAtUtc = noTimestamp ? null : DateTimeOffset.UtcNow,
            Assemblies = new List<McpAssemblyMetadata>()
        };

        var totalClasses = 0;
        var totalMembers = 0;

        foreach (var assemblyPath in assemblyPaths)
        {
            Console.Write($"   Scanning {Path.GetFileName(assemblyPath)}... ");
            var assemblyMetadata = ScanAssembly(assemblyPath, inputDir, omitBasePath);
            if (assemblyMetadata != null && assemblyMetadata.Classes.Count > 0)
            {
                result.Assemblies.Add(assemblyMetadata);
                var classCount = assemblyMetadata.Classes.Count;
                var memberCount = assemblyMetadata.Classes.Sum(c => c.Members.Count);
                totalClasses += classCount;
                totalMembers += memberCount;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"✓ ({classCount} classes, {memberCount} members)");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("(no MCP metadata)");
                Console.ResetColor();
            }
        }

        Console.WriteLine();

        // Perform security analysis if requested
        if (performSecurityScan)
        {
            Console.WriteLine("🔒 Running security vulnerability scan...");
            Console.WriteLine();
            result.SecurityAnalysis = SecurityAnalyzer.AnalyzeMetadata(result);
            
            var criticalCount = result.SecurityAnalysis.Findings.Count(f => f.Severity == SecuritySeverity.Critical);
            var highCount = result.SecurityAnalysis.Findings.Count(f => f.Severity == SecuritySeverity.High);
            var mediumCount = result.SecurityAnalysis.Findings.Count(f => f.Severity == SecuritySeverity.Medium);
            var lowCount = result.SecurityAnalysis.Findings.Count(f => f.Severity == SecuritySeverity.Low);
            
            Console.ForegroundColor = criticalCount > 0 ? ConsoleColor.Red : ConsoleColor.Green;
            Console.WriteLine($"   • Critical: {criticalCount}");
            Console.ResetColor();
            Console.ForegroundColor = highCount > 0 ? ConsoleColor.Yellow : ConsoleColor.Green;
            Console.WriteLine($"   • High: {highCount}");
            Console.ResetColor();
            Console.ForegroundColor = mediumCount > 0 ? ConsoleColor.DarkYellow : ConsoleColor.Green;
            Console.WriteLine($"   • Medium: {mediumCount}");
            Console.ResetColor();
            Console.WriteLine($"   • Low: {lowCount}");
            Console.WriteLine();
        }

        var outputPath = Path.Combine(outputDir, "mcp-metadata.json");
        var json = JsonSerializer.Serialize(result, JsonOptions);
        File.WriteAllText(outputPath, json);
        
        // Generate markdown report if requested
        if (generateMarkdown)
        {
            var markdownPath = Path.Combine(outputDir, "mcp-metadata.md");
            var markdown = GenerateMarkdownReport(result, performSecurityScan);
            File.WriteAllText(markdownPath, markdown);
        }
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✅ Success!");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"📊 Summary:");
        Console.WriteLine($"   • Assemblies scanned: {assemblyPaths.Count}");
        Console.WriteLine($"   • Assemblies with MCP metadata: {result.Assemblies.Count}");
        Console.WriteLine($"   • Total classes discovered: {totalClasses}");
        Console.WriteLine($"   • Total members discovered: {totalMembers}");
        Console.WriteLine();
        Console.WriteLine($"📄 Metadata written to:");
        Console.WriteLine($"   {outputPath}");
        if (generateMarkdown)
        {
            var markdownPath = Path.Combine(outputDir, "mcp-metadata.md");
            Console.WriteLine($"   {markdownPath}");
        }
        Console.WriteLine();
        
        return 0;
    }

    private static McpAssemblyMetadata? ScanAssembly(string assemblyPath, string basePath, bool omitBasePath)
    {
        var context = new IsolatedAssemblyLoadContext(assemblyPath);
        try
        {
            var assembly = context.LoadFromAssemblyPath(assemblyPath);
            var classes = new List<McpClassMetadata>();
            foreach (var type in GetLoadableTypes(assembly))
            {
                var classAttr = type.CustomAttributes.FirstOrDefault(a =>
                    ClassAttributeKinds.ContainsKey(GetAttributeKey(a)));
                if (classAttr == null)
                {
                    continue;
                }

                var classKind = ClassAttributeKinds[GetAttributeKey(classAttr)];
                var members = FindMcpMembers(type);
                if (members.Count == 0)
                {
                    continue;
                }

                classes.Add(new McpClassMetadata
                {
                    TypeName = type.FullName ?? type.Name,
                    Kind = classKind,
                    Description = ExtractDescription(type.CustomAttributes),
                    Audiences = ExtractAudiences(type.CustomAttributes),
                    Members = members
                });
            }

            if (classes.Count == 0)
            {
                return null;
            }

            var outputPath = assemblyPath;
            if (omitBasePath)
            {
                var relativePath = Path.GetRelativePath(basePath, assemblyPath);
                outputPath = relativePath.StartsWith("..") ? Path.GetFileName(assemblyPath) : relativePath;
            }

            return new McpAssemblyMetadata
            {
                AssemblyPath = outputPath,
                Classes = classes
            };
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠️  ({ex.GetType().Name})");
            Console.ResetColor();
            return null;
        }
        finally
        {
            context.Unload();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    private static List<McpMemberMetadata> FindMcpMembers(Type type)
    {
        var members = new List<McpMemberMetadata>();
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                      BindingFlags.Static | BindingFlags.DeclaredOnly);
        foreach (var method in methods)
        {
            var methodAttr = method.CustomAttributes.FirstOrDefault(a =>
                MethodAttributeKinds.ContainsKey(GetAttributeKey(a)));
            if (methodAttr == null)
            {
                continue;
            }

            var (name, title) = ExtractNameAndTitle(methodAttr);
            members.Add(new McpMemberMetadata
            {
                MethodName = method.Name,
                Kind = MethodAttributeKinds[GetAttributeKey(methodAttr)],
                Name = name,
                Title = title,
                Description = ExtractDescription(method.CustomAttributes),
                Audiences = ExtractAudiences(method.CustomAttributes)
            });
        }

        return members;
    }

    private static (string? Name, string? Title) ExtractNameAndTitle(CustomAttributeData attribute)
    {
        var name = TryGetNamedString(attribute, "Name") ?? TryGetConstructorString(attribute, 0);
        var title = TryGetNamedString(attribute, "Title") ?? TryGetConstructorString(attribute, 1);
        return (Normalize(name), Normalize(title));
    }

    private static string? ExtractDescription(IEnumerable<CustomAttributeData> attributes)
    {
        foreach (var attribute in attributes)
        {
            var key = GetAttributeKey(attribute);
            if (!string.Equals(key, "Description", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var constructorValue = attribute.ConstructorArguments.FirstOrDefault();
            if (constructorValue.ArgumentType == typeof(string) && constructorValue.Value is string description)
            {
                return Normalize(description);
            }

            foreach (var named in attribute.NamedArguments)
            {
                if (string.Equals(named.MemberName, "Description", StringComparison.OrdinalIgnoreCase) &&
                    named.TypedValue.ArgumentType == typeof(string) && named.TypedValue.Value is string namedDescription)
                {
                    return Normalize(namedDescription);
                }
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ExtractAudiences(IEnumerable<CustomAttributeData> attributes)
    {
        var attribute = attributes.FirstOrDefault(a =>
            string.Equals(GetAttributeKey(a), "McpAudience", StringComparison.OrdinalIgnoreCase));
        if (attribute == null)
        {
            return Array.Empty<string>();
        }

        var audiences = new List<string>();
        foreach (var argument in attribute.ConstructorArguments)
        {
            AddAudience(argument, audiences);
        }

        foreach (var named in attribute.NamedArguments)
        {
            AddAudience(named.TypedValue, audiences);
        }

        return audiences
            .Select(Normalize)
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddAudience(CustomAttributeTypedArgument argument, IList<string> audiences)
    {
        if (argument.ArgumentType == typeof(string) && argument.Value is string value)
        {
            audiences.Add(value);
            return;
        }

        if (argument.ArgumentType.IsArray && argument.Value is IEnumerable<CustomAttributeTypedArgument> nested)
        {
            foreach (var child in nested)
            {
                AddAudience(child, audiences);
            }
        }
    }

    private static string? TryGetNamedString(CustomAttributeData attribute, string propertyName)
    {
        foreach (var named in attribute.NamedArguments)
        {
            if (string.Equals(named.MemberName, propertyName, StringComparison.OrdinalIgnoreCase) &&
                named.TypedValue.ArgumentType == typeof(string) && named.TypedValue.Value is string value)
            {
                return value;
            }
        }

        return null;
    }

    private static string? TryGetConstructorString(CustomAttributeData attribute, int index)
    {
        if (index >= attribute.ConstructorArguments.Count)
        {
            return null;
        }

        var argument = attribute.ConstructorArguments[index];
        return argument.ArgumentType == typeof(string) ? argument.Value as string : null;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool ShouldShowHelp(IEnumerable<string> args)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string GenerateMarkdownReport(McpDiscoveryResult result, bool includeSecurityAnalysis)
    {
        var sb = new System.Text.StringBuilder();
        
        // Header
        sb.AppendLine("# MCP Discovery Report");
        sb.AppendLine();
        
        if (result.GeneratedAtUtc.HasValue)
        {
            sb.AppendLine($"**Generated:** {result.GeneratedAtUtc.Value:yyyy-MM-dd HH:mm:ss UTC}");
            sb.AppendLine();
        }
        
        // Summary
        var totalClasses = result.Assemblies.Sum(a => a.Classes.Count);
        var totalMembers = result.Assemblies.Sum(a => a.Classes.Sum(c => c.Members.Count));
        var toolCount = result.Assemblies.Sum(a => a.Classes.Where(c => c.Kind == "ToolType").Sum(c => c.Members.Count));
        var resourceCount = result.Assemblies.Sum(a => a.Classes.Where(c => c.Kind == "ResourceType").Sum(c => c.Members.Count));
        var promptCount = result.Assemblies.Sum(a => a.Classes.Where(c => c.Kind == "PromptType").Sum(c => c.Members.Count));
        
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Assemblies Scanned:** {result.Assemblies.Count}");
        sb.AppendLine($"- **Total Classes:** {totalClasses}");
        sb.AppendLine($"- **Total Capabilities:** {totalMembers}");
        sb.AppendLine($"  - Tools: {toolCount}");
        sb.AppendLine($"  - Resources: {resourceCount}");
        sb.AppendLine($"  - Prompts: {promptCount}");
        sb.AppendLine();
        
        // Group by capability kind
        var toolTypes = result.Assemblies.SelectMany(a => a.Classes).Where(c => c.Kind == "ToolType").ToList();
        var resourceTypes = result.Assemblies.SelectMany(a => a.Classes).Where(c => c.Kind == "ResourceType").ToList();
        var promptTypes = result.Assemblies.SelectMany(a => a.Classes).Where(c => c.Kind == "PromptType").ToList();
        
        // Tools section
        if (toolTypes.Any())
        {
            sb.AppendLine("## 🔧 Tools");
            sb.AppendLine();
            
            foreach (var toolType in toolTypes)
            {
                sb.AppendLine($"### {toolType.TypeName}");
                sb.AppendLine();
                
                if (!string.IsNullOrEmpty(toolType.Description))
                {
                    sb.AppendLine($"*{toolType.Description}*");
                    sb.AppendLine();
                }
                
                if (toolType.Audiences.Any())
                {
                    sb.AppendLine($"**Audiences:** {string.Join(", ", toolType.Audiences)}");
                    sb.AppendLine();
                }
                
                if (toolType.Members.Any())
                {
                    sb.AppendLine("| Name | Title | Description | Audiences |");
                    sb.AppendLine("|------|-------|-------------|-----------|");
                    
                    foreach (var member in toolType.Members)
                    {
                        var name = EscapeMarkdown(member.Name ?? member.MethodName);
                        var title = EscapeMarkdown(member.Title ?? "");
                        var description = EscapeMarkdown(member.Description ?? "");
                        var audiences = string.Join(", ", member.Audiences);
                        
                        sb.AppendLine($"| `{name}` | {title} | {description} | {audiences} |");
                    }
                    
                    sb.AppendLine();
                }
            }
        }
        
        // Resources section
        if (resourceTypes.Any())
        {
            sb.AppendLine("## 📚 Resources");
            sb.AppendLine();
            
            foreach (var resourceType in resourceTypes)
            {
                sb.AppendLine($"### {resourceType.TypeName}");
                sb.AppendLine();
                
                if (!string.IsNullOrEmpty(resourceType.Description))
                {
                    sb.AppendLine($"*{resourceType.Description}*");
                    sb.AppendLine();
                }
                
                if (resourceType.Audiences.Any())
                {
                    sb.AppendLine($"**Audiences:** {string.Join(", ", resourceType.Audiences)}");
                    sb.AppendLine();
                }
                
                if (resourceType.Members.Any())
                {
                    sb.AppendLine("| Name | Title | Description | Audiences |");
                    sb.AppendLine("|------|-------|-------------|-----------|");
                    
                    foreach (var member in resourceType.Members)
                    {
                        var name = EscapeMarkdown(member.Name ?? member.MethodName);
                        var title = EscapeMarkdown(member.Title ?? "");
                        var description = EscapeMarkdown(member.Description ?? "");
                        var audiences = string.Join(", ", member.Audiences);
                        
                        sb.AppendLine($"| `{name}` | {title} | {description} | {audiences} |");
                    }
                    
                    sb.AppendLine();
                }
            }
        }
        
        // Prompts section
        if (promptTypes.Any())
        {
            sb.AppendLine("## 💬 Prompts");
            sb.AppendLine();
            
            foreach (var promptType in promptTypes)
            {
                sb.AppendLine($"### {promptType.TypeName}");
                sb.AppendLine();
                
                if (!string.IsNullOrEmpty(promptType.Description))
                {
                    sb.AppendLine($"*{promptType.Description}*");
                    sb.AppendLine();
                }
                
                if (promptType.Audiences.Any())
                {
                    sb.AppendLine($"**Audiences:** {string.Join(", ", promptType.Audiences)}");
                    sb.AppendLine();
                }
                
                if (promptType.Members.Any())
                {
                    sb.AppendLine("| Name | Title | Description | Audiences |");
                    sb.AppendLine("|------|-------|-------------|-----------|");
                    
                    foreach (var member in promptType.Members)
                    {
                        var name = EscapeMarkdown(member.Name ?? member.MethodName);
                        var title = EscapeMarkdown(member.Title ?? "");
                        var description = EscapeMarkdown(member.Description ?? "");
                        var audiences = string.Join(", ", member.Audiences);
                        
                        sb.AppendLine($"| `{name}` | {title} | {description} | {audiences} |");
                    }
                    
                    sb.AppendLine();
                }
            }
        }
        
        // Assembly details
        sb.AppendLine("## 📦 Assembly Details");
        sb.AppendLine();
        
        foreach (var assembly in result.Assemblies)
        {
            sb.AppendLine($"### {Path.GetFileName(assembly.AssemblyPath)}");
            sb.AppendLine();
            sb.AppendLine($"**Path:** `{assembly.AssemblyPath}`");
            sb.AppendLine();
            sb.AppendLine($"**Classes:** {assembly.Classes.Count}");
            sb.AppendLine();
            
            foreach (var classGroup in assembly.Classes.GroupBy(c => c.Kind))
            {
                sb.AppendLine($"- {classGroup.Key}: {classGroup.Count()} ({classGroup.Sum(c => c.Members.Count)} members)");
            }
            
            sb.AppendLine();
        }
        
        // Security analysis section
        if (includeSecurityAnalysis && result.SecurityAnalysis != null)
        {
            sb.AppendLine("## 🔒 Security Analysis");
            sb.AppendLine();
            
            var analysis = result.SecurityAnalysis;
            
            sb.AppendLine("### Summary");
            sb.AppendLine();
            sb.AppendLine($"- **Total Findings:** {analysis.TotalFindings}");
            sb.AppendLine($"- **Critical:** {analysis.CriticalCount}");
            sb.AppendLine($"- **High:** {analysis.HighCount}");
            sb.AppendLine($"- **Medium:** {analysis.MediumCount}");
            sb.AppendLine($"- **Low:** {analysis.LowCount}");
            sb.AppendLine();
            
            if (analysis.Findings.Any())
            {
                // Group by severity
                var criticalFindings = analysis.Findings.Where(f => f.Severity == SecuritySeverity.Critical).ToList();
                var highFindings = analysis.Findings.Where(f => f.Severity == SecuritySeverity.High).ToList();
                var mediumFindings = analysis.Findings.Where(f => f.Severity == SecuritySeverity.Medium).ToList();
                var lowFindings = analysis.Findings.Where(f => f.Severity == SecuritySeverity.Low).ToList();
                
                if (criticalFindings.Any())
                {
                    sb.AppendLine("### 🚨 Critical Severity");
                    sb.AppendLine();
                    AppendSecurityFindings(sb, criticalFindings);
                }
                
                if (highFindings.Any())
                {
                    sb.AppendLine("### ⚠️ High Severity");
                    sb.AppendLine();
                    AppendSecurityFindings(sb, highFindings);
                }
                
                if (mediumFindings.Any())
                {
                    sb.AppendLine("### ⚡ Medium Severity");
                    sb.AppendLine();
                    AppendSecurityFindings(sb, mediumFindings);
                }
                
                if (lowFindings.Any())
                {
                    sb.AppendLine("### ℹ️ Low Severity");
                    sb.AppendLine();
                    AppendSecurityFindings(sb, lowFindings);
                }
            }
            else
            {
                sb.AppendLine("✅ No security vulnerabilities detected.");
                sb.AppendLine();
            }
        }
        
        return sb.ToString();
    }

    private static void AppendSecurityFindings(System.Text.StringBuilder sb, List<SecurityFinding> findings)
    {
        foreach (var finding in findings)
        {
            var categoryIcon = finding.Category switch
            {
                VulnerabilityCategory.PromptInjection => "💉",
                VulnerabilityCategory.ToolPoisoning => "☠️",
                VulnerabilityCategory.ToxicFlow => "⚡",
                VulnerabilityCategory.GeneralSecurity => "🛡️",
                _ => "⚠️"
            };
            
            sb.AppendLine($"#### {categoryIcon} {finding.Title}");
            sb.AppendLine();
            sb.AppendLine($"**Location:** `{finding.Location}`");
            sb.AppendLine();
            sb.AppendLine($"**Description:** {finding.Description}");
            sb.AppendLine();
            sb.AppendLine($"**Recommendation:** {finding.Recommendation}");
            sb.AppendLine();
            
            if (!string.IsNullOrEmpty(finding.Evidence))
            {
                sb.AppendLine($"**Evidence:** {finding.Evidence}");
                sb.AppendLine();
            }
            
            sb.AppendLine("---");
            sb.AppendLine();
        }
    }

    private static string EscapeMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        
        return text
            .Replace("|", "\\|")
            .Replace("\n", " ")
            .Replace("\r", "");
    }

    private static string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                     ?? assembly.GetName().Version?.ToString()
                     ?? "0.0.0";
        
        // Remove git commit hash if present (e.g., "1.0.0+abc123" -> "1.0.0")
        var plusIndex = version.IndexOf('+');
        if (plusIndex > 0)
        {
            version = version.Substring(0, plusIndex);
        }
        
        return version;
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
   _____ _____ _____     ____  _                             
  |     |     |  _  |___|    \|_|___ ___ ___ _ _ ___ ___ _ _ 
  | | | |   --|   __|___|  |  | |_ -|  _| . | | | -_|  _| | |
  |_|_|_|_____|__|      |____/|_|___|___|___|\_/|___|_| |_  |
                                                         |___|");
        Console.ResetColor();
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"  Model Context Protocol Discovery Tool v{GetVersion()}");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  mcp-discover-dotnet <input-directory> <output-directory>");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("Description:");
        Console.WriteLine("  Scans .NET assemblies for MCP (Model Context Protocol) metadata");
        Console.WriteLine("  and generates a structured JSON file with discovered capabilities.");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  <input-directory>   Directory containing .dll assemblies to scan");
        Console.WriteLine("  <output-directory>  Directory where mcp-metadata.json will be written");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -h, --help         Show this help information");
        Console.WriteLine("  -m, --markdown     Generate markdown report alongside JSON");
        Console.WriteLine("  -o, --omit-path    Omit base path from assembly paths in output");
        Console.WriteLine("  -n, --no-timestamp Omit timestamp from output (for version control)");
        Console.WriteLine("  -s, --security     Perform security vulnerability analysis");
        Console.WriteLine();

        Console.WriteLine("Examples:");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  # Scan build output");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  mcp-discover-dotnet ./bin/Release/net10.0 ./metadata");
        Console.ResetColor();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  # Scan current directory");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  mcp-discover-dotnet . ./output");
        Console.ResetColor();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  # Generate markdown report");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  mcp-discover-dotnet ./bin/Release/net10.0 ./metadata --markdown");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("What it discovers:");
        Console.WriteLine("  • Tools      - Methods decorated with [McpServerTool]");
        Console.WriteLine("  • Resources  - Methods decorated with [McpServerResource]");
        Console.WriteLine("  • Prompts    - Methods decorated with [McpServerPrompt]");
        Console.WriteLine();
        Console.WriteLine("For more information:");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("  https://github.com/echapmanFromBunnings/mcp.discovery.tool");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }

    private static string GetAttributeKey(CustomAttributeData attribute)
    {
        var name = attribute.AttributeType.Name;
        return name.EndsWith("Attribute", StringComparison.Ordinal) ? name[..^9] : name;
    }
}

internal sealed class IsolatedAssemblyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public IsolatedAssemblyLoadContext(string mainAssemblyPath)
        : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path != null ? LoadFromAssemblyPath(path) : null;
    }
}

internal sealed class McpDiscoveryResult
{
    public DateTimeOffset? GeneratedAtUtc { get; set; }
    public List<McpAssemblyMetadata> Assemblies { get; set; } = new();
    public SecurityAnalysisResult? SecurityAnalysis { get; set; }
}

internal sealed class McpAssemblyMetadata
{
    public string AssemblyPath { get; set; } = string.Empty;
    public List<McpClassMetadata> Classes { get; set; } = new();
}

internal sealed class McpClassMetadata
{
    public string TypeName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? Description { get; set; }
    public IReadOnlyList<string> Audiences { get; set; } = Array.Empty<string>();
    public List<McpMemberMetadata> Members { get; set; } = new();
}

internal sealed class McpMemberMetadata
{
    public string MethodName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public IReadOnlyList<string> Audiences { get; set; } = Array.Empty<string>();
}

internal enum SecuritySeverity
{
    Low,
    Medium,
    High,
    Critical
}

internal enum VulnerabilityCategory
{
    PromptInjection,
    ToolPoisoning,
    ToxicFlow,
    GeneralSecurity
}

internal sealed class SecurityFinding
{
    public VulnerabilityCategory Category { get; set; }
    public SecuritySeverity Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string? Evidence { get; set; }
}

internal sealed class SecurityAnalysisResult
{
    public int TotalFindings { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
    public List<SecurityFinding> Findings { get; set; } = new();
}

internal static class SecurityAnalyzer
{
    private static readonly string[] DangerousMethodPatterns = 
    {
        "Execute", "Exec", "Run", "Invoke", "Call", "System", "Command", "Shell",
        "Delete", "Remove", "Drop", "Truncate", "Destroy", "Kill", "Terminate"
    };

    private static readonly string[] FileSystemPatterns = 
    {
        "File", "Directory", "Path", "Read", "Write", "Create", "Open", "Save", "Load"
    };

    private static readonly string[] DatabasePatterns = 
    {
        "Query", "Sql", "Database", "Db", "Execute", "Insert", "Update", "Delete", "Select"
    };

    private static readonly string[] UnsafeInputIndicators = 
    {
        "string", "object", "dynamic", "unvalidated", "raw", "user", "input"
    };

    public static SecurityAnalysisResult AnalyzeMetadata(McpDiscoveryResult result)
    {
        var findings = new List<SecurityFinding>();

        foreach (var assembly in result.Assemblies)
        {
            foreach (var classMetadata in assembly.Classes)
            {
                // Analyze each member for vulnerabilities
                foreach (var member in classMetadata.Members)
                {
                    AnalyzePromptInjection(member, classMetadata, assembly, findings);
                    AnalyzeToolPoisoning(member, classMetadata, assembly, findings);
                    AnalyzeToxicFlow(member, classMetadata, assembly, findings);
                    AnalyzeGeneralSecurity(member, classMetadata, assembly, findings);
                }

                // Analyze class-level issues
                AnalyzeClassSecurity(classMetadata, assembly, findings);
            }
        }

        return new SecurityAnalysisResult
        {
            Findings = findings,
            TotalFindings = findings.Count,
            CriticalCount = findings.Count(f => f.Severity == SecuritySeverity.Critical),
            HighCount = findings.Count(f => f.Severity == SecuritySeverity.High),
            MediumCount = findings.Count(f => f.Severity == SecuritySeverity.Medium),
            LowCount = findings.Count(f => f.Severity == SecuritySeverity.Low)
        };
    }

    private static void AnalyzePromptInjection(McpMemberMetadata member, McpClassMetadata classMetadata, 
        McpAssemblyMetadata assembly, List<SecurityFinding> findings)
    {
        if (member.Kind != "Prompt")
            return;

        // Check for dynamic content indicators in description
        var description = member.Description ?? "";
        var title = member.Title ?? "";
        var name = member.Name ?? "";

        var hasUserInput = description.Contains("user", StringComparison.OrdinalIgnoreCase) ||
                          description.Contains("input", StringComparison.OrdinalIgnoreCase) ||
                          description.Contains("parameter", StringComparison.OrdinalIgnoreCase);

        var hasConcatenation = description.Contains("concat", StringComparison.OrdinalIgnoreCase) ||
                              description.Contains("+", StringComparison.OrdinalIgnoreCase) ||
                              description.Contains("format", StringComparison.OrdinalIgnoreCase);

        if (hasUserInput && hasConcatenation)
        {
            findings.Add(new SecurityFinding
            {
                Category = VulnerabilityCategory.PromptInjection,
                Severity = SecuritySeverity.High,
                Title = "Potential Prompt Injection Risk",
                Description = "Prompt appears to accept user input and may concatenate it directly without sanitization.",
                Location = $"{classMetadata.TypeName}.{member.MethodName}",
                Recommendation = "Implement input sanitization and validation. Use templating with proper escaping. Consider using allowlists for expected input patterns.",
                Evidence = $"Description contains user input indicators: '{description}'"
            });
        }

        // Check for missing input validation
        if (string.IsNullOrEmpty(description) && member.Kind == "Prompt")
        {
            findings.Add(new SecurityFinding
            {
                Category = VulnerabilityCategory.PromptInjection,
                Severity = SecuritySeverity.Medium,
                Title = "Missing Prompt Documentation",
                Description = "Prompt lacks description which may indicate missing input validation considerations.",
                Location = $"{classMetadata.TypeName}.{member.MethodName}",
                Recommendation = "Document the prompt's expected inputs and validate them appropriately.",
                Evidence = "No description provided"
            });
        }
    }

    private static void AnalyzeToolPoisoning(McpMemberMetadata member, McpClassMetadata classMetadata,
        McpAssemblyMetadata assembly, List<SecurityFinding> findings)
    {
        if (member.Kind != "Tool")
            return;

        var methodName = member.MethodName.ToLowerInvariant();
        var description = (member.Description ?? "").ToLowerInvariant();
        var name = (member.Name ?? "").ToLowerInvariant();

        // Check for dangerous operations
        foreach (var pattern in DangerousMethodPatterns)
        {
            if (methodName.Contains(pattern.ToLowerInvariant()) || 
                description.Contains(pattern.ToLowerInvariant()) ||
                name.Contains(pattern.ToLowerInvariant()))
            {
                findings.Add(new SecurityFinding
                {
                    Category = VulnerabilityCategory.ToolPoisoning,
                    Severity = SecuritySeverity.Critical,
                    Title = "Potentially Dangerous Tool Operation",
                    Description = $"Tool contains dangerous operation pattern: '{pattern}'",
                    Location = $"{classMetadata.TypeName}.{member.MethodName}",
                    Recommendation = "Ensure strict input validation, implement allowlists, add authorization checks, and audit all usage.",
                    Evidence = $"Pattern '{pattern}' detected in tool name/description"
                });
                break;
            }
        }

        // Check for file system operations
        foreach (var pattern in FileSystemPatterns)
        {
            if (methodName.Contains(pattern.ToLowerInvariant()) || 
                description.Contains(pattern.ToLowerInvariant()))
            {
                findings.Add(new SecurityFinding
                {
                    Category = VulnerabilityCategory.ToolPoisoning,
                    Severity = SecuritySeverity.High,
                    Title = "File System Access Detected",
                    Description = $"Tool performs file system operations which could be exploited for path traversal attacks.",
                    Location = $"{classMetadata.TypeName}.{member.MethodName}",
                    Recommendation = "Validate all file paths against an allowlist. Use Path.GetFullPath() and ensure paths are within expected directories. Implement strict path sanitization.",
                    Evidence = $"File system pattern '{pattern}' detected"
                });
                break;
            }
        }

        // Check for database operations
        foreach (var pattern in DatabasePatterns)
        {
            if (methodName.Contains(pattern.ToLowerInvariant()) || 
                description.Contains(pattern.ToLowerInvariant()))
            {
                findings.Add(new SecurityFinding
                {
                    Category = VulnerabilityCategory.ToolPoisoning,
                    Severity = SecuritySeverity.High,
                    Title = "Database Operation Detected",
                    Description = "Tool performs database operations which could be vulnerable to SQL injection.",
                    Location = $"{classMetadata.TypeName}.{member.MethodName}",
                    Recommendation = "Always use parameterized queries or an ORM. Never concatenate user input into SQL statements. Implement least-privilege database access.",
                    Evidence = $"Database pattern '{pattern}' detected"
                });
                break;
            }
        }
    }

    private static void AnalyzeToxicFlow(McpMemberMetadata member, McpClassMetadata classMetadata,
        McpAssemblyMetadata assembly, List<SecurityFinding> findings)
    {
        // Check for async operations without timeout hints
        if (member.MethodName.Contains("Async", StringComparison.OrdinalIgnoreCase) ||
            member.Description?.Contains("async", StringComparison.OrdinalIgnoreCase) == true)
        {
            var hasTimeoutMention = member.Description?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true ||
                                   member.Description?.Contains("delay", StringComparison.OrdinalIgnoreCase) == true;

            if (!hasTimeoutMention)
            {
                findings.Add(new SecurityFinding
                {
                    Category = VulnerabilityCategory.ToxicFlow,
                    Severity = SecuritySeverity.Medium,
                    Title = "Async Operation Without Timeout",
                    Description = "Async operation detected without timeout configuration which could lead to resource exhaustion.",
                    Location = $"{classMetadata.TypeName}.{member.MethodName}",
                    Recommendation = "Implement cancellation tokens and timeout limits. Consider using Task.WaitAsync() with timeout. Document expected execution time.",
                    Evidence = "Async operation without timeout documentation"
                });
            }
        }

        // Check for expensive operations without rate limiting hints
        var expensivePatterns = new[] { "process", "generate", "compute", "calculate", "analyze", "fetch", "download" };
        var methodNameLower = member.MethodName.ToLowerInvariant();
        var descriptionLower = (member.Description ?? "").ToLowerInvariant();

        foreach (var pattern in expensivePatterns)
        {
            if (methodNameLower.Contains(pattern) || descriptionLower.Contains(pattern))
            {
                var hasRateLimitMention = descriptionLower.Contains("rate") ||
                                         descriptionLower.Contains("limit") ||
                                         descriptionLower.Contains("throttle");

                if (!hasRateLimitMention && member.Kind == "Tool")
                {
                    findings.Add(new SecurityFinding
                    {
                        Category = VulnerabilityCategory.ToxicFlow,
                        Severity = SecuritySeverity.Medium,
                        Title = "Potentially Expensive Operation",
                        Description = $"Tool performs potentially expensive operation '{pattern}' without rate limiting.",
                        Location = $"{classMetadata.TypeName}.{member.MethodName}",
                        Recommendation = "Implement rate limiting, request throttling, or queue-based processing. Set maximum execution time limits.",
                        Evidence = $"Expensive operation pattern '{pattern}' detected without rate limiting"
                    });
                    break;
                }
            }
        }
    }

    private static void AnalyzeGeneralSecurity(McpMemberMetadata member, McpClassMetadata classMetadata,
        McpAssemblyMetadata assembly, List<SecurityFinding> findings)
    {
        // Check for missing audience restrictions on sensitive operations
        if ((member.Audiences == null || member.Audiences.Count == 0) && member.Kind == "Tool")
        {
            var methodNameLower = member.MethodName.ToLowerInvariant();
            var isSensitive = DangerousMethodPatterns.Any(p => methodNameLower.Contains(p.ToLowerInvariant())) ||
                            FileSystemPatterns.Any(p => methodNameLower.Contains(p.ToLowerInvariant())) ||
                            DatabasePatterns.Any(p => methodNameLower.Contains(p.ToLowerInvariant()));

            if (isSensitive)
            {
                findings.Add(new SecurityFinding
                {
                    Category = VulnerabilityCategory.GeneralSecurity,
                    Severity = SecuritySeverity.High,
                    Title = "Missing Authorization Controls",
                    Description = "Sensitive tool operation lacks audience restrictions for access control.",
                    Location = $"{classMetadata.TypeName}.{member.MethodName}",
                    Recommendation = "Define appropriate audiences using [McpAudience] attribute to restrict access. Implement role-based access control.",
                    Evidence = "No audience restrictions defined on sensitive operation"
                });
            }
        }

        // Check for external API calls
        if (member.Description?.Contains("api", StringComparison.OrdinalIgnoreCase) == true ||
            member.Description?.Contains("http", StringComparison.OrdinalIgnoreCase) == true ||
            member.Description?.Contains("url", StringComparison.OrdinalIgnoreCase) == true)
        {
            findings.Add(new SecurityFinding
            {
                Category = VulnerabilityCategory.GeneralSecurity,
                Severity = SecuritySeverity.Medium,
                Title = "External API Call Detected",
                Description = "Tool makes external API calls which could be vulnerable to SSRF attacks.",
                Location = $"{classMetadata.TypeName}.{member.MethodName}",
                Recommendation = "Validate all URLs against an allowlist. Use HTTPS only. Implement request signing. Avoid accepting arbitrary URLs from user input.",
                Evidence = "External API/URL pattern detected in description"
            });
        }
    }

    private static void AnalyzeClassSecurity(McpClassMetadata classMetadata, McpAssemblyMetadata assembly,
        List<SecurityFinding> findings)
    {
        // Check for classes with many tools but no audience restrictions
        if (classMetadata.Kind == "ToolType" && 
            (classMetadata.Audiences == null || classMetadata.Audiences.Count == 0) &&
            classMetadata.Members.Count > 3)
        {
            findings.Add(new SecurityFinding
            {
                Category = VulnerabilityCategory.GeneralSecurity,
                Severity = SecuritySeverity.Medium,
                Title = "Tool Class Without Access Control",
                Description = $"Tool class contains {classMetadata.Members.Count} tools but lacks audience-based access control.",
                Location = classMetadata.TypeName,
                Recommendation = "Add [McpAudience] attribute to the class to define who can access these tools.",
                Evidence = $"Class has {classMetadata.Members.Count} tools with no audience restrictions"
            });
        }
    }
}
