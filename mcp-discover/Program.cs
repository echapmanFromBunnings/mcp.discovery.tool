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
            PrintUsage();
            return 0;
        }

        if (args.Length < 2)
        {
            Console.Error.WriteLine("error: input and output directories are required.");
            PrintUsage();
            return 1;
        }

        var inputDir = Path.GetFullPath(args[0]);
        var outputDir = Path.GetFullPath(args[1]);

        if (!Directory.Exists(inputDir))
        {
            Console.Error.WriteLine($"error: input directory '{inputDir}' was not found.");
            return 1;
        }

        Directory.CreateDirectory(outputDir);

        var assemblyPaths = Directory.EnumerateFiles(inputDir, "*.dll", SearchOption.AllDirectories).ToList();
        if (assemblyPaths.Count == 0)
        {
            Console.WriteLine($"warning: no assemblies found under '{inputDir}'.");
        }

        var result = new McpDiscoveryResult
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Assemblies = new List<McpAssemblyMetadata>()
        };

        foreach (var assemblyPath in assemblyPaths)
        {
            Console.WriteLine($"Scanning {assemblyPath}...");
            var assemblyMetadata = ScanAssembly(assemblyPath);
            if (assemblyMetadata != null && assemblyMetadata.Classes.Count > 0)
            {
                result.Assemblies.Add(assemblyMetadata);
            }
        }

        var outputPath = Path.Combine(outputDir, "mcp-metadata.json");
        var json = JsonSerializer.Serialize(result, JsonOptions);
        File.WriteAllText(outputPath, json);
        Console.WriteLine($"Metadata written to {outputPath}.");
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
            Console.Error.WriteLine($"warning: failed to scan '{assemblyPath}': {ex.Message}");
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

    private static void PrintUsage()
    {
        Console.WriteLine("mcp-discover <input-directory> <output-directory>");
        Console.WriteLine("Scans assemblies in the input directory for MCP metadata and emits JSON output.");
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
