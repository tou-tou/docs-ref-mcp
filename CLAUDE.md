# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a C# MCP (Model Context Protocol) server that provides efficient access to documentation and source code from multiple git repositories managed as submodules. The server implements smart filtering to extract source files while excluding build artifacts.

## Architecture

The project follows a modular architecture with HTTP-based MCP implementation and CLI support:

### Core Components

1. **StreamableMcpServerApplication** (`Server/`)
   - HTTP listener on port 7334 (configurable via MCP_PORT)
   - Pipe-based streaming for efficient communication
   - Dynamic client registration support

2. **DocumentManager** (`Core/`)
   - Manages document loading and caching
   - Implements smart filtering with GitIgnoreParser
   - Handles pagination for large files (>15KB)
   - Binary file detection using null byte and non-ASCII character analysis

3. **DocTools** (`Tools/`)
   - MCP tool implementations with [McpServerTool] attributes
   - Tools: ListDocs, ListDocsSummary, ListDocsTree, GetDoc, GrepDocs
   - Returns summaries by default to avoid token limits

4. **WebTools** (`Tools/`)
   - Web page fetching and conversion tools
   - Tools: fetch_web_page, fetch_web_pages_batch, list_web_docs, suggest_refactoring_resources
   - Converts HTML to clean Markdown for offline reference

5. **WebPageDownloader** (`Core/`)
   - HTTP client with retry logic and timeout handling
   - Supports gzip/deflate compression
   - Configurable via environment variables

6. **HtmlToMarkdownConverter** (`Core/`)
   - Uses HtmlAgilityPack and ReverseMarkdown libraries
   - Removes ads, navigation, and other unwanted elements
   - Extracts main content from article/main tags

### CLI Components

7. **CliCommands** (`Cli/`)
   - Root command and subcommand definitions using System.CommandLine
   - Handles command routing and argument parsing
   - Supports both text and JSON output formats

8. **WebCommands** (`Cli/`)
   - CLI wrapper for WebTools functionality
   - Handles web page fetching operations from command line
   - Formats output for console display

9. **DocCommands** (`Cli/`)
   - CLI wrapper for DocTools functionality
   - Provides document search and retrieval from command line
   - Supports JSON output for scripting

## Build and Run Commands

```bash
# Build the project
dotnet build DocsRef.csproj

# Run in server mode (default when no arguments)
dotnet run

# Run in CLI mode
dotnet run -- web suggest
dotnet run -- docs grep "pattern"

# Run with custom settings
DOCS_FOLDERS=repos/UniVRM MCP_PORT=8080 dotnet run

# Publish self-contained executable
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ./publish

# Use published executable
./publish/DocsRef web fetch https://example.com/article --category tutorials
```

## Environment Variables

Critical variables that affect server behavior:

- `DOCS_BASE_DIR`: Base directory for documents (default: current directory)
- `DOCS_SMART_FILTER`: Enable smart filtering (default: true) - extracts source files regardless of .gitignore
- `DOCS_RESPECT_GITIGNORE`: Respect .gitignore patterns (default: true)
- `DOCS_FOLDERS`: Comma-separated folders to load (default: all folders in docs/)
- `MCP_PORT`: Server port (default: 7334)
- `WEB_CACHE_DIR`: Directory for web documents (default: docs/web)
- `WEB_USER_AGENT`: User agent for HTTP requests
- `WEB_TIMEOUT`: HTTP request timeout in seconds (default: 30)
- `WEB_MAX_RETRIES`: Maximum retry attempts (default: 3)

## Managing Submodules

The `docs/repos/` directory contains git submodules for:
- R3, UniTask, VContainer, UniVRM, vrm-specification

```bash
# Update all submodules
./scripts/update-docs.sh  # Unix/Linux/Mac
.\scripts\update-docs.ps1 # Windows

# Add new submodule
git submodule add https://github.com/owner/repo.git docs/repos/repo-name
```

## Smart Filtering Logic

The GitIgnoreParser implements smart filtering that:
1. Always includes source file extensions (.cs, .js, .py, etc.)
2. Always excludes build outputs (bin/, obj/, build/, dist/)
3. Always excludes dev tools (node_modules/, .vs/, .idea/)
4. Detects binary files by checking for null bytes and >30% non-ASCII characters

When `DOCS_SMART_FILTER=true` (default), source files are included even if .gitignore would exclude them.

## Claude Code Integration

```bash
# Add MCP server to Claude Code
claude mcp add --transport http docs-ref http://127.0.0.1:7334/mcp/

# For development with automatic restart
pkill -f DocsRef && dotnet run
```

## Token Limit Handling

The server implements several strategies to handle large repositories:
- ListDocs without parameters returns a summary instead of full file list
- Pagination for files >15KB (10KB per page)
- Filtering with pattern matching (e.g., `*.cs`, `repos/UniVRM/**/*.shader`)
- Maximum 100 results by default in filtered searches

## Project File Exclusions

The .csproj explicitly excludes `docs/**` from compilation to prevent submodule code from being compiled. This includes:
- All .cs, .csproj, .sln files in docs/
- All bin/ and obj/ directories in docs/

## Development Workflow

1. Submodules are readonly references - don't modify files in `docs/repos/`
2. Manual documentation goes in `docs/manual/`
3. Web-sourced documentation goes in `docs/web/` organized by category
4. Server logs to console - use `Logging__LogLevel__Default=Debug` for verbose output
5. The server automatically detects and excludes binary files and build artifacts

## Web Tools Usage

The web tools allow downloading and converting web pages to Markdown for offline reference:

1. **Check suggested resources**: `suggest_refactoring_resources()`
2. **Download single page**: `fetch_web_page(url, category, tags)`
3. **Download multiple pages**: `fetch_web_pages_batch(urls, category)`
4. **List downloaded docs**: `list_web_docs(category)`
5. **Read downloaded doc**: `GetDoc("docs/web/category/filename.md")`

Downloaded documents include metadata (source URL, download date) and are cleaned of ads/navigation.

## CLI Mode Usage

The application supports dual-mode operation:
- **No arguments**: Runs as MCP server (default)
- **With arguments**: Runs as CLI tool

### CLI Commands

```bash
# Web operations
docsref web fetch <url> [--category <cat>] [--tags <tags>] [--output json]
docsref web fetch-batch <urls> [--category <cat>] [--output json]
docsref web list [--category <cat>] [--output json]
docsref web suggest [--output json]

# Document operations
docsref docs list [--pattern <pat>] [--directory <dir>] [--max <n>] [--output json]
docsref docs summary [--output json]
docsref docs tree [--directory <dir>] [--depth <d>] [--output json]
docsref docs get <path> [--page <p>] [--output json]
docsref docs grep <pattern> [--case-sensitive] [--output json]

# Server mode
docsref server [--port <port>]
```

### Scripting with JSON Output

The `--output json` flag enables structured output for scripting:

```bash
# Extract file names from search results
dotnet run -- docs grep "TODO" --output json | jq '.files[].file'

# Get all web documents in a category
dotnet run -- web list --category "refactoring" --output json | jq '.files[]'
```

### Shell Script Examples

See `scripts/examples/` for practical examples:
- `fetch-refactoring-resources.sh` - Batch download refactoring materials
- `search-async-patterns.sh` - Search for async patterns in code
- `batch-operations.sh` - Complex batch processing example