using System.Text.Json;
using DocsRef.Core;
using DocsRef.Tools;
using Microsoft.Extensions.Logging;

namespace DocsRef.Cli;

public class DocCommands
{
    private readonly DocumentManager _documentManager;
    private readonly ILogger<DocCommands> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly DocTools _docTools;

    public DocCommands(DocumentManager documentManager, ILogger<DocCommands> logger, DocTools docTools)
    {
        _documentManager = documentManager;
        _logger = logger;
        _docTools = docTools;
        _jsonOptions = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task ListDocs(string? pattern, string? directory, int maxResults, OutputFormat output)
    {
        try
        {
            var result = await _docTools.ListDocs(pattern, directory, maxResults);
            
            if (output == OutputFormat.Json)
            {
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var files = new List<string>();
                var isFileList = false;
                
                foreach (var line in lines)
                {
                    if (line.Contains("documents found") || line.Contains("Showing"))
                    {
                        isFileList = true;
                        continue;
                    }
                    if (isFileList && !line.StartsWith("Note:") && !line.StartsWith("Use parameters"))
                    {
                        files.Add(line.Trim());
                    }
                }
                
                var jsonResult = new
                {
                    pattern = pattern,
                    directory = directory,
                    maxResults = maxResults,
                    count = files.Count,
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

    public async Task Summary(OutputFormat output)
    {
        try
        {
            var result = await _docTools.ListDocsSummary();
            
            if (output == OutputFormat.Json)
            {
                var stats = new Dictionary<string, object>();
                var lines = result.Split('\n');
                string? currentRepo = null;
                var repoStats = new Dictionary<string, Dictionary<string, int>>();
                
                foreach (var line in lines)
                {
                    if (line.Contains("Repository:"))
                    {
                        currentRepo = line.Split("Repository:")[1].Trim();
                        repoStats[currentRepo] = new Dictionary<string, int>();
                    }
                    else if (line.Contains("Total files:") && currentRepo != null)
                    {
                        var count = int.Parse(line.Split("Total files:")[1].Trim());
                        repoStats[currentRepo]["totalFiles"] = count;
                    }
                    else if (line.Contains(":") && line.Contains("files") && currentRepo != null)
                    {
                        var parts = line.Split(':');
                        if (parts.Length == 2)
                        {
                            var ext = parts[0].Trim();
                            var countStr = parts[1].Replace("files", "").Trim();
                            if (int.TryParse(countStr, out var count))
                            {
                                repoStats[currentRepo][ext] = count;
                            }
                        }
                    }
                }
                
                var jsonResult = new
                {
                    repositories = repoStats
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

    public async Task Tree(string? directory, int maxDepth, OutputFormat output)
    {
        try
        {
            var result = await _docTools.ListDocsTree(directory, maxDepth);
            
            if (output == OutputFormat.Json)
            {
                // Parse tree structure into JSON
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var tree = ParseTreeStructure(lines);
                
                var jsonResult = new
                {
                    directory = directory ?? ".",
                    maxDepth = maxDepth,
                    tree = tree
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

    public async Task GetDoc(string path, int? page, OutputFormat output)
    {
        try
        {
            var result = await _docTools.GetDoc(path, page ?? 1);
            
            if (output == OutputFormat.Json)
            {
                var lines = result.Split('\n');
                var metadata = new Dictionary<string, object>();
                var contentStartIndex = 0;
                
                // Parse metadata from header
                foreach (var line in lines.Take(10))
                {
                    if (line.StartsWith("=== Document:"))
                    {
                        metadata["path"] = line.Split("Document:")[1].Trim();
                    }
                    else if (line.StartsWith("=== Page"))
                    {
                        var pageInfo = line.Replace("=== Page", "").Replace("===", "").Trim();
                        var parts = pageInfo.Split("of");
                        if (parts.Length == 2)
                        {
                            metadata["currentPage"] = int.Parse(parts[0].Trim());
                            metadata["totalPages"] = int.Parse(parts[1].Trim());
                        }
                    }
                    else if (line.Contains("===") && contentStartIndex == 0)
                    {
                        contentStartIndex = Array.IndexOf(lines, line) + 1;
                    }
                }
                
                var content = string.Join("\n", lines.Skip(contentStartIndex));
                
                var jsonResult = new
                {
                    path = path,
                    page = page ?? 1,
                    metadata = metadata,
                    content = content
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

    public async Task Grep(string pattern, bool ignoreCase, OutputFormat output)
    {
        try
        {
            var result = await _docTools.GrepDocs(pattern, ignoreCase);
            
            if (output == OutputFormat.Json)
            {
                var matches = new List<object>();
                var lines = result.Split('\n');
                string? currentFile = null;
                var currentMatches = new List<object>();
                
                foreach (var line in lines)
                {
                    if (line.EndsWith(":"))
                    {
                        // New file
                        if (currentFile != null && currentMatches.Any())
                        {
                            matches.Add(new
                            {
                                file = currentFile,
                                matches = currentMatches.ToArray()
                            });
                        }
                        currentFile = line.TrimEnd(':');
                        currentMatches.Clear();
                    }
                    else if (line.Contains(":") && currentFile != null)
                    {
                        var parts = line.Split(':', 2);
                        if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out var lineNum))
                        {
                            currentMatches.Add(new
                            {
                                line = lineNum,
                                content = parts[1].Trim()
                            });
                        }
                    }
                }
                
                // Add last file
                if (currentFile != null && currentMatches.Any())
                {
                    matches.Add(new
                    {
                        file = currentFile,
                        matches = currentMatches.ToArray()
                    });
                }
                
                var jsonResult = new
                {
                    pattern = pattern,
                    ignoreCase = ignoreCase,
                    totalMatches = matches.Sum(m => ((dynamic)m).matches.Length),
                    files = matches
                };
                Console.WriteLine(JsonSerializer.Serialize(jsonResult, _jsonOptions));
            }
            else
            {
                Console.WriteLine(result);
            }
            
            Environment.Exit(result.Contains("No matches found") ? 1 : 0);
        }
        catch (Exception ex)
        {
            HandleError(ex, output);
        }
    }

    private object ParseTreeStructure(string[] lines)
    {
        var root = new Dictionary<string, object>();
        var stack = new Stack<(Dictionary<string, object> node, int level)>();
        stack.Push((root, -1));
        
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            var level = 0;
            var content = line;
            
            // Count indentation level
            while (content.StartsWith("  ") || content.StartsWith("│ ") || content.StartsWith("├─") || content.StartsWith("└─"))
            {
                if (content.StartsWith("  "))
                {
                    level++;
                    content = content.Substring(2);
                }
                else
                {
                    level++;
                    content = content.Substring(2);
                    if (content.StartsWith(" "))
                        content = content.Substring(1);
                }
            }
            
            // Remove tree characters
            content = content.TrimStart('├', '└', '─', ' ');
            
            if (string.IsNullOrWhiteSpace(content)) continue;
            
            // Pop stack to correct level
            while (stack.Count > 1 && stack.Peek().level >= level)
            {
                stack.Pop();
            }
            
            var parent = stack.Peek().node;
            var isDirectory = content.EndsWith("/");
            var name = content.TrimEnd('/');
            
            if (isDirectory)
            {
                var newNode = new Dictionary<string, object>();
                parent[name] = newNode;
                stack.Push((newNode, level));
            }
            else
            {
                parent[name] = "file";
            }
        }
        
        return root;
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