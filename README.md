# Docs MCP Server

ドキュメントやソースコードを効率的に参照するための MCP (Model Context Protocol) サーバーです。

## 特徴

- 📚 **大規模リポジトリ対応**: git submodule による複数リポジトリの一元管理
- 🔍 **スマートフィルタリング**: ソースファイルを自動抽出し、ビルド成果物を除外
- ⚡ **高速検索**: 正規表現による全文検索とフィルタリング機能
- 📄 **ページネーション**: 大きなファイルの効率的な処理
- 🛠️ **柔軟な設定**: 環境変数による細かい動作制御
- 🌐 **Webページ取り込み**: リファクタリングやコードスメルの参考資料をMarkdownに変換して保存

## 構成

```
docs-mcp/
├── Core/                       # ドキュメント管理のコアロジック
│   ├── DocumentManager.cs      # ドキュメントの読み込みとキャッシュ管理
│   ├── GitIgnoreParser.cs      # .gitignore パターンの解析
│   ├── WebPageDownloader.cs    # HTTPリクエスト処理
│   ├── HtmlToMarkdownConverter.cs # HTML→Markdown変換
│   └── Models/                 # データモデル
│       └── WebDocument.cs      # Webドキュメントモデル
├── Server/                     # MCP サーバー実装
│   └── StreamableMcpServerApplication.cs
├── Tools/                      # MCP ツール実装
│   ├── DocTools.cs             # ドキュメント管理ツール
│   └── WebTools.cs             # Webページ取り込みツール
├── Cli/                        # CLI実装
│   ├── CliCommands.cs          # コマンド定義
│   ├── WebCommands.cs          # Web操作のCLI
│   └── DocCommands.cs          # ドキュメント操作のCLI
├── Properties/                 # プロジェクト設定
├── docs/                       # ドキュメントルート
│   ├── repos/                  # Git submodule で管理されるリポジトリ
│   │   ├── R3/
│   │   ├── UniTask/
│   │   ├── VContainer/
│   │   ├── UniVRM/
│   │   └── vrm-specification/
│   ├── manual/                 # 手動で管理するドキュメント
│   └── web/                    # Webから取り込んだドキュメント
├── scripts/                    # ユーティリティスクリプト
│   ├── update-docs.sh          # Unix/Linux用 submodule 更新
│   ├── update-docs.ps1         # Windows用 submodule 更新
│   └── examples/               # CLI使用例のスクリプト
│       ├── fetch-refactoring-resources.sh
│       ├── search-async-patterns.sh
│       └── batch-operations.sh
├── DocsRef.csproj              # プロジェクトファイル
├── docs-mcp.sln                # ソリューションファイル
├── Program.cs                  # エントリーポイント
└── README.md                   # このファイル
```

## セットアップ

### 1. 必要要件

- .NET 8.0 SDK
- Git

### 2. リポジトリのクローン

```bash
git clone --recursive https://github.com/yourusername/docs-mcp.git
cd docs-mcp
```

`--recursive` オプションにより、submodule も同時にクローンされます。

### 3. 既存のリポジトリをクローンした場合

```bash
# Submodule を初期化して取得
git submodule init
git submodule update
```

### 4. MCP サーバーの起動

```bash
dotnet run
```

デフォルトで `http://127.0.0.1:7334/mcp/` で起動します。

## 環境変数

