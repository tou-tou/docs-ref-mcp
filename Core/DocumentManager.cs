using System.Text.Json;
using System.Text.RegularExpressions;

namespace DocsRef.Core;

public class DocumentManager
{
    private static readonly string[] DefaultExtensions = new[]
    {
        // Document formats
        ".md", ".mdx", ".txt", ".rst", ".asciidoc", ".org",
        // Data/Config formats
        ".json", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf", ".xml", ".csv",
        // Programming languages
        ".py", ".js", ".jsx", ".ts", ".tsx", ".java", ".cpp", ".c", ".h", ".hpp",
        ".cs", ".go", ".rs", ".rb", ".php", ".swift", ".kt", ".scala", ".r", ".m",
        // Scripts/Shell
        ".sh", ".bash", ".zsh", ".fish", ".ps1", ".bat", ".cmd",
        // Web
        ".html", ".htm", ".css", ".scss", ".sass", ".less", ".vue", ".svelte", ".astro",
        // Config/Build
        ".dockerfile", ".dockerignore", ".gitignore", ".env", ".env.example",
        ".editorconfig", ".prettierrc", ".eslintrc", ".babelrc",
        // Others
        ".sql", ".graphql", ".proto", ".ipynb"
    };

    // Default patterns to exclude (similar to .gitignore)
    private static readonly string[] DefaultExcludePatterns = new[]
    {
        ".git/",
        ".svn/",
        ".hg/",
        ".vs/",
        ".vscode/",
        ".idea/",
        "node_modules/",
        "__pycache__/",
        "*.pyc",
        "bin/",
        "obj/",
        "dist/",
        "build/",
        "target/",
        "*.min.js",
        "*.min.css",
        "*.map",
        "*.log",
        "*.lock",
        "package-lock.json",
        "yarn.lock"
    };

    private readonly string _baseDir;
    private readonly string _docsDir;
    private readonly string _metadataFile;
    private readonly List<string>? _allowedFolders;
    private readonly string[] _allowedExtensions;
    private readonly Dictionary<string, string> _docsContent = new();
    private readonly Dictionary<string, string> _docsMetadata = new();
    
    private readonly int _maxCharsPerPage;
    private readonly int _largeFileThreshold;
    private readonly int _maxFileSize;
    private readonly bool _respectGitIgnore;
    private readonly GitIgnoreParser _ignoreParser;

    public DocumentManager(List<string>? allowedFolders = null)
    {
        // Get base directory from environment or use current directory
        _baseDir = Environment.GetEnvironmentVariable("DOCS_BASE_DIR") ?? Directory.GetCurrentDirectory();
        _docsDir = Path.Combine(_baseDir, "docs");
        _metadataFile = Path.Combine(_baseDir, "docs_metadata.json");
        
        _allowedFolders = allowedFolders;
        
        // Get file extensions from environment or use defaults
        var extensionsEnv = Environment.GetEnvironmentVariable("DOCS_FILE_EXTENSIONS");
        if (!string.IsNullOrWhiteSpace(extensionsEnv))
        {
            _allowedExtensions = extensionsEnv
                .Split(',')
                .Select(ext => ext.Trim())
                .Where(ext => !string.IsNullOrEmpty(ext))
                .Select(ext => ext.StartsWith(".") ? ext : $".{ext}")
                .ToArray();
            Console.WriteLine($"Using custom file extensions: {string.Join(", ", _allowedExtensions)}");
        }
        else
        {
            _allowedExtensions = DefaultExtensions;
        }
        
        // Pagination settings
        _maxCharsPerPage = int.TryParse(Environment.GetEnvironmentVariable("DOCS_MAX_CHARS_PER_PAGE"), out var maxChars) 
            ? maxChars : 10000;
        _largeFileThreshold = int.TryParse(Environment.GetEnvironmentVariable("DOCS_LARGE_FILE_THRESHOLD"), out var threshold) 
            ? threshold : 15000;
        
        // Git repository settings
        _maxFileSize = int.TryParse(Environment.GetEnvironmentVariable("DOCS_MAX_FILE_SIZE"), out var maxSize) 
            ? maxSize : 1024 * 1024; // 1MB default
        _respectGitIgnore = bool.TryParse(Environment.GetEnvironmentVariable("DOCS_RESPECT_GITIGNORE"), out var respectGit) 
            ? respectGit : true;
        
        // Check for smart filter mode
        var useSmartFilter = bool.TryParse(Environment.GetEnvironmentVariable("DOCS_SMART_FILTER"), out var smartFilter) 
            ? smartFilter : true; // Default to true for smart filtering
        
        // Initialize ignore parser with smart filter
        _ignoreParser = new GitIgnoreParser(_docsDir, useSmartFilter);
        
        // Add default exclude patterns
        foreach (var pattern in DefaultExcludePatterns)
        {
            _ignoreParser.AddPattern(pattern);
        }
        
        // Add custom exclude patterns from environment
        var customPatterns = Environment.GetEnvironmentVariable("DOCS_EXCLUDE_PATTERNS");
        if (!string.IsNullOrWhiteSpace(customPatterns))
        {
            foreach (var pattern in customPatterns.Split(',').Select(p => p.Trim()))
            {
                _ignoreParser.AddPattern(pattern);
            }
        }
    }

