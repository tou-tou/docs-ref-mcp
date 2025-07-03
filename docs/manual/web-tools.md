# Web Tools Documentation

The DocsRef MCP server now includes powerful web page fetching and markdown conversion capabilities. These tools allow you to download web pages, convert them to clean markdown format, and cache them for offline access.

## Available Tools

### FetchWebPage
Downloads a single web page and converts it to markdown.

**Parameters:**
- `url` (string, required): URL of the web page to fetch
- `saveToCache` (bool, optional): Save to cache (default: true)

**Example:**
```
FetchWebPage(url: "https://refactoring.guru/refactoring/smells/long-method")
```

### FetchWebPageBatch
Downloads multiple web pages and converts them to markdown in a single operation.

**Parameters:**
- `urls` (string, required): Comma-separated list of URLs to fetch
- `saveToCache` (bool, optional): Save to cache (default: true)

**Example:**
```
FetchWebPageBatch(urls: "https://refactoring.guru/refactoring/smells/long-method,https://refactoring.guru/refactoring/techniques/extract-method")
```

### SearchRefactoringResources
Searches for high-quality refactoring and code smell resources from curated sources.

**Parameters:**
- `query` (string, required): Search query (e.g., 'extract method', 'code smells', 'SOLID principles')
- `includeDomains` (string, optional): Include these domains (comma-separated)
- `maxResults` (int, optional): Maximum results to return (default: 5)

**Example:**
```
SearchRefactoringResources(query: "code smells", maxResults: 3)
```

### ListCachedPages
Lists all cached web pages with their metadata.

**Example:**
```
ListCachedPages()
```

### ClearWebCache
Clears the web page cache.

**Parameters:**
- `clearAll` (bool, optional): Clear all cache (true) or only files older than 7 days (false, default)

**Example:**
```
ClearWebCache(clearAll: false)
```

## Configuration

The web tools can be configured using environment variables:

- `WEB_CACHE_DIR`: Directory for cached web content (default: `docs/web`)
- `WEB_USER_AGENT`: User agent for web requests (default: "DocsRef-MCP/1.0")
- `WEB_TIMEOUT`: Request timeout in seconds (default: 30)
- `WEB_MAX_RETRIES`: Maximum retry attempts (default: 3)

## Features

### Smart HTML to Markdown Conversion
- Extracts main content from web pages
- Removes navigation, ads, and other non-content elements
- Preserves important metadata (title, author, description)
- Handles various HTML structures gracefully

### Caching System
- Automatically caches converted pages for 7 days
- Reduces redundant downloads
- Organized file structure based on URLs
- Easy cache management with list and clear operations

### Error Handling
- Automatic retry with exponential backoff
- Graceful handling of timeouts and network errors
- Clear error messages for troubleshooting

### Curated Resources
The `SearchRefactoringResources` tool provides access to high-quality sources:
- Refactoring.guru for refactoring techniques and code smells
- Martin Fowler's refactoring catalog
- Design pattern resources
- SOLID principles documentation

## Use Cases

1. **Learning Refactoring Techniques**: Download and study refactoring patterns offline
2. **Building a Knowledge Base**: Create a local repository of coding best practices
3. **Team Documentation**: Share curated resources with your development team
4. **Offline Access**: Cache important documentation for offline reference

## Example Workflow

```bash
# Search for code smell resources
SearchRefactoringResources(query: "code smells")

# Fetch a specific page about long methods
FetchWebPage(url: "https://refactoring.guru/refactoring/smells/long-method")

# Batch download multiple related pages
FetchWebPageBatch(urls: "https://refactoring.guru/refactoring/smells/long-method,https://refactoring.guru/refactoring/techniques/extract-method,https://refactoring.guru/refactoring/techniques/replace-temp-with-query")

# List what's been cached
ListCachedPages()

# Clean up old cache entries
ClearWebCache(clearAll: false)
```