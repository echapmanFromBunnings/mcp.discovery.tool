namespace Mcp.TestServer;

/// <summary>
/// Security test server - demonstrates various security vulnerabilities
/// </summary>
[McpServerToolType("security-test", "Security Test Tools")]
[Description("Tools with intentional security vulnerabilities for testing")]
public static class SecurityTestTools
{
    [McpServerTool("execute-command", "Execute Shell Command")]
    [Description("Executes arbitrary shell commands")]
    public static string ExecuteCommand(string command)
    {
        // Critical: Command execution without validation
        return "Command executed";
    }

    [McpServerTool("read-file", "Read File")]
    [Description("Reads file content from user-specified path")]
    public static string ReadFile(string filePath)
    {
        // High: File system access without path validation
        return "File content";
    }

    [McpServerTool("sql-query", "Execute SQL Query")]
    [Description("Executes SQL query with user input")]
    public static string ExecuteQuery(string query)
    {
        // High: Database operation without parameterization
        return "Query results";
    }

    [McpServerTool("api-call", "Call External API")]
    [Description("Makes HTTP request to user-specified URL")]
    public static async Task<string> CallApi(string url)
    {
        // Medium: External API call without URL validation (SSRF risk)
        return "API response";
    }

    [McpServerTool("process-data", "Process Large Data")]
    [Description("Processes large dataset asynchronously")]
    public static async Task<string> ProcessData(string data)
    {
        // Medium: Async without timeout, expensive operation without rate limiting
        return "Processed";
    }
}

[McpServerPromptType("security-prompts", "Security Test Prompts")]
[Description("Prompts with potential injection vulnerabilities")]
public sealed class SecurityTestPrompts
{
    [McpServerPrompt("user-prompt", "Dynamic User Prompt")]
    [Description("Generates prompt by concatenating user input")]
    public string GetUserPrompt(string userInput)
    {
        // High: Prompt injection risk - direct string concatenation
        return $"Process this user input: {userInput}";
    }

    [McpServerPrompt("formatted-prompt", "Formatted Prompt")]
    [Description("Uses string.Format with user parameter")]
    public string GetFormattedPrompt(string param)
    {
        // High: Prompt injection via format strings
        return string.Format("Analyze: {0}", param);
    }
}

[McpServerResourceType("admin-resources", "Admin Resources")]
[Description("Administrative resources without access control")]
// Note: No audience restrictions!
public sealed class AdminResources
{
    [McpServerResource("delete-all", "Delete All Data")]
    [Description("Deletes all system data")]
    // Critical: Sensitive operation without audience restriction
    public string DeleteAll()
    {
        return "All data deleted";
    }

    [McpServerResource("config", "System Configuration")]
    [Description("Returns sensitive configuration data")]
    public string GetConfig()
    {
        return "Sensitive config";
    }
}