    public void LoadDocuments()
    {
        // Load metadata if exists
        if (File.Exists(_metadataFile))
        {
            try
            {
                var metadataJson = File.ReadAllText(_metadataFile);
                var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataJson);
                if (metadata != null)
                {
                    foreach (var kvp in metadata)
                    {
                        _docsMetadata[kvp.Key] = kvp.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading metadata: {ex.Message}");
            }
        }

        // Load documents based on allowed folders
        if (_allowedFolders != null && _allowedFolders.Any())
        {
            foreach (var folderName in _allowedFolders)
            {
                var folderPath = Path.Combine(_docsDir, folderName);
                if (Directory.Exists(folderPath))
                {
                    LoadFolder(folderPath);
                }
                else
                {
                    Console.WriteLine($"Warning: Folder not found: {folderName}");
                }
            }
        }
        else
        {
            // Load all files from docs directory
            LoadAllFiles();
        }
        
        Console.WriteLine($"Loaded {_docsContent.Count} documents (excluded git/binary files)");
    }

    private void LoadFolder(string folderPath)
    {
        var dirInfo = new DirectoryInfo(folderPath);
        
        // Load .gitignore files if respect git ignore is enabled
        if (_respectGitIgnore)
        {
            LoadGitIgnoreFiles(dirInfo);
        }
        
        foreach (var file in dirInfo.GetFiles("*.*", SearchOption.AllDirectories))
        {
            if (ShouldLoadFile(file))
            {
                LoadFile(file);
            }
        }
    }

    private void LoadAllFiles()
    {
        if (!Directory.Exists(_docsDir))
        {
            Console.WriteLine($"Docs directory not found: {_docsDir}");
            return;
        }

        var dirInfo = new DirectoryInfo(_docsDir);
        
        // Load .gitignore files if respect git ignore is enabled
        if (_respectGitIgnore)
        {
            LoadGitIgnoreFiles(dirInfo);
        }
        
        foreach (var file in dirInfo.GetFiles("*.*", SearchOption.AllDirectories))
        {
            if (ShouldLoadFile(file))
            {
                LoadFile(file);
            }
        }
    }

    private void LoadGitIgnoreFiles(DirectoryInfo rootDir)
    {
        foreach (var gitIgnoreFile in rootDir.GetFiles(".gitignore", SearchOption.AllDirectories))
        {
            _ignoreParser.AddGitIgnoreFile(gitIgnoreFile.FullName);
        }
    }

    private bool ShouldLoadFile(FileInfo file)
    {
        // Check if file extension is allowed
        if (!_allowedExtensions.Contains(file.Extension.ToLower()))
        {
            return false;
        }
        
        // Check if file is ignored by patterns
        if (_ignoreParser.IsIgnored(file.FullName))
        {
            return false;
        }
        
        // Check file size
        if (file.Length > _maxFileSize)
        {
            Console.WriteLine($"Skipping large file: {file.FullName} ({file.Length / 1024 / 1024}MB)");
            return false;
        }
        
        return true;
    }

    private void LoadFile(FileInfo file)
    {
        try
        {
            // Check if file is binary
            if (IsBinaryFile(file.FullName))
            {
                return;
            }
            
            var relativePath = Path.GetRelativePath(_docsDir, file.FullName).Replace('\\', '/');
            var content = File.ReadAllText(file.FullName);
            _docsContent[relativePath] = content;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading {file.FullName}: {ex.Message}");
        }
    }

    private bool IsBinaryFile(string filePath)
    {
        const int checkLength = 8000;
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            var buffer = new byte[Math.Min(checkLength, stream.Length)];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            
            // Check for null bytes (common in binary files)
            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] == 0)
                {
                    return true;
                }
            }
            
            // Check for high percentage of non-ASCII characters
            var nonAsciiCount = 0;
            for (int i = 0; i < bytesRead; i++)
            {
                if (buffer[i] > 127)
                {
                    nonAsciiCount++;
                }
            }
            
            // If more than 30% non-ASCII, consider it binary
            return (double)nonAsciiCount / bytesRead > 0.3;
        }
        catch
        {
            // If we can't read the file, assume it's binary
            return true;
        }
    }

    public string ListDocuments()
    {
        var result = new List<string>();
        
        foreach (var path in _docsContent.Keys.OrderBy(k => k))
        {
            if (_docsMetadata.TryGetValue(path, out var description) && !string.IsNullOrEmpty(description))
            {
                result.Add($"{path} - {description}");
            }
            else
            {
                result.Add(path);
            }
        }
        
        return string.Join("\n", result);
    }

    public string ListDocumentsFiltered(string? pattern = null, string? directory = null, int maxResults = 100)
    {
        var query = _docsContent.Keys.AsEnumerable();
        
        // Filter by directory
        if (!string.IsNullOrEmpty(directory))
        {
            var normalizedDir = directory.Replace('\\', '/').TrimEnd('/') + "/";
            query = query.Where(path => path.StartsWith(normalizedDir));
        }
        
        // Filter by pattern (simple wildcard support)
        if (!string.IsNullOrEmpty(pattern))
        {
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*\\*", ".*")  // ** matches any path
                .Replace("\\*", "[^/]*")  // * matches any filename part
                + "$";
            var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
            query = query.Where(path => regex.IsMatch(path));
        }
        
        // Apply limit and sort
        var results = query.OrderBy(p => p).Take(maxResults).ToList();
        
        if (results.Count == 0)
        {
            return "No documents found matching the criteria.";
        }
        
        var output = new List<string>();
        foreach (var path in results)
        {
            if (_docsMetadata.TryGetValue(path, out var description) && !string.IsNullOrEmpty(description))
            {
                output.Add($"{path} - {description}");
            }
            else
            {
                output.Add(path);
            }
        }
        
        if (query.Count() > maxResults)
        {
            output.Add($"\n... and {query.Count() - maxResults} more files (showing first {maxResults})");
        }
        
        return string.Join("\n", output);
    }

    public string GetDocument(string path, int? page = null)
    {
        if (!_docsContent.TryGetValue(path, out var content))
        {
            return $"Error: Document not found: {path}";
        }

        var totalChars = content.Length;

        // Handle page parameter
        if (page == null)
        {
            // Auto-paginate large files
            if (totalChars > _largeFileThreshold)
            {
                page = 1;
            }
            else
            {
                return content;
            }
        }

        // Pagination logic
        var totalPages = (totalChars + _maxCharsPerPage - 1) / _maxCharsPerPage;

        if (page < 1)
        {
            return "Error: Page number must be 1 or greater";
        }

        if (page > totalPages)
        {
            return $"Error: Page {page} not found. Total pages: {totalPages} (max chars per page: {_maxCharsPerPage:N0})";
        }

        var startChar = (page.Value - 1) * _maxCharsPerPage;
        var endChar = Math.Min(startChar + _maxCharsPerPage, totalChars);

        // Adjust to not split lines
        if (endChar < totalChars)
        {
            var nextNewline = content.IndexOf('\n', endChar);
            if (nextNewline != -1)
            {
                endChar = nextNewline + 1;
            }
        }

        var pageContent = content.Substring(startChar, endChar - startChar);

        // Count lines for display
        var linesBeforeStart = content.Substring(0, startChar).Count(c => c == '\n');
        var pageLines = pageContent.Count(c => c == '\n');
        var totalLines = content.Count(c => c == '\n') + 1;
        var startLine = linesBeforeStart + 1;
        var endLine = Math.Min(startLine + pageLines, totalLines);

        // Build header
        var header = $"📄 Document: {path}\n";
        header += $"📖 Page {page}/{totalPages} (chars {startChar + 1:N0}-{endChar:N0}/{totalChars:N0})\n";
        header += $"📏 Lines {startLine}-{endLine}/{totalLines:N0} | Max chars per page: {_maxCharsPerPage:N0}\n";

        if (page == 1 && totalChars > _largeFileThreshold)
        {
            header += "⚠️  Large document auto-paginated. To see other pages:\n";
            header += $"💡 get_doc('{path}', page=2)  # Next page\n";
            header += $"💡 get_doc('{path}', page={totalPages})  # Last page\n";
        }

        header += new string('─', 60) + "\n\n";

        return header + pageContent;
    }

    public string GrepSearch(string pattern, bool ignoreCase = true)
    {
        try
        {
            var options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
            var regex = new Regex(pattern, options);
            var results = new List<string>();

            foreach (var (docPath, content) in _docsContent.OrderBy(kvp => kvp.Key))
            {
                var lines = content.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (regex.IsMatch(lines[i]))
                    {
                        var linePreview = lines[i].Trim();
                        if (linePreview.Length > 120)
                        {
                            linePreview = linePreview.Substring(0, 117) + "...";
                        }
                        results.Add($"{docPath}:{i + 1}: {linePreview}");
                    }
                }
            }

            if (results.Count == 0)
            {
                return "No matches found";
            }

            // Limit results
            if (results.Count > 100)
            {
                var total = results.Count;
                results = results.Take(100).ToList();
                results.Add($"\n... and {total - 100} more matches");
            }

            return string.Join("\n", results);
        }
        catch (ArgumentException ex)
        {
            return $"Error: Invalid regex pattern: {ex.Message}";
        }
    }

    public string GetDocumentTree(string? rootDirectory = null, int maxDepth = 3)
    {
        var paths = _docsContent.Keys.ToList();
        
        // Filter by root directory if specified
        if (!string.IsNullOrEmpty(rootDirectory))
        {
            var normalizedRoot = rootDirectory.Replace('\\', '/').TrimEnd('/') + "/";
            paths = paths.Where(p => p.StartsWith(normalizedRoot)).ToList();
        }
        
        // Build tree structure
        var tree = new TreeNode("");
        foreach (var path in paths)
        {
            var parts = path.Split('/');
            var currentNode = tree;
            
            for (int i = 0; i < parts.Length && i < maxDepth; i++)
            {
                var part = parts[i];
                if (!currentNode.Children.ContainsKey(part))
                {
                    currentNode.Children[part] = new TreeNode(part);
                }
                currentNode = currentNode.Children[part];
                currentNode.FileCount++;
            }
        }
        
        // Generate output
        var output = new List<string>();
        if (!string.IsNullOrEmpty(rootDirectory))
        {
            output.Add($"Directory tree for: {rootDirectory}");
        }
        else
        {
            output.Add("Directory tree:");
        }
        output.Add("");
        
        foreach (var child in tree.Children.OrderBy(kvp => kvp.Key))
        {
            BuildTreeOutput(output, child.Value, "", true, 1, maxDepth);
        }
        
        output.Add("");
        output.Add($"Total files: {paths.Count}");
        
        return string.Join("\n", output);
    }
    
    private void BuildTreeOutput(List<string> output, TreeNode node, string prefix, bool isLast, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;
        
        var connector = isLast ? "└── " : "├── ";
        var fileCountStr = node.FileCount > 0 ? $" ({node.FileCount} files)" : "";
        output.Add($"{prefix}{connector}{node.Name}/{fileCountStr}");
        
        var extension = isLast ? "    " : "│   ";
        var children = node.Children.OrderBy(kvp => kvp.Key).ToList();
        
        for (int i = 0; i < children.Count; i++)
        {
            BuildTreeOutput(output, children[i].Value, prefix + extension, i == children.Count - 1, depth + 1, maxDepth);
        }
    }
    
    private class TreeNode
    {
        public string Name { get; }
        public Dictionary<string, TreeNode> Children { get; } = new();
        public int FileCount { get; set; }
        
        public TreeNode(string name)
        {
            Name = name;
        }
    }

    public string GetDocumentSummary()
    {
        var output = new List<string>();
        output.Add("=== Document Repository Summary ===");
        output.Add("");
        
        // Group by top-level directory (repository)
        var repoGroups = _docsContent.Keys
            .GroupBy(path => path.Split('/')[0])
            .OrderBy(g => g.Key);
        
        int totalFiles = 0;
        var allExtensions = new Dictionary<string, int>();
        
        foreach (var repo in repoGroups)
        {
            var repoFiles = repo.ToList();
            totalFiles += repoFiles.Count;
            
            // Count extensions in this repo
            var extensions = repoFiles
                .Select(f => Path.GetExtension(f).ToLower())
                .Where(ext => !string.IsNullOrEmpty(ext))
                .GroupBy(ext => ext)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .ToList();
            
            output.Add($"📁 {repo.Key}/: {repoFiles.Count} files");
            
            if (extensions.Any())
            {
                var extStr = string.Join(", ", extensions.Select(g => $"{g.Key}: {g.Count()}"));
                output.Add($"   Top extensions: {extStr}");
            }
            
            // Aggregate all extensions
            foreach (var file in repoFiles)
            {
                var ext = Path.GetExtension(file).ToLower();
                if (!string.IsNullOrEmpty(ext))
                {
                    if (!allExtensions.ContainsKey(ext))
                        allExtensions[ext] = 0;
                    allExtensions[ext]++;
                }
            }
            
            output.Add("");
        }
        
        output.Add("=== Overall Statistics ===");
        output.Add($"Total documents: {totalFiles}");
        output.Add("");
        output.Add("Top file types:");
        
        var topExtensions = allExtensions
            .OrderByDescending(kvp => kvp.Value)
            .Take(10);
        
        foreach (var ext in topExtensions)
        {
            output.Add($"  {ext.Key}: {ext.Value} files");
        }
        
        return string.Join("\n", output);
    }

    public int GetDocumentCount() => _docsContent.Count;
}