| 環境変数 | 説明 | デフォルト値 |
|---------|------|-------------|
| `MCP_PORT` | サーバーのポート番号 | 7334 |
| `DOCS_BASE_DIR` | ドキュメントベースディレクトリ | カレントディレクトリ |
| `DOCS_FOLDERS` | 読み込むフォルダ（カンマ区切り） | すべてのフォルダ |
| `DOCS_FILE_EXTENSIONS` | 対象ファイル拡張子（カンマ区切り） | プログラミング言語、設定ファイルなど |
| `DOCS_EXCLUDE_PATTERNS` | 除外パターン（カンマ区切り） | .git/, node_modules/, bin/, obj/ など |
| `DOCS_MAX_FILE_SIZE` | 最大ファイルサイズ（バイト） | 1048576 (1MB) |
| `DOCS_RESPECT_GITIGNORE` | .gitignore を尊重するか | true |
| `DOCS_SMART_FILTER` | スマートフィルタを使用（ソースファイルのみ抽出） | true |
| `DOCS_MAX_CHARS_PER_PAGE` | ページあたりの最大文字数 | 10000 |
| `DOCS_LARGE_FILE_THRESHOLD` | 大きいファイルの閾値 | 15000 |
| `WEB_CACHE_DIR` | Webドキュメントの保存先 | docs/web |
| `WEB_USER_AGENT` | HTTP リクエストのUser-Agent | Mozilla/5.0... |
| `WEB_TIMEOUT` | HTTPリクエストのタイムアウト（秒） | 30 |
| `WEB_MAX_RETRIES` | HTTPリクエストの最大リトライ回数 | 3 |

### 使用例

特定のリポジトリのみを読み込む：
```bash
DOCS_FOLDERS=repos/UniVRM dotnet run
```

スマートフィルタを無効化：
```bash
DOCS_SMART_FILTER=false dotnet run
```

異なるポートで起動：
```bash
MCP_PORT=8080 dotnet run
```

## CLI モード（コマンドライン）

DocsRef は MCP サーバーとしてだけでなく、強力なコマンドラインツールとしても使用できます。

### 基本的な使い方

```bash
# ヘルプを表示
dotnet run -- --help

# Webページを取り込む
dotnet run -- web fetch https://example.com/article --category tutorials

# ドキュメントを検索
dotnet run -- docs grep "async" --output json

# サーバーモードで起動（デフォルト）
dotnet run
# または明示的に
dotnet run -- server --port 7334
```

### 実行可能ファイルとして発行

```bash
# Linux/Mac用
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o ./publish

# Windows用
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o ./publish

# 使用例
./publish/DocsRef web fetch https://example.com/article
```

### CLI コマンド一覧

#### Web コマンド

```bash
# 単一ページの取り込み
docsref web fetch <url> [--category <category>] [--tags <tags>] [--output json]

# 複数ページの一括取り込み
docsref web fetch-batch <url1,url2,...> [--category <category>] [--output json]

# 取り込み済みドキュメントの一覧
docsref web list [--category <category>] [--output json]

# おすすめリソースの表示
docsref web suggest [--output json]
```

#### Docs コマンド

```bash
# ドキュメント一覧
docsref docs list [--pattern <pattern>] [--directory <dir>] [--max <count>] [--output json]

# リポジトリ統計
docsref docs summary [--output json]

# ディレクトリツリー表示
docsref docs tree [--directory <dir>] [--depth <depth>] [--output json]

# ドキュメント取得
docsref docs get <path> [--page <page>] [--output json]

# 正規表現検索
docsref docs grep <pattern> [--case-sensitive] [--output json]
```

### シェルスクリプトでの活用例

提供されているサンプルスクリプト：

```bash
# リファクタリング資料を一括ダウンロード
./scripts/examples/fetch-refactoring-resources.sh

# 非同期パターンを検索
./scripts/examples/search-async-patterns.sh

# バッチ処理の例
./scripts/examples/batch-operations.sh
```

### JSON 出力の活用

```bash
# jq と組み合わせて使用
docsref docs grep "TODO" --output json | jq '.files[].file'

# PowerShell での例
docsref docs list --pattern "*.cs" --output json | ConvertFrom-Json | Select-Object -ExpandProperty files

# 結果をファイルに保存
docsref web list --output json > web-docs.json
```

## Claude Code での接続

```bash
claude mcp add --transport http docs-ref http://127.0.0.1:7334/mcp/
```

**注意**: ポート番号 7334 は "REF" (Reference) を表しており、ドキュメント参照サーバーであることを示唆しています。

## 利用可能な MCP ツール

### スマートフィルタリング

