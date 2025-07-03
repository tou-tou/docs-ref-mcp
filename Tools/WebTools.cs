using System.ComponentModel;
using System.Text;
using DocsRef.Core;
using DocsRef.Core.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace DocsRef.Tools;

[McpServerToolType, Description("Web page fetching and conversion tools")]
public class WebTools
{
    private readonly ILogger<WebTools> _logger;
    private readonly WebPageDownloader _downloader;
    private readonly HtmlToMarkdownConverter _converter;
    private readonly string _webCacheDir;
    private readonly string _docsBaseDir;

    public WebTools(ILogger<WebTools> logger, WebPageDownloader downloader, HtmlToMarkdownConverter converter)
    {
        _logger = logger;
        _downloader = downloader;
        _converter = converter;
        
        _docsBaseDir = Environment.GetEnvironmentVariable("DOCS_BASE_DIR") ?? Directory.GetCurrentDirectory();
        _webCacheDir = Environment.GetEnvironmentVariable("WEB_CACHE_DIR") ?? Path.Combine(_docsBaseDir, "docs", "web");
        
        if (!Directory.Exists(_webCacheDir))
        {
            Directory.CreateDirectory(_webCacheDir);
            _logger.LogInformation("Created web cache directory: {Dir}", _webCacheDir);
        }
    }

    [McpServerTool, Description("Download a web page and convert it to markdown for reference")]
    public async ValueTask<string> FetchWebPage(
        [Description("The URL of the web page to download")]
        string url,
        [Description("Optional category for organizing the document (e.g., 'refactoring', 'code-smells', 'design-patterns')")]
        string? category = null,
        [Description("Optional tags for the document, comma-separated")]
        string? tags = null)
    {
        try
        {
            _logger.LogInformation("Fetching web page: {Url}", url);
            
            var (success, html, error) = await _downloader.DownloadPageAsync(url);
            if (!success)
            {
                return $"Error: Failed to download page: {error}";
            }
            
            var (convertSuccess, markdown, convertError) = _converter.ConvertHtmlToMarkdown(html, url);
            if (!convertSuccess)
            {
                return $"Error: Failed to convert to markdown: {convertError}";
            }
            
            var webDoc = WebDocument.FromMarkdown(url, markdown);
            webDoc.Category = category;
            if (!string.IsNullOrEmpty(tags))
            {
                webDoc.Tags = tags.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList();
            }
            
            var categoryDir = string.IsNullOrEmpty(category) 
                ? _webCacheDir 
                : Path.Combine(_webCacheDir, category.ToLowerInvariant().Replace(' ', '-'));
            
            if (!Directory.Exists(categoryDir))
            {
                Directory.CreateDirectory(categoryDir);
            }
            
            var fileName = webDoc.GetSafeFileName();
            var filePath = Path.Combine(categoryDir, fileName);
            
            await File.WriteAllTextAsync(filePath, markdown);
            webDoc.LocalPath = Path.GetRelativePath(_docsBaseDir, filePath);
            
            _logger.LogInformation("Saved web document to: {Path}", webDoc.LocalPath);
            
            var response = new StringBuilder();
            response.AppendLine($"Successfully downloaded and converted: {webDoc.Title}");
            response.AppendLine($"URL: {url}");
            response.AppendLine($"Saved to: {webDoc.LocalPath}");
            response.AppendLine($"Size: {markdown.Length:N0} characters");
            
            if (!string.IsNullOrEmpty(category))
                response.AppendLine($"Category: {category}");
            
            if (webDoc.Tags.Any())
                response.AppendLine($"Tags: {string.Join(", ", webDoc.Tags)}");
            
            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching web page: {Url}", url);
            return $"Error: Unexpected error: {ex.Message}";
        }
    }

