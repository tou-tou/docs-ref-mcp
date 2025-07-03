using System.Text.Json;
using DocsRef.Core;
using DocsRef.Core.Models;
using DocsRef.Tools;
using Microsoft.Extensions.Logging;

namespace DocsRef.Cli;

public class WebCommands
{
    private readonly WebTools _webTools;
    private readonly ILogger<WebCommands> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public WebCommands(WebTools webTools, ILogger<WebCommands> logger)
    {
        _webTools = webTools;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task FetchWebPage(string url, string? category, string? tags, OutputFormat output)
    {
        try
        {
            var result = await _webTools.FetchWebPage(url, category, tags);
            
            if (output == OutputFormat.Json)
            {
                var jsonResult = new
                {
                    success = !result.StartsWith("Error:"),
                    message = result,
                    url = url,
                    category = category,
                    tags = tags?.Split(',').Select(t => t.Trim()).ToArray()
                };
                Console.WriteLine(JsonSerializer.Serialize(jsonResult, _jsonOptions));
            }
            else
            {
                Console.WriteLine(result);
            }
            
            Environment.Exit(result.StartsWith("Error:") ? 1 : 0);
        }
        catch (Exception ex)
        {
            HandleError(ex, output);
        }
    }

    public async Task FetchWebPagesBatch(string urls, string? category, OutputFormat output)
    {
        try
        {
            var result = await _webTools.FetchWebPagesBatch(urls, category);
            
            if (output == OutputFormat.Json)
            {
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var summary = lines.FirstOrDefault() ?? "";
                var results = lines.Skip(2) // Skip summary and "Results:" line
                    .Select(line =>
                    {
                        var isSuccess = line.StartsWith("✅");
                        var content = line.Substring(2).Trim();
                        return new { success = isSuccess, message = content };
                    })
                    .ToArray();
                
                var jsonResult = new
                {
                    summary = summary,
                    results = results,
                    urls = urls.Split(',').Select(u => u.Trim()).ToArray(),
                    category = category
                };
                Console.WriteLine(JsonSerializer.Serialize(jsonResult, _jsonOptions));
            }
            else
            {
                Console.WriteLine(result);
            }
            
            Environment.Exit(result.Contains("Error:") ? 1 : 0);
        }
        catch (Exception ex)
        {
            HandleError(ex, output);
        }
    }

    public async Task ListWebDocs(string? category, OutputFormat output)
    {
        try
        {
            var result = await _webTools.ListWebDocs(category);
            
            if (output == OutputFormat.Json)
            {
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var files = lines
                    .Where(line => line.StartsWith("- "))
                    .Select(line => line.Substring(2).Trim())
                    .ToArray();
                
                var jsonResult = new
                {
                    count = files.Length,
                    category = category,
                    files = files
                };
                Console.WriteLine(JsonSerializer.Serialize(jsonResult, _jsonOptions));
            }
            else
            {
                Console.WriteLine(result);
            }
            
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            HandleError(ex, output);
        }
    }

    public async Task SuggestResources(OutputFormat output)
    {
        try
        {
            var result = await _webTools.SuggestRefactoringResources();
            
            if (output == OutputFormat.Json)
            {
                var resources = new List<object>();
                var lines = result.Split('\n');
                
                string? currentTitle = null;
                string? currentUrl = null;
                string? currentCategory = null;
                
                foreach (var line in lines)
                {
                    if (line.StartsWith("**") && line.EndsWith("**"))
                    {
                        if (currentTitle != null && currentUrl != null)
                        {
                            resources.Add(new
                            {
                                title = currentTitle,
                                url = currentUrl,
                                category = currentCategory
                            });
                        }
                        currentTitle = line.Trim('*').Trim();
                    }
                    else if (line.Contains("URL:"))
                    {
                        currentUrl = line.Split("URL:")[1].Trim();
                    }
                    else if (line.Contains("Category:"))
                    {
                        currentCategory = line.Split("Category:")[1].Trim();
                    }
                }
                
                // Add the last resource
                if (currentTitle != null && currentUrl != null)
                {
                    resources.Add(new
                    {
                        title = currentTitle,
                        url = currentUrl,
                        category = currentCategory
                    });
                }
                
                var jsonResult = new
                {
                    resources = resources
                };
                Console.WriteLine(JsonSerializer.Serialize(jsonResult, _jsonOptions));
            }
            else
            {
                Console.WriteLine(result);
            }
            
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            HandleError(ex, output);
        }
    }

    private void HandleError(Exception ex, OutputFormat output)
    {
        _logger.LogError(ex, "CLI command failed");
        
        if (output == OutputFormat.Json)
        {
            var errorResult = new
            {
                success = false,
                error = ex.Message,
                type = ex.GetType().Name
            };
            Console.WriteLine(JsonSerializer.Serialize(errorResult, _jsonOptions));
        }
        else
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
        }
        
        Environment.Exit(1);
    }
}