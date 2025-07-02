using System.Text.RegularExpressions;

namespace DocsRef.Core;

/// <summary>
/// Parser for .gitignore patterns
/// </summary>
public class GitIgnoreParser
{
    private readonly List<GitIgnorePattern> _patterns = new();
    private readonly string _basePath;
    private readonly bool _smartFilter;
    
    // Source file extensions that should always be included
    private static readonly HashSet<string> SourceFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csproj", ".sln", ".json", ".xml", ".config", ".yaml", ".yml",
        ".md", ".txt", ".sh", ".ps1", ".cmd", ".bat",
        ".js", ".ts", ".jsx", ".tsx", ".css", ".scss", ".html", ".vue",
        ".py", ".java", ".cpp", ".c", ".h", ".hpp", ".go", ".rs", ".rb"
    };
    
    // Patterns that should always be excluded even in smart filter mode
    private static readonly string[] AlwaysExcludePatterns = new[]
    {
        "node_modules/",
        ".git/",
        ".vs/",
        ".vscode/",
        ".idea/",
        "__pycache__/",
        "*.pyc",
        ".DS_Store",
        "Thumbs.db",
        "*.log",
        "*.tmp",
        "*.temp",
        "*.cache"
    };

    public GitIgnoreParser(string basePath, bool smartFilter = false)
    {
        _basePath = basePath.Replace('\\', '/').TrimEnd('/');
        _smartFilter = smartFilter;
        
        // Add always-exclude patterns
        foreach (var pattern in AlwaysExcludePatterns)
        {
            AddPattern(pattern);
        }
    }

    public void AddGitIgnoreFile(string gitIgnorePath)
    {
        if (!File.Exists(gitIgnorePath))
            return;

        var directory = Path.GetDirectoryName(gitIgnorePath)?.Replace('\\', '/') ?? "";
        var relativeDir = GetRelativePath(_basePath, directory);

        foreach (var line in File.ReadAllLines(gitIgnorePath))
        {
            var trimmedLine = line.Trim();
            
            // Skip empty lines and comments
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith('#'))
                continue;

            _patterns.Add(new GitIgnorePattern(trimmedLine, relativeDir));
        }
    }

    public void AddPattern(string pattern)
    {
        if (!string.IsNullOrWhiteSpace(pattern))
        {
            _patterns.Add(new GitIgnorePattern(pattern, ""));
        }
    }

    public bool IsIgnored(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var relativePath = GetRelativePath(_basePath, normalizedPath);

        // In smart filter mode, always include source files regardless of gitignore
        if (_smartFilter)
        {
            var extension = Path.GetExtension(filePath);
            if (SourceFileExtensions.Contains(extension))
            {
                // Still check always-exclude patterns
                foreach (var pattern in _patterns)
                {
                    if (AlwaysExcludePatterns.Contains(pattern.OriginalPattern) && pattern.Matches(relativePath))
                        return true;
                }
                
                // Check if it's in a build output directory
                if (IsBuildOutputPath(relativePath))
                    return true;
                    
                return false;
            }
        }

        foreach (var pattern in _patterns)
        {
            if (pattern.Matches(relativePath))
                return true;
        }

        return false;
    }
    
    private bool IsBuildOutputPath(string relativePath)
    {
        var parts = relativePath.Split('/');
        foreach (var part in parts)
        {
            if (part.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Debug", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("Release", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("x64", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("x86", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("build", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("dist", StringComparison.OrdinalIgnoreCase) ||
                part.Equals("out", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private string GetRelativePath(string basePath, string fullPath)
    {
        var normalizedBase = basePath.Replace('\\', '/').TrimEnd('/');
        var normalizedFull = fullPath.Replace('\\', '/');

        if (normalizedFull.StartsWith(normalizedBase))
        {
            return normalizedFull.Substring(normalizedBase.Length).TrimStart('/');
        }

        return normalizedFull;
    }

    private class GitIgnorePattern
    {
        private readonly Regex _regex;
        private readonly bool _isDirectory;
        private readonly bool _isNegation;
        private readonly string _baseDirectory;
        public string OriginalPattern { get; }

        public GitIgnorePattern(string pattern, string baseDirectory)
        {
            _baseDirectory = baseDirectory;
            OriginalPattern = pattern;
            
            // Handle negation patterns
            if (pattern.StartsWith('!'))
            {
                _isNegation = true;
                pattern = pattern.Substring(1);
            }

            // Handle directory patterns
            if (pattern.EndsWith('/'))
            {
                _isDirectory = true;
                pattern = pattern.TrimEnd('/');
            }

            // Convert gitignore pattern to regex
            var regexPattern = ConvertToRegex(pattern);
            _regex = new Regex(regexPattern, RegexOptions.Compiled);
        }

        public bool Matches(string path)
        {
            // Apply base directory context
            var targetPath = path;
            if (!string.IsNullOrEmpty(_baseDirectory))
            {
                if (!path.StartsWith(_baseDirectory + "/"))
                    return false;
                targetPath = path.Substring(_baseDirectory.Length + 1);
            }

            var isMatch = _regex.IsMatch(targetPath);
            
            if (_isDirectory && isMatch)
            {
                // For directory patterns, check if the path is a directory or inside it
                isMatch = targetPath.Contains('/') || path.EndsWith(targetPath);
            }

            return _isNegation ? !isMatch : isMatch;
        }

        private string ConvertToRegex(string pattern)
        {
            // Escape special regex characters except * and ?
            pattern = Regex.Escape(pattern);
            pattern = pattern.Replace(@"\*", ".*").Replace(@"\?", ".");

            // Handle patterns that should match anywhere
            if (!pattern.Contains('/'))
            {
                // Pattern without slash matches in any directory
                return $@"(^|/){pattern}(/|$)";
            }
            else if (pattern.StartsWith('/'))
            {
                // Pattern starting with slash matches from root
                return $"^{pattern.Substring(1)}(/|$)";
            }
            else
            {
                // Pattern with slash matches as specified
                return $@"(^|/){pattern}(/|$)";
            }
        }
    }
}