# Manual Documentation Directory

このディレクトリは、git submodule で管理されない手動管理のドキュメントを配置するための場所です。

## 用途

### 推奨される使用例
- プロジェクト固有のメモや設計書
- 内部向けドキュメント
- 設定ファイルのサンプル
- API利用例やコードスニペット
- チームメンバー向けのガイドライン

### ファイル形式
以下の形式のファイルが自動的に認識されます：
- Markdown (`.md`, `.mdx`)
- テキスト (`.txt`)
- 設定ファイル (`.json`, `.yaml`, `.yml`, `.toml`)
- コードサンプル (`.cs`, `.js`, `.py` など)

## 構成例

```
manual/
├── README.md           # このファイル
├── mcp.md             # MCP に関する説明
├── api-examples/      # API 利用例
│   ├── basic.cs
│   └── advanced.cs
├── configs/           # 設定サンプル
│   ├── sample.json
│   └── config.yaml
└── guides/            # ガイドドキュメント
    ├── setup.md
    └── troubleshooting.md
```

## ベストプラクティス

1. **明確な命名**: ファイル名は内容を表す分かりやすい名前を使用
2. **適切な階層化**: 関連するドキュメントはサブディレクトリにまとめる
3. **メタデータの活用**: 必要に応じて docs_metadata.json に説明を追加
4. **定期的な整理**: 不要になったドキュメントは削除または archive/ に移動

## 注意事項

- このディレクトリの内容は git で管理されます
- 機密情報を含むドキュメントは配置しないでください
- 大きなバイナリファイルは避けてください（1MB以上は自動的に除外されます）