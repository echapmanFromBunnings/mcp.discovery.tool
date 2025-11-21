namespace Mcp.TestServer;

/// <summary>
/// Basic MCP Test Server - demonstrates a minimal MCP server implementation
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("MCP Test Server Started");
        Console.WriteLine("Server Info: v1.0.0");
        Console.WriteLine("Available capabilities: tools, resources, prompts");
        
        // Keep server running
        await Task.Delay(-1);
        return 0;
    }
}

/// <summary>
/// Calculator tool set - basic arithmetic operations
/// </summary>
[McpServerToolType("calculator", "Calculator Tools")]
[Description("Basic arithmetic operations")]
[McpAudience("developers", "qa")]
public static class CalculatorTools
{
    [McpServerTool("add", "Add Numbers")]
    [Description("Adds two integers and returns the sum")]
    [McpAudience("developers")]
    public static int Add(int a, int b)
    {
        Console.WriteLine($"[Tool:add] Called with a={a}, b={b}");
        return a + b;
    }

    [McpServerTool("subtract", "Subtract Numbers")]
    [Description("Subtracts second integer from first")]
    [McpAudience("developers")]
    public static int Subtract(int a, int b)
    {
        Console.WriteLine($"[Tool:subtract] Called with a={a}, b={b}");
        return a - b;
    }

    [McpServerTool("multiply", "Multiply Numbers")]
    [Description("Multiplies two integers")]
    [McpAudience("developers")]
    public static int Multiply(int a, int b)
    {
        Console.WriteLine($"[Tool:multiply] Called with a={a}, b={b}");
        return a * b;
    }

    [McpServerTool("slow", "Slow Async Tool")]
    [Description("Simulates an async operation with delay")]
    [McpAudience("ops")]
    public static async Task<int> SlowAsync(int delayMs)
    {
        Console.WriteLine($"[Tool:slow] Waiting for {delayMs}ms");
        await Task.Delay(delayMs);
        Console.WriteLine($"[Tool:slow] Completed");
        return delayMs;
    }
}

/// <summary>
/// Documentation resources - provides static documentation content
/// </summary>
[McpServerResourceType("doc", "Documentation")]
[Description("Static documentation and help resources")]
[McpAudience("docs", "developers")]
public sealed class DocumentationResources
{
    [McpServerResource("readme", "README Document")]
    public string GetReadme()
    {
        Console.WriteLine("[Resource:readme] Accessed");
        return "# MCP Test Server\n\nThis is a basic test server for MCP protocol demonstration.";
    }

    [McpServerResource("version", "Version Info")]
    public string GetVersion()
    {
        Console.WriteLine("[Resource:version] Accessed");
        return "Version: 1.0.0\nProtocol: MCP\nCapabilities: tools, resources, prompts";
    }
}

/// <summary>
/// Prompt catalog - provides AI prompt templates
/// </summary>
[McpServerPromptType("ideas", "Idea Prompts")]
[Description("Creative and brainstorming prompts")]
[McpAudience("ideation", "creative")]
public sealed class PromptCatalog
{
    [McpServerPrompt("brainstorm", "Brainstorm Ideas")]
    public string GetBrainstormPrompt()
    {
        Console.WriteLine("[Prompt:brainstorm] Accessed");
        return "Generate three creative ideas for the given topic. Consider unique angles and practical applications.";
    }

    [McpServerPrompt("analyze", "Analyze Code")]
    public string GetAnalyzePrompt()
    {
        Console.WriteLine("[Prompt:analyze] Accessed");
        return "Analyze the provided code for potential improvements, bugs, and best practices.";
    }
}
