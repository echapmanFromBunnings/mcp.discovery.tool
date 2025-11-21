using System;

namespace Mcp.TestServer;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class McpServerToolTypeAttribute : Attribute
{
    public McpServerToolTypeAttribute(string name, string? title = null) { }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class McpServerResourceTypeAttribute : Attribute
{
    public McpServerResourceTypeAttribute(string name, string? title = null) { }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class McpServerPromptTypeAttribute : Attribute
{
    public McpServerPromptTypeAttribute(string name, string? title = null) { }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class McpServerToolAttribute : Attribute
{
    public McpServerToolAttribute(string name, string? title = null) { }
    public string? Description { get; set; }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class McpServerResourceAttribute : Attribute
{
    public McpServerResourceAttribute(string name, string? title = null) { }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class McpServerPromptAttribute : Attribute
{
    public McpServerPromptAttribute(string name, string? title = null) { }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class DescriptionAttribute : Attribute
{
    public DescriptionAttribute(string description) { }
    public string Description { get; init; } = null!;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class McpAudienceAttribute : Attribute
{
    public McpAudienceAttribute(params string[] audiences) { }
    public string[] Audiences { get; init; } = Array.Empty<string>();
}

