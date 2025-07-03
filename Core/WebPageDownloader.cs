using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;

namespace DocsRef.Core;

public class WebPageDownloader
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebPageDownloader> _logger;
    private readonly int _maxRetries;
    private readonly TimeSpan _timeout;

    public WebPageDownloader(ILogger<WebPageDownloader> logger)
    {
        _logger = logger;
        _maxRetries = int.Parse(Environment.GetEnvironmentVariable("WEB_MAX_RETRIES") ?? "3");
        _timeout = TimeSpan.FromSeconds(int.Parse(Environment.GetEnvironmentVariable("WEB_TIMEOUT") ?? "30"));
        
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        
        _httpClient = new HttpClient(handler)
        {
            Timeout = _timeout
        };
        
        var userAgent = Environment.GetEnvironmentVariable("WEB_USER_AGENT") ?? 
                       "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9,ja;q=0.8");
        _httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip, deflate");
    }

    public async Task<(bool success, string content, string? error)> DownloadPageAsync(string url)
    {
        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Downloading page from {Url} (attempt {Attempt}/{MaxRetries})", 
                    url, attempt, _maxRetries);
                
                using var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("Successfully downloaded {Length} characters from {Url}", 
                        content.Length, url);
                    return (true, content, null);
                }
                
                _logger.LogWarning("HTTP {StatusCode} when downloading {Url}: {Reason}", 
                    response.StatusCode, url, response.ReasonPhrase);
                
                if (response.StatusCode == HttpStatusCode.NotFound || 
                    response.StatusCode == HttpStatusCode.Forbidden ||
                    response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return (false, string.Empty, $"HTTP {response.StatusCode}: {response.ReasonPhrase}");
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogError("Timeout downloading {Url} after {Timeout}s", url, _timeout.TotalSeconds);
                if (attempt == _maxRetries)
                    return (false, string.Empty, $"Timeout after {_timeout.TotalSeconds} seconds");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error downloading {Url}", url);
                if (attempt == _maxRetries)
                    return (false, string.Empty, $"Network error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error downloading {Url}", url);
                return (false, string.Empty, $"Unexpected error: {ex.Message}");
            }
            
            if (attempt < _maxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));
                _logger.LogInformation("Retrying in {Delay}s...", delay.TotalSeconds);
                await Task.Delay(delay);
            }
        }
        
        return (false, string.Empty, "Max retries exceeded");
    }

    public async Task<Dictionary<string, (bool success, string content, string? error)>> DownloadPagesAsync(
        IEnumerable<string> urls)
    {
        var tasks = urls.Select(async url => new
        {
            Url = url,
            Result = await DownloadPageAsync(url)
        });
        
        var results = await Task.WhenAll(tasks);
        
        return results.ToDictionary(
            r => r.Url,
            r => r.Result
        );
    }
}