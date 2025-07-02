using System.ComponentModel;
using System.Threading.Tasks;
using DocsRef.Core;
using ModelContextProtocol.Server;

namespace DocsRef.Tools;

[McpServerToolType, Description("Documentation management tools")]
public static class DocTools
{
    private static DocumentManager? _documentManager;

    public static void Initialize(DocumentManager documentManager)
    {
        _documentManager = documentManager;
    }

    [McpServerTool, Description("List available documents with optional filtering")]
    public static ValueTask<string> ListDocs(
        [Description("File path pattern (e.g., '*.cs', 'repos/UniVRM/**/*.shader')")] string? pattern = null,
        [Description("Directory to search in (e.g., 'repos/R3')")] string? directory = null,
        [Description("Maximum number of results to return (default: 100)")] int maxResults = 100)
    {
        if (_documentManager == null)
            return ValueTask.FromResult("Error: DocumentManager not initialized");
        
        // Use filtered list if any parameters are provided
        if (!string.IsNullOrEmpty(pattern) || !string.IsNullOrEmpty(directory) || maxResults != 100)
        {
            var result = _documentManager.ListDocumentsFiltered(pattern, directory, maxResults);
            return ValueTask.FromResult(result);
        }
        
        // For backward compatibility, if no parameters, show summary instead of full list
        var summary = _documentManager.GetDocumentSummary();
        var message = "Note: Full document list is too large. Showing summary instead.\n" +
                     "Use parameters to filter: ListDocs(pattern: \"*.cs\", directory: \"repos/R3\")\n\n" + 
                     summary;
        return ValueTask.FromResult(message);
    }

    [McpServerTool, Description("Show document repository summary and statistics")]
    public static ValueTask<string> ListDocsSummary()
    {
        if (_documentManager == null)
            return ValueTask.FromResult("Error: DocumentManager not initialized");
            
        var result = _documentManager.GetDocumentSummary();
        return ValueTask.FromResult(result);
    }

    [McpServerTool, Description("Show directory tree structure")]
    public static ValueTask<string> ListDocsTree(
        [Description("Root directory to show tree for (e.g., 'repos/UniVRM')")] string? directory = null,
        [Description("Maximum depth to display (default: 3)")] int maxDepth = 3)
    {
        if (_documentManager == null)
            return ValueTask.FromResult("Error: DocumentManager not initialized");
            
        var result = _documentManager.GetDocumentTree(directory, maxDepth);
        return ValueTask.FromResult(result);
    }

    [McpServerTool, Description("Get document content by path with pagination support")]
    public static ValueTask<string> GetDoc(
        [Description("Document file path")] string path,
        [Description("Page number (starts from 1, null for full document)")] int? page = null)
    {
        if (_documentManager == null)
            return ValueTask.FromResult("Error: DocumentManager not initialized");
            
        var content = _documentManager.GetDocument(path, page);
        return ValueTask.FromResult(content);
    }

    [McpServerTool, Description("Search documents using grep with regex support")]
    public static ValueTask<string> GrepDocs(
        [Description("Search pattern (supports regex)")] string pattern,
        [Description("Ignore case (default: true)")] bool ignoreCase = true)
    {
        if (_documentManager == null)
            return ValueTask.FromResult("Error: DocumentManager not initialized");
            
        var result = _documentManager.GrepSearch(pattern, ignoreCase);
        return ValueTask.FromResult(result);
    }
}