デフォルトで有効になっているスマートフィルタリングにより：

- **ソースファイルの自動抽出**: .gitignore の設定に関わらず、ソースコード（.cs, .js, .py など）は常に含まれます
- **ビルド成果物の除外**: bin/, obj/, build/, dist/ などのディレクトリは自動的に除外されます
- **開発ツールファイルの除外**: node_modules/, .vs/, .idea/ などは常に除外されます

### 1. ListDocs - ドキュメント一覧（フィルタリング対応）

```typescript
ListDocs(pattern?: string, directory?: string, maxResults?: number)
```

- **pattern**: ファイルパスパターン（例: `"*.cs"`, `"repos/UniVRM/**/*.shader"`）
- **directory**: 検索対象ディレクトリ（例: `"repos/R3"`）
- **maxResults**: 最大結果数（デフォルト: 100）

### 2. ListDocsSummary - リポジトリ統計

```typescript
ListDocsSummary()
```

各リポジトリのファイル数と拡張子別の統計を表示します。

### 3. ListDocsTree - ディレクトリツリー表示

```typescript
ListDocsTree(directory?: string, maxDepth?: number)
```

- **directory**: ルートディレクトリ（例: `"repos/UniVRM"`）
- **maxDepth**: 表示する最大深度（デフォルト: 3）

### 4. GetDoc - ドキュメント取得（ページネーション対応）

```typescript
GetDoc(path: string, page?: number)
```

大きなファイルは自動的にページ分割されます。

### 5. GrepDocs - 正規表現検索

```typescript
GrepDocs(pattern: string, ignoreCase?: boolean)
```

ドキュメント内を正規表現で検索します。

### 6. Webページ取り込みツール

#### fetch_web_page - 単一ページの取り込み

```typescript
fetch_web_page(url: string, category?: string, tags?: string)
```

- **url**: 取り込むWebページのURL
- **category**: カテゴリ（例: `"refactoring"`, `"code-smells"`）
- **tags**: タグ（カンマ区切り）

例：
```
fetch_web_page("https://refactoring.guru/refactoring/smells", "code-smells")
```

#### fetch_web_pages_batch - 複数ページの一括取り込み

```typescript
fetch_web_pages_batch(urls: string, category?: string)
```

- **urls**: カンマ区切りのURL一覧
- **category**: すべてのドキュメントに適用するカテゴリ

#### list_web_docs - 取り込み済みドキュメントの一覧

```typescript
list_web_docs(category?: string)
```

カテゴリでフィルタリングして、取り込み済みのWebドキュメントを一覧表示します。

#### suggest_refactoring_resources - おすすめリソース

```typescript
suggest_refactoring_resources()
```

リファクタリングやコードスメルについての人気リソースを提案します。

## Submodule の管理

### すべての submodule を最新に更新

```bash
# Unix/Linux/Mac
./scripts/update-docs.sh

# Windows
.\scripts\update-docs.ps1
```

### 新しいリポジトリを追加

```bash
git submodule add https://github.com/example/repo.git docs/repos/repo-name
git commit -m "Add repo-name documentation"
```

### 特定の submodule を更新

```bash
cd docs/repos/R3
git pull origin main
cd ../..
git add docs/repos/R3
git commit -m "Update R3 to latest version"
```

## 現在管理されているリポジトリ

