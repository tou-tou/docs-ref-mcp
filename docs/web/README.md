# Web Documents Directory

このディレクトリには、Webから取り込んだドキュメント（主にリファクタリングやコードスメルに関する参考資料）がMarkdown形式で保存されます。

## ディレクトリ構造

```
web/
├── refactoring/          # リファクタリングテクニックに関する資料
├── code-smells/          # コードスメルの識別と対処法
├── design-patterns/      # デザインパターンの解説
├── clean-code/           # クリーンコードの原則
└── design-principles/    # SOLID原則などの設計原則
```

## 使用方法

### 1. おすすめリソースの確認

```
suggest_refactoring_resources()
```

このコマンドで、よく知られているリファクタリング資料のURLリストが表示されます。

### 2. Webページの取り込み

**単一ページ:**
```
fetch_web_page("https://refactoring.guru/refactoring/smells", "code-smells")
```

**複数ページ:**
```
fetch_web_pages_batch("URL1,URL2,URL3", "category")
```

### 3. 取り込み済みドキュメントの確認

```
list_web_docs()              # すべてのWebドキュメント
list_web_docs("code-smells") # 特定カテゴリのみ
```

### 4. ドキュメントの閲覧

```
GetDoc("docs/web/code-smells/refactoring.guru-refactoring-smells.md")
```

## 特徴

- **自動クリーニング**: 広告、ナビゲーション、サイドバーなどを自動的に除去
- **メタデータ**: 各ファイルの先頭にソースURLとダウンロード日時を記録
- **オフライン対応**: 一度取り込めばインターネット接続なしで参照可能
- **カテゴリ管理**: 関連するドキュメントをカテゴリ別に整理

## ファイル命名規則

ファイル名は以下の規則で自動生成されます：
- `{ドメイン名}-{パスの一部}.md`
- 例: `refactoring.guru-refactoring-smells.md`

## 注意事項

- このディレクトリ内のファイルは自動生成されるため、直接編集しないでください
- 元のWebページが更新された場合は、再度 `fetch_web_page` で取り込み直してください
- 著作権に配慮し、個人的な学習・参照目的でのみ使用してください