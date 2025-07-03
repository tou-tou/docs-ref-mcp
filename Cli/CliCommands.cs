using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DocsRef.Cli;

public class CliCommands
{
    private readonly IServiceProvider _serviceProvider;
    private readonly JsonSerializerOptions _jsonOptions;

    public CliCommands(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _jsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public RootCommand CreateRootCommand()
    {
        var rootCommand = new RootCommand("DocsRef - Documentation Reference MCP Server and CLI");
        
        // Add subcommands
        rootCommand.AddCommand(CreateWebCommand());
        rootCommand.AddCommand(CreateDocsCommand());
        rootCommand.AddCommand(CreateServerCommand());
        
        return rootCommand;
    }

    private Command CreateWebCommand()
    {
        var webCommand = new Command("web", "Web page fetching and conversion operations");
        
        // fetch subcommand
        var fetchCommand = new Command("fetch", "Download and convert a web page to markdown");
        var urlArgument = new Argument<string>("url", "URL of the web page to download");
        var categoryOption = new Option<string?>("--category", "Category for organizing the document");
        var tagsOption = new Option<string?>("--tags", "Comma-separated tags for the document");
        var outputOption = new Option<OutputFormat>("--output", getDefaultValue: () => OutputFormat.Text, "Output format");
        
        fetchCommand.AddArgument(urlArgument);
        fetchCommand.AddOption(categoryOption);
        fetchCommand.AddOption(tagsOption);
        fetchCommand.AddOption(outputOption);
        fetchCommand.SetHandler(async (url, category, tags, output) =>
        {
            await ExecuteWebFetch(url, category, tags, output);
        }, urlArgument, categoryOption, tagsOption, outputOption);
        
        // fetch-batch subcommand
        var fetchBatchCommand = new Command("fetch-batch", "Download multiple web pages");
        var urlsArgument = new Argument<string>("urls", "Comma-separated list of URLs");
        var batchCategoryOption = new Option<string?>("--category", "Category for all documents");
        var batchOutputOption = new Option<OutputFormat>("--output", getDefaultValue: () => OutputFormat.Text, "Output format");
        
        fetchBatchCommand.AddArgument(urlsArgument);
        fetchBatchCommand.AddOption(batchCategoryOption);
        fetchBatchCommand.AddOption(batchOutputOption);
        fetchBatchCommand.SetHandler(async (urls, category, output) =>
        {
            await ExecuteWebFetchBatch(urls, category, output);
        }, urlsArgument, batchCategoryOption, batchOutputOption);
        
        // list subcommand
        var listCommand = new Command("list", "List downloaded web documents");
        var listCategoryOption = new Option<string?>("--category", "Filter by category");
        var listOutputOption = new Option<OutputFormat>("--output", getDefaultValue: () => OutputFormat.Text, "Output format");
        
        listCommand.AddOption(listCategoryOption);
        listCommand.AddOption(listOutputOption);
        listCommand.SetHandler(async (category, output) =>
        {
            await ExecuteWebList(category, output);
        }, listCategoryOption, listOutputOption);
        
        // suggest subcommand
        var suggestCommand = new Command("suggest", "Suggest refactoring and code smell resources");
        var suggestOutputOption = new Option<OutputFormat>("--output", getDefaultValue: () => OutputFormat.Text, "Output format");
        
        suggestCommand.AddOption(suggestOutputOption);
        suggestCommand.SetHandler(async (output) =>
        {
            await ExecuteWebSuggest(output);
        }, suggestOutputOption);
        
        webCommand.AddCommand(fetchCommand);
        webCommand.AddCommand(fetchBatchCommand);
        webCommand.AddCommand(listCommand);
        webCommand.AddCommand(suggestCommand);
        
        return webCommand;
    }

    private Command CreateDocsCommand()
    {
        var docsCommand = new Command("docs", "Document management operations");
        
        // list subcommand
        var listCommand = new Command("list", "List available documents");
        var patternOption = new Option<string?>("--pattern", "File path pattern (e.g., '*.cs')");
        var directoryOption = new Option<string?>("--directory", "Directory to search in");
        var maxOption = new Option<int>("--max", getDefaultValue: () => 100, "Maximum number of results");
        var listOutputOption = new Option<OutputFormat>("--output", getDefaultValue: () => OutputFormat.Text, "Output format");
        
        listCommand.AddOption(patternOption);
        listCommand.AddOption(directoryOption);
        listCommand.AddOption(maxOption);
        listCommand.AddOption(listOutputOption);
        listCommand.SetHandler(async (pattern, directory, max, output) =>
        {
            await ExecuteDocsList(pattern, directory, max, output);
        }, patternOption, directoryOption, maxOption, listOutputOption);
        
        // summary subcommand
        var summaryCommand = new Command("summary", "Show document repository summary");
        var summaryOutputOption = new Option<OutputFormat>("--output", getDefaultValue: () => OutputFormat.Text, "Output format");
        
        summaryCommand.AddOption(summaryOutputOption);
        summaryCommand.SetHandler(async (output) =>
        {
            await ExecuteDocsSummary(output);
        }, summaryOutputOption);
        
        // tree subcommand
        var treeCommand = new Command("tree", "Show directory tree");
        var treeDirectoryOption = new Option<string?>("--directory", "Root directory");
        var depthOption = new Option<int>("--depth", getDefaultValue: () => 3, "Maximum depth");
        var treeOutputOption = new Option<OutputFormat>("--output", getDefaultValue: () => OutputFormat.Text, "Output format");
        
        treeCommand.AddOption(treeDirectoryOption);
        treeCommand.AddOption(depthOption);
        treeCommand.AddOption(treeOutputOption);
        treeCommand.SetHandler(async (directory, depth, output) =>
        {
            await ExecuteDocsTree(directory, depth, output);
        }, treeDirectoryOption, depthOption, treeOutputOption);
        
        // get subcommand
        var getCommand = new Command("get", "Get document content");
        var pathArgument = new Argument<string>("path", "Document path");
        var pageOption = new Option<int?>("--page", "Page number for large files");
        var getOutputOption = new Option<OutputFormat>("--output", getDefaultValue: () => OutputFormat.Text, "Output format");
        
        getCommand.AddArgument(pathArgument);
        getCommand.AddOption(pageOption);
        getCommand.AddOption(getOutputOption);
        getCommand.SetHandler(async (path, page, output) =>
        {
            await ExecuteDocsGet(path, page, output);
        }, pathArgument, pageOption, getOutputOption);
        
        // grep subcommand
        var grepCommand = new Command("grep", "Search documents with regex");
        var grepPatternArgument = new Argument<string>("pattern", "Regular expression pattern");
        var caseSensitiveOption = new Option<bool>("--case-sensitive", "Case sensitive search");
        var grepOutputOption = new Option<OutputFormat>("--output", getDefaultValue: () => OutputFormat.Text, "Output format");
        
        grepCommand.AddArgument(grepPatternArgument);
        grepCommand.AddOption(caseSensitiveOption);
        grepCommand.AddOption(grepOutputOption);
        grepCommand.SetHandler(async (pattern, caseSensitive, output) =>
        {
            await ExecuteDocsGrep(pattern, !caseSensitive, output);
        }, grepPatternArgument, caseSensitiveOption, grepOutputOption);
        
        docsCommand.AddCommand(listCommand);
        docsCommand.AddCommand(summaryCommand);
        docsCommand.AddCommand(treeCommand);
        docsCommand.AddCommand(getCommand);
        docsCommand.AddCommand(grepCommand);
        
        return docsCommand;
    }

    private Command CreateServerCommand()
    {
        var serverCommand = new Command("server", "Run the MCP server");
        var portOption = new Option<int?>("--port", "Server port (default: 7334)");
        
        serverCommand.AddOption(portOption);
        serverCommand.SetHandler(async (port) =>
        {
            if (port.HasValue)
            {
                Environment.SetEnvironmentVariable("MCP_PORT", port.Value.ToString());
            }
            await RunMcpServer();
        }, portOption);
        
        return serverCommand;
    }

    // Web command handlers
    private async Task ExecuteWebFetch(string url, string? category, string? tags, OutputFormat output)
    {
        var webCommands = _serviceProvider.GetRequiredService<WebCommands>();
        await webCommands.FetchWebPage(url, category, tags, output);
    }

    private async Task ExecuteWebFetchBatch(string urls, string? category, OutputFormat output)
    {
        var webCommands = _serviceProvider.GetRequiredService<WebCommands>();
        await webCommands.FetchWebPagesBatch(urls, category, output);
    }

    private async Task ExecuteWebList(string? category, OutputFormat output)
    {
        var webCommands = _serviceProvider.GetRequiredService<WebCommands>();
        await webCommands.ListWebDocs(category, output);
    }

    private async Task ExecuteWebSuggest(OutputFormat output)
    {
        var webCommands = _serviceProvider.GetRequiredService<WebCommands>();
        await webCommands.SuggestResources(output);
    }

    // Docs command handlers
    private async Task ExecuteDocsList(string? pattern, string? directory, int max, OutputFormat output)
    {
        var docCommands = _serviceProvider.GetRequiredService<DocCommands>();
        await docCommands.ListDocs(pattern, directory, max, output);
    }

    private async Task ExecuteDocsSummary(OutputFormat output)
    {
        var docCommands = _serviceProvider.GetRequiredService<DocCommands>();
        await docCommands.Summary(output);
    }

    private async Task ExecuteDocsTree(string? directory, int depth, OutputFormat output)
    {
        var docCommands = _serviceProvider.GetRequiredService<DocCommands>();
        await docCommands.Tree(directory, depth, output);
    }

    private async Task ExecuteDocsGet(string path, int? page, OutputFormat output)
    {
        var docCommands = _serviceProvider.GetRequiredService<DocCommands>();
        await docCommands.GetDoc(path, page, output);
    }

    private async Task ExecuteDocsGrep(string pattern, bool ignoreCase, OutputFormat output)
    {
        var docCommands = _serviceProvider.GetRequiredService<DocCommands>();
        await docCommands.Grep(pattern, ignoreCase, output);
    }

    private async Task RunMcpServer()
    {
        var logger = _serviceProvider.GetRequiredService<ILogger<CliCommands>>();
        logger.LogInformation("Starting MCP server mode...");
        
        // This will be handled by Program.cs
        throw new InvalidOperationException("Server mode should be handled by Program.cs");
    }
}

public enum OutputFormat
{
    Text,
    Json
}