- **R3**: Reactive Extensions for .NET (https://github.com/Cysharp/R3)
- **UniTask**: Provides an efficient allocation free async/await integration for Unity (https://github.com/Cysharp/UniTask)
- **VContainer**: Fast and lightweight dependency injection container for Unity (https://github.com/hadashiA/VContainer)
- **UniVRM**: VRM implementation for Unity (https://github.com/vrm-c/UniVRM)
- **vrm-specification**: VRM format specification (https://github.com/vrm-c/vrm-specification)

## Webドキュメントの管理

### リファクタリング資料の取り込み

Webページ取り込み機能を使用して、リファクタリングやコードスメルに関する参考資料をローカルに保存できます。

#### 使用例

1. **推奨リソースを確認**
```
suggest_refactoring_resources()
```

2. **単一ページの取り込み**
```
fetch_web_page("https://refactoring.guru/refactoring/smells", "code-smells")
```

3. **複数ページの一括取り込み**
```
fetch_web_pages_batch("https://refactoring.guru/refactoring/smells,https://refactoring.guru/refactoring/techniques", "refactoring")
```

4. **取り込み済みドキュメントの確認**
```
list_web_docs()
list_web_docs("code-smells")  # カテゴリでフィルタリング
```

5. **取り込んだドキュメントの閲覧**
```
GetDoc("docs/web/code-smells/refactoring.guru-refactoring-smells.md")
```

### 保存構造

取り込まれたWebドキュメントは以下の構造で保存されます：

```
docs/web/
├── refactoring/          # リファクタリングテクニック
├── code-smells/          # コードスメル
├── design-patterns/      # デザインパターン
├── clean-code/           # クリーンコード
└── design-principles/    # 設計原則
```

### 特徴

- **自動クリーニング**: 広告、ナビゲーション、コメントセクションを自動除去
- **メタデータ保存**: ソースURL、ダウンロード日時を記録
- **カテゴリ分類**: ドキュメントをカテゴリ別に整理
- **オフライン参照**: 一度取り込めばインターネット接続なしで参照可能

## 技術仕様

### アーキテクチャ

#### コア コンポーネント

1. **StreamableMcpServerApplication**
   - MCP (Model Context Protocol) の HTTP サーバー実装
   - パイプストリーミングによる効率的な通信
   - 動的クライアント登録のサポート

2. **DocumentManager**
   - ドキュメントの読み込みとキャッシュ管理
   - ページネーション機能
   - フィルタリングと検索機能

3. **GitIgnoreParser**
   - .gitignore パターンの解析と適用
   - スマートフィルタリング機能
   - バイナリファイルの自動検出

4. **WebPageDownloader**
   - HTTPリクエストの処理とリトライロジック
   - 圧縮コンテンツの自動展開
   - タイムアウト管理

5. **HtmlToMarkdownConverter**
   - HTMLからMarkdownへの変換
   - 不要な要素の自動除去
   - メインコンテンツの抽出

### スマートフィルタリング詳細

#### ファイル除外ロジック
1. 常に除外されるパターン（AlwaysExcludePatterns）
2. .gitignore ファイルのパターン
3. ビルド出力ディレクトリの検出
4. バイナリファイルの検出

#### バイナリファイル検出アルゴリズム
- NULL バイト（0x00）の存在チェック
- 非ASCII文字の割合（30%以上でバイナリと判定）
- ファイルの最初の8KBをサンプリング

### パフォーマンス最適化

- 大規模ファイルのスキップ（デフォルト: 1MB以上）
- 効率的なメモリ使用のためのストリーミング処理
- インメモリキャッシュによる高速アクセス

### 拡張ポイント

#### 新しいツールの追加
1. `DocTools` クラスに新しいメソッドを追加
2. `[McpServerTool]` 属性を付与
3. 適切な説明を `[Description]` 属性で提供

#### カスタムフィルターの実装
1. `GitIgnoreParser` を拡張
2. 新しいパターンマッチングロジックを追加
3. `DocumentManager` で使用

## トラブルシューティング

### サーバーが起動しない
- .NET 8.0 SDK がインストールされているか確認
- ポート 7334 が使用されていないか確認

### ドキュメントが表示されない
- `DOCS_BASE_DIR` が正しく設定されているか確認
- `docs/` フォルダが存在するか確認
- ファイル拡張子がサポートされているか確認

### 接続できない
- サーバーが起動しているか確認（`http://127.0.0.1:7334/mcp/` にアクセス）
- Claude Code での MCP 設定を確認：`claude mcp`

### サーバーの再起動

```bash
# 既存のプロセスを終了
pkill -f DocsRef

# サーバーを再起動
dotnet run
```

## ライセンス

MIT License