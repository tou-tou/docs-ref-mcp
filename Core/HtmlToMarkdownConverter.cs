using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using ReverseMarkdown;

namespace DocsRef.Core;

public class HtmlToMarkdownConverter
{
    private readonly ILogger<HtmlToMarkdownConverter> _logger;
    private readonly Converter _converter;
    private static readonly Regex ExcessiveNewlineRegex = new(@"\n{3,}", RegexOptions.Compiled);
    private static readonly Regex TrailingWhitespaceRegex = new(@"[ \t]+$", RegexOptions.Multiline | RegexOptions.Compiled);

    public HtmlToMarkdownConverter(ILogger<HtmlToMarkdownConverter> logger)
    {
        _logger = logger;
        
        var config = new Config
        {
            UnknownTags = Config.UnknownTagsOption.PassThrough,
            GithubFlavored = true,
            SmartHrefHandling = true,
            RemoveComments = true,
            WhitelistUriSchemes = new[] { "http", "https", "ftp", "mailto" }
        };
        
        _converter = new Converter(config);
    }

    public (bool success, string markdown, string? error) ConvertHtmlToMarkdown(string html, string sourceUrl)
    {
        try
        {
            _logger.LogInformation("Converting HTML to Markdown for {Url}", sourceUrl);
            
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            
            RemoveUnwantedElements(doc);
            
            ExtractMainContent(doc);
            
            var cleanedHtml = doc.DocumentNode.OuterHtml;
            
            var markdown = _converter.Convert(cleanedHtml);
            
            markdown = PostProcessMarkdown(markdown, sourceUrl);
            
            _logger.LogInformation("Successfully converted HTML to Markdown ({Length} chars)", markdown.Length);
            return (true, markdown, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting HTML to Markdown for {Url}", sourceUrl);
            return (false, string.Empty, $"Conversion error: {ex.Message}");
        }
    }

    private void RemoveUnwantedElements(HtmlDocument doc)
    {
        var unwantedSelectors = new[]
        {
            "script",
            "style",
            "nav",
            "header nav",
            "footer",
            ".navigation",
            ".sidebar",
            ".advertisement",
            ".ads",
            "#comments",
            ".comments",
            ".social-share",
            ".cookie-notice",
            ".popup",
            ".modal"
        };

        foreach (var selector in unwantedSelectors)
        {
            var nodes = doc.DocumentNode.SelectNodes($"//{selector}");
            if (nodes != null)
            {
                foreach (var node in nodes.ToList())
                {
                    node.Remove();
                }
            }
        }
    }

    private void ExtractMainContent(HtmlDocument doc)
    {
        var contentSelectors = new[]
        {
            "main",
            "article",
            "[role='main']",
            ".main-content",
            ".content",
            "#content",
            ".post-content",
            ".entry-content"
        };

        HtmlNode? mainContent = null;
        foreach (var selector in contentSelectors)
        {
            mainContent = doc.DocumentNode.SelectSingleNode($"//{selector}");
            if (mainContent != null)
                break;
        }

        if (mainContent != null)
        {
            var newDoc = new HtmlDocument();
            var title = doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Trim();
            var h1 = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText?.Trim();
            
            var html = new StringBuilder();
            if (!string.IsNullOrEmpty(title) && (string.IsNullOrEmpty(h1) || !title.Contains(h1)))
            {
                html.AppendLine($"<h1>{HtmlEntity.Entitize(title)}</h1>");
            }
            html.Append(mainContent.OuterHtml);
            
            newDoc.LoadHtml(html.ToString());
            doc.DocumentNode.RemoveAllChildren();
            doc.DocumentNode.AppendChild(newDoc.DocumentNode);
        }
    }

    private string PostProcessMarkdown(string markdown, string sourceUrl)
    {
        var processed = new StringBuilder();
        
        processed.AppendLine($"---");
        processed.AppendLine($"source: {sourceUrl}");
        processed.AppendLine($"downloaded: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        processed.AppendLine($"---");
        processed.AppendLine();
        
        markdown = ExcessiveNewlineRegex.Replace(markdown, "\n\n");
        
        markdown = TrailingWhitespaceRegex.Replace(markdown, "");
        
        var lines = markdown.Split('\n');
        bool inCodeBlock = false;
        
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("```"))
            {
                inCodeBlock = !inCodeBlock;
            }
            
            if (!inCodeBlock && string.IsNullOrWhiteSpace(line))
            {
                if (processed.Length > 0 && !processed.ToString().EndsWith("\n\n"))
                {
                    processed.AppendLine();
                }
                continue;
            }
            
            processed.AppendLine(line);
        }
        
        return processed.ToString().TrimEnd() + "\n";
    }

    public async Task<Dictionary<string, (bool success, string markdown, string? error)>> ConvertMultipleAsync(
        Dictionary<string, string> urlToHtmlMap)
    {
        var tasks = urlToHtmlMap.Select(async kvp =>
        {
            await Task.Yield();
            return new
            {
                Url = kvp.Key,
                Result = ConvertHtmlToMarkdown(kvp.Value, kvp.Key)
            };
        });
        
        var results = await Task.WhenAll(tasks);
        
        return results.ToDictionary(
            r => r.Url,
            r => r.Result
        );
    }
}