    [McpServerTool, Description("Download multiple web pages and convert them to markdown")]
    public async ValueTask<string> FetchWebPagesBatch(
        [Description("Comma-separated list of URLs to download")]
        string urls,
        [Description("Optional category for all documents")]
        string? category = null)
    {
        try
        {
            var urlList = urls.Split(',')
                .Select(u => u.Trim())
                .Where(u => !string.IsNullOrEmpty(u))
                .Distinct()
                .ToList();
            
            if (!urlList.Any())
            {
                return "Error: No valid URLs provided";
            }
            
            _logger.LogInformation("Fetching {Count} web pages", urlList.Count);
            
            var downloadResults = await _downloader.DownloadPagesAsync(urlList);
            
            var htmlToConvert = downloadResults
                .Where(kvp => kvp.Value.success)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.content);
            
            var conversionResults = await _converter.ConvertMultipleAsync(htmlToConvert);
            
            var categoryDir = string.IsNullOrEmpty(category) 
                ? _webCacheDir 
                : Path.Combine(_webCacheDir, category.ToLowerInvariant().Replace(' ', '-'));
            
            if (!Directory.Exists(categoryDir))
            {
                Directory.CreateDirectory(categoryDir);
            }
            
            var results = new List<string>();
            var successCount = 0;
            var failureCount = 0;
            
            foreach (var url in urlList)
            {
                if (!downloadResults[url].success)
                {
                    results.Add($"❌ {url}: Download failed - {downloadResults[url].error}");
                    failureCount++;
                    continue;
                }
                
                if (!conversionResults.ContainsKey(url) || !conversionResults[url].success)
                {
                    var error = conversionResults.ContainsKey(url) ? conversionResults[url].error : "Unknown error";
                    results.Add($"❌ {url}: Conversion failed - {error}");
                    failureCount++;
                    continue;
                }
                
                try
                {
                    var markdown = conversionResults[url].markdown;
                    var webDoc = WebDocument.FromMarkdown(url, markdown);
                    webDoc.Category = category;
                    
                    var fileName = webDoc.GetSafeFileName();
                    var filePath = Path.Combine(categoryDir, fileName);
                    
                    await File.WriteAllTextAsync(filePath, markdown);
                    webDoc.LocalPath = Path.GetRelativePath(_docsBaseDir, filePath);
                    
                    results.Add($"✅ {webDoc.Title} → {webDoc.LocalPath}");
                    successCount++;
                }
                catch (Exception ex)
                {
                    results.Add($"❌ {url}: Save failed - {ex.Message}");
                    failureCount++;
                }
            }
            
            var summary = new StringBuilder();
            summary.AppendLine($"Batch download completed: {successCount} succeeded, {failureCount} failed");
            summary.AppendLine();
            summary.AppendLine("Results:");
            foreach (var result in results)
            {
                summary.AppendLine(result);
            }
            
            return summary.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch fetch");
            return $"Error: Unexpected error: {ex.Message}";
        }
    }

    [McpServerTool, Description("List downloaded web documents")]
    public ValueTask<string> ListWebDocs(
        [Description("Optional category filter")]
        string? category = null)
    {
        try
        {
            var searchDir = string.IsNullOrEmpty(category)
                ? _webCacheDir
                : Path.Combine(_webCacheDir, category.ToLowerInvariant().Replace(' ', '-'));
            
            if (!Directory.Exists(searchDir))
            {
                return ValueTask.FromResult("No web documents found");
            }
            
            var files = Directory.GetFiles(searchDir, "*.md", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(_docsBaseDir, f))
                .OrderBy(f => f)
                .ToList();
            
            if (!files.Any())
            {
                return ValueTask.FromResult("No web documents found");
            }
            
            var response = new StringBuilder();
            response.AppendLine($"Found {files.Count} web document(s):");
            response.AppendLine();
            
            foreach (var file in files)
            {
                response.AppendLine($"- {file}");
            }
            
            return ValueTask.FromResult(response.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing web docs");
            return ValueTask.FromResult($"Error: {ex.Message}");
        }
    }

    [McpServerTool, Description("Suggest popular refactoring and code smell resources")]
    public ValueTask<string> SuggestRefactoringResources()
    {
        var resources = new[]
        {
            new { Title = "Refactoring Guru - Code Smells", Url = "https://refactoring.guru/refactoring/smells", Category = "code-smells" },
            new { Title = "Refactoring Guru - Refactoring Techniques", Url = "https://refactoring.guru/refactoring/techniques", Category = "refactoring" },
            new { Title = "Martin Fowler - Refactoring Catalog", Url = "https://refactoring.com/catalog/", Category = "refactoring" },
            new { Title = "SourceMaking - Code Smells", Url = "https://sourcemaking.com/refactoring/smells", Category = "code-smells" },
            new { Title = "SourceMaking - Refactoring", Url = "https://sourcemaking.com/refactoring", Category = "refactoring" },
            new { Title = "Clean Code - Summary", Url = "https://gist.github.com/wojteklu/73c6914cc446146b8b533c0988cf8d29", Category = "clean-code" },
            new { Title = "SOLID Principles", Url = "https://www.digitalocean.com/community/conceptual-articles/s-o-l-i-d-the-first-five-principles-of-object-oriented-design", Category = "design-principles" }
        };
        
        var response = new StringBuilder();
        response.AppendLine("Suggested refactoring and code smell resources:");
        response.AppendLine();
        
        foreach (var resource in resources)
        {
            response.AppendLine($"**{resource.Title}**");
            response.AppendLine($"  URL: {resource.Url}");
            response.AppendLine($"  Category: {resource.Category}");
            response.AppendLine();
        }
        
        response.AppendLine("To download any of these resources, use:");
        response.AppendLine("- `fetch_web_page` for a single page");
        response.AppendLine("- `fetch_web_pages_batch` for multiple pages");
        
        return ValueTask.FromResult(response.ToString());
    }
}