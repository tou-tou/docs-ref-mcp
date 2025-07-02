# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a C# MCP (Model Context Protocol) server that provides efficient access to documentation and source code from multiple git repositories managed as submodules. The server implements smart filtering to extract source files while excluding build artifacts.

## Architecture

The project follows a modular architecture with HTTP-based MCP implementation:

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

## Build and Run Commands

```bash
# Build the project
dotnet build DocsMcpServer.csproj

# Run the server (default port 7334)
dotnet run

# Run with custom settings
DOCS_FOLDERS=repos/UniVRM MCP_PORT=8080 dotnet run

# Publish self-contained executable
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ./publish
```

## Environment Variables

Critical variables that affect server behavior:

- `DOCS_BASE_DIR`: Base directory for documents (default: current directory)
- `DOCS_SMART_FILTER`: Enable smart filtering (default: true) - extracts source files regardless of .gitignore
- `DOCS_RESPECT_GITIGNORE`: Respect .gitignore patterns (default: true)
- `DOCS_FOLDERS`: Comma-separated folders to load (default: all folders in docs/)
- `MCP_PORT`: Server port (default: 7334)

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
pkill -f DocsMcpServer && dotnet run
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
3. Server logs to console - use `Logging__LogLevel__Default=Debug` for verbose output
4. The server automatically detects and excludes binary files and build artifacts