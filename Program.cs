using DocsMcpServer.Core;
using DocsMcpServer.Server;
using DocsMcpServer.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Create service collection
var services = new ServiceCollection();

// Configure logging
services.AddLogging(builder =>
{
    builder.ClearProviders();
    builder.AddConsole();
    
    // Check for log level environment variable
    var logLevelEnv = Environment.GetEnvironmentVariable("LOGGING__LOGLEVEL__DEFAULT") ?? 
                      Environment.GetEnvironmentVariable("Logging__LogLevel__Default");
    
    if (Enum.TryParse<LogLevel>(logLevelEnv, true, out var logLevel))
    {
        builder.SetMinimumLevel(logLevel);
    }
    else
    {
        builder.SetMinimumLevel(LogLevel.Information);
    }
});

// Configure DocumentManager
services.AddSingleton<DocumentManager>(sp =>
{
    // Parse allowed folders from environment variable
    var docsFoldersEnv = Environment.GetEnvironmentVariable("DOCS_FOLDERS")?.Trim();
    List<string>? allowedFolders = null;
    
    if (!string.IsNullOrEmpty(docsFoldersEnv))
    {
        allowedFolders = docsFoldersEnv
            .Split(',')
            .Select(f => f.Trim())
            .Where(f => !string.IsNullOrEmpty(f))
            .ToList();
        
        var logger = sp.GetRequiredService<ILogger<Program>>();
        logger.LogInformation($"Loading documents from folders: {string.Join(", ", allowedFolders)}");
    }
    else
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Loading all documents (no DOCS_FOLDERS specified)");
    }
    
    return new DocumentManager(allowedFolders);
});

// Register the MCP server application
services.AddSingleton<StreamableMcpServerApplication>();

// Build service provider
var serviceProvider = services.BuildServiceProvider();

// Initialize DocumentManager
var documentManager = serviceProvider.GetRequiredService<DocumentManager>();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Loading documents...");
documentManager.LoadDocuments();
logger.LogInformation($"Loaded {documentManager.GetDocumentCount()} documents");

// Initialize static tools
DocTools.Initialize(documentManager);

// Create and run MCP server
using var mcpServer = serviceProvider.GetRequiredService<StreamableMcpServerApplication>();

// Get port from environment variable or use default
var port = 7334; // REF (Reference) port - indicates documentation reference server
var portEnv = Environment.GetEnvironmentVariable("MCP_PORT");
if (!string.IsNullOrEmpty(portEnv) && int.TryParse(portEnv, out var envPort))
{
    port = envPort;
}

// Run the server
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await mcpServer.RunAsync("127.0.0.1", port, cts.Token);
}
catch (OperationCanceledException)
{
    logger.LogInformation("MCP server shutdown requested");
}
catch (Exception ex)
{
    logger.LogError(ex, "Fatal error running MCP server");
    Environment.Exit(1);
}