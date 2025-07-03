namespace DocsRef.Core.Models;

public class WebDocument
{
    public required string Url { get; set; }
    public required string Title { get; set; }
    public required string MarkdownContent { get; set; }
    public required DateTime DownloadedAt { get; set; }
    public string? LocalPath { get; set; }
    public string? Category { get; set; }
    public List<string> Tags { get; set; } = new();
    
    public string GetSafeFileName()
    {
        var uri = new Uri(Url);
        var host = uri.Host.Replace("www.", "");
        var path = uri.AbsolutePath.Trim('/').Replace('/', '-');
        
        if (string.IsNullOrEmpty(path))
        {
            path = "index";
        }
        
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = string.Join("", $"{host}-{path}".Select(c => invalidChars.Contains(c) ? '-' : c));
        
        safeName = System.Text.RegularExpressions.Regex.Replace(safeName, @"-{2,}", "-");
        
        if (safeName.Length > 100)
        {
            safeName = safeName.Substring(0, 100);
        }
        
        return $"{safeName}.md";
    }
    
    public static WebDocument FromMarkdown(string url, string markdownContent)
    {
        var lines = markdownContent.Split('\n');
        var title = "Untitled";
        DateTime downloadedAt = DateTime.UtcNow;
        
        bool inFrontMatter = false;
        int contentStartIndex = 0;
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            
            if (i == 0 && line == "---")
            {
                inFrontMatter = true;
                continue;
            }
            
            if (inFrontMatter)
            {
                if (line == "---")
                {
                    inFrontMatter = false;
                    contentStartIndex = i + 1;
                    continue;
                }
                
                if (line.StartsWith("downloaded:"))
                {
                    var dateStr = line.Substring("downloaded:".Length).Trim().Replace(" UTC", "");
                    if (DateTime.TryParse(dateStr, out var parsed))
                    {
                        downloadedAt = parsed;
                    }
                }
            }
            else if (line.StartsWith("# "))
            {
                title = line.Substring(2).Trim();
                break;
            }
        }
        
        return new WebDocument
        {
            Url = url,
            Title = title,
            MarkdownContent = markdownContent,
            DownloadedAt = downloadedAt
        };
    }
}