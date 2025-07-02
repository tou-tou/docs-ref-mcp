# Claude CodeでDocsMcpServerを使用する方法

## 方法1: dotnet runを使用（開発時）

```bash
# MCP HTTPサーバーを追加
claude mcp add --transport http docs-csharp http://localhost:5000/mcp

# 別のターミナルでサーバーを起動
cd /path/to/DocsMcpServer
DOCS_BASE_DIR=/path/to/your/docs dotnet run
```

## 方法2: 自己完結型実行ファイルを作成

### 1. 実行ファイルをビルド

```bash
cd DocsMcpServer

# Linux/macOS用
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ./publish/linux

# Windows用
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish/win

# macOS用
dotnet publish -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -o ./publish/osx
```

### 2. 実行権限を付与（Linux/macOS）

```bash
chmod +x ./publish/linux/DocsMcpServer
```

### 3. Claude Codeに登録

```bash
# 実行ファイルを直接指定
claude mcp add docs-csharp -e DOCS_BASE_DIR=/path/to/your/docs -- /path/to/DocsMcpServer/publish/linux/DocsMcpServer

# 環境変数を複数設定する場合
claude mcp add docs-csharp \
  -e DOCS_BASE_DIR=/path/to/your/docs \
  -e DOCS_FOLDERS=api,guides \
  -e DOCS_MAX_CHARS_PER_PAGE=5000 \
  -- /path/to/DocsMcpServer/publish/linux/DocsMcpServer
```

## 方法3: シェルスクリプトを使用

### 1. 起動スクリプトを作成

```bash
cat > start-docs-mcp.sh << 'EOF'
#!/bin/bash
cd /path/to/DocsMcpServer
export DOCS_BASE_DIR="${DOCS_BASE_DIR:-/path/to/default/docs}"
dotnet run --urls "http://localhost:5000"
EOF

chmod +x start-docs-mcp.sh
```

### 2. Claude Codeに登録

```bash
claude mcp add docs-csharp -- /path/to/start-docs-mcp.sh
```

## 方法4: Dockerを使用

### 1. Dockerfileを作成

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY ./publish/linux .
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["./DocsMcpServer"]
```

### 2. Dockerイメージをビルド

```bash
docker build -t docs-mcp-server .
```

### 3. Claude Codeに登録

```bash
claude mcp add docs-csharp \
  -e DOCS_BASE_DIR=/docs \
  -- docker run -p 5000:5000 -v /path/to/your/docs:/docs docs-mcp-server
```

## 登録確認とテスト

```bash
# 登録されたサーバーを確認
claude mcp list

# サーバーの詳細を確認
claude mcp get docs-csharp

# Claude Code内でテスト
claude
> /mcp  # MCPサーバーの状態を確認
```

## トラブルシューティング

### サーバーが起動しない場合

1. ポート5000が使用されていないか確認
```bash
lsof -i :5000  # Linux/macOS
netstat -ano | findstr :5000  # Windows
```

2. 別のポートを使用
```bash
# appsettings.jsonを編集してポートを変更
# または環境変数で指定
ASPNETCORE_URLS=http://localhost:5001 dotnet run
```

3. Claude Codeの登録を更新
```bash
claude mcp remove docs-csharp
claude mcp add --transport http docs-csharp http://localhost:5001/mcp
```

### ログを確認

```bash
# サーバーのログを確認
LOGGING__LOGLEVEL__DEFAULT=Debug dotnet run

# Claude Codeのログを確認
claude --log-level debug
```

## プロジェクトスコープでの共有

チームでMCP設定を共有する場合：

```bash
# プロジェクトスコープで追加
claude mcp add docs-csharp -s project \
  -e DOCS_BASE_DIR=./docs \
  -- dotnet run --project ./DocsMcpServer

# .mcp.jsonファイルが作成される
cat .mcp.json
```

これで、チームメンバー全員が同じMCP設定を使用できます。