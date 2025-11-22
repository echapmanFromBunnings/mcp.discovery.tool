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

        var filteredArgs = args.Where(a => 
            !string.Equals(a, "-m", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(a, "--markdown", StringComparison.OrdinalIgnoreCase)).ToArray();

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
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Assemblies = new List<McpAssemblyMetadata>()
        };

        var totalClasses = 0;
        var totalMembers = 0;

        foreach (var assemblyPath in assemblyPaths)
        {
            Console.Write($"   Scanning {Path.GetFileName(assemblyPath)}... ");
            var assemblyMetadata = ScanAssembly(assemblyPath);
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

        var outputPath = Path.Combine(outputDir, "mcp-metadata.json");
        var json = JsonSerializer.Serialize(result, JsonOptions);
        File.WriteAllText(outputPath, json);
        
        // Generate markdown report if requested
        if (generateMarkdown)
        {
            var markdownPath = Path.Combine(outputDir, "mcp-metadata.md");
            var markdown = GenerateMarkdownReport(result);
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

    private static McpAssemblyMetadata? ScanAssembly(string assemblyPath)
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

            return classes.Count == 0
                ? null
                : new McpAssemblyMetadata
                {
                    AssemblyPath = assemblyPath,
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

    private static string GenerateMarkdownReport(McpDiscoveryResult result)
    {
        var sb = new System.Text.StringBuilder();
        
        // Header
        sb.AppendLine("# MCP Discovery Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {result.GeneratedAtUtc:yyyy-MM-dd HH:mm:ss UTC}");
        sb.AppendLine();
        
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
        
        return sb.ToString();
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
        Console.WriteLine("  Model Context Protocol Discovery Tool v1.0.0");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  mcp-discover <input-directory> <output-directory>");
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
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  # Scan build output");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  mcp-discover ./bin/Release/net10.0 ./metadata");
        Console.ResetColor();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  # Scan current directory");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  mcp-discover . ./output");
        Console.ResetColor();
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  # Generate markdown report");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("  mcp-discover ./bin/Release/net10.0 ./metadata --markdown");
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
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public List<McpAssemblyMetadata> Assemblies { get; set; } = new();
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
