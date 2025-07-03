using System.CommandLine;
using DocsRef.Cli;
using DocsRef.Core;
using DocsRef.Server;
using DocsRef.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Check if running in CLI mode
var isCliMode = args.Length > 0 && !args.Contains("server");

// Create service collection
var services = new ServiceCollection();

// Configure logging
services.AddLogging(builder =>
{
    builder.ClearProviders();
    
    // For CLI mode, minimize logging unless explicitly set
    if (isCliMode)
    {
        var logLevelStr = Environment.GetEnvironmentVariable("LOGGING__LOGLEVEL__DEFAULT") ?? "Warning";
        if (Enum.TryParse<LogLevel>(logLevelStr, true, out var cliLogLevel))
        {
            builder.SetMinimumLevel(cliLogLevel);
        }
        builder.AddFilter("Microsoft", LogLevel.Warning);
        builder.AddFilter("System", LogLevel.Warning);
    }
    else
    {
        builder.AddConsole();
        var logLevelStr = Environment.GetEnvironmentVariable("LOGGING__LOGLEVEL__DEFAULT") ?? "Information";
        if (Enum.TryParse<LogLevel>(logLevelStr, true, out var logLevel))
        {
            builder.SetMinimumLevel(logLevel);
        }
    }
});

// Configure web services
services.AddSingleton<WebPageDownloader>();
services.AddSingleton<HtmlToMarkdownConverter>();
services.AddSingleton<WebTools>();

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

// Register CLI services
services.AddSingleton<CliCommands>();
services.AddSingleton<WebCommands>();
services.AddSingleton<DocCommands>();

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

// Initialize web tools to ensure directory creation
var webTools = serviceProvider.GetRequiredService<WebTools>();

// Check if we should run in CLI mode
if (args.Length > 0)
{
    // CLI mode
    var cliCommands = serviceProvider.GetRequiredService<CliCommands>();
    var rootCommand = cliCommands.CreateRootCommand();
    
    // Special handling for 'server' command
    if (args.Length == 1 && args[0] == "server")
    {
        await RunMcpServer(serviceProvider, logger, null);
    }
    else if (args.Length >= 2 && args[0] == "server" && args[1] == "--port")
    {
        if (args.Length >= 3 && int.TryParse(args[2], out var cliPort))
        {
            await RunMcpServer(serviceProvider, logger, cliPort);
        }
        else
        {
            Console.Error.WriteLine("Invalid port number");
            Environment.Exit(1);
        }
    }
    else
    {
        // Run CLI command
        await rootCommand.InvokeAsync(args);
    }
}
else
{
    // Default: Run MCP server
    await RunMcpServer(serviceProvider, logger, null);
}

async Task RunMcpServer(IServiceProvider sp, ILogger logger, int? overridePort)
{
    using var mcpServer = sp.GetRequiredService<StreamableMcpServerApplication>();
    
    // Configuration constants
    const int DEFAULT_PORT = 7334; // REF (Reference) - documentation reference server
    
    // Get port from override, environment variable, or use default
    var port = overridePort ?? 
               (int.TryParse(Environment.GetEnvironmentVariable("MCP_PORT"), out var envPort) 
                   ? envPort 
                   : DEFAULT_PORT);
    
    // Run the server
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (sender, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };
    
    try
    {
        logger.LogInformation($"Starting MCP server on port {port}...");
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
}