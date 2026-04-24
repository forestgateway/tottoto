# GitHub Copilot カスタム指示 — todochart

<!-- アーキテクチャ・クラス一覧の詳細は下記ファイルを参照: Refer to [CONTRIBUTING.md](../src/CONTRIBUTING.md) -->

## ⚠️ 作業開始前に必ず行うこと

1. `src/CONTRIBUTING.md` を読み、アーキテクチャ・クラス一覧・責務ルールを把握すること。
2. 実装完了後、新しいクラス・機能・ルールを追加・変更した場合は **必ず `src/CONTRIBUTING.md` を更新**すること。

---

## プロジェクト概要

- **アプリ種別**: WPF / .NET 8 / タスク管理 & ガントチャート（Windows デスクトップ）
- **アーキテクチャ**: MVVM
- **言語**: C#（nullable 有効）、XAML
- **名前空間ルート**: `todochart`

---

## ディレクトリ構成と責務

| ディレクトリ | 責務 | 禁止事項 |
|---|---|---|
| `src/Models/` | データ構造・ステータス計算ロジック | UI 参照・Service 呼び出し |
| `src/ViewModels/` | UI 状態管理・コマンド定義・FlatList 生成 | 直接ファイル I/O |
| `src/Views/` | XAML バインディング・ウィンドウ定義 | ビジネスロジック記述 |
| `src/Services/` | ファイル I/O・外部 API 通信・設定永続化 | UI 参照 |
| `src/Controls/` | カスタム描画コントロール（DrawingContext 使用） | ビジネスロジック記述 |
| `src/Converters/` | XAML バインディング用 IValueConverter 実装 | 副作用を持つ処理 |
| `src/Themes/` | スタイル・テーマ定義（BaseTheme.xaml など） | ロジック |

---

## コーディング規約

- private フィールドは `_camelCase` プレフィックス
- コマンドは `ICommand`（`RelayCommand`）で定義し ViewModel に配置
- XAML code-behind はイベントの View への委譲のみ（ビジネスロジック禁止）
- `ObservableCollection` は ViewModel のみで使用
- `sealed record` / `record` を表示専用データに積極活用
- null 許容参照型（`nullable enable`）を使用

---

## CONTRIBUTING.md メンテナンスルール

以下に該当する変更を行った場合は **必ず `CONTRIBUTING.md` の該当テーブルを更新**すること：

| 変更内容 | 更新箇所 |
|---|---|
| 新しいクラスを追加 | `src/CONTRIBUTING.md` の該当レイヤーの「主要クラス一覧」テーブルに追記 |
| クラスを削除・リネーム | 該当行を削除・更新 |
| 新機能・新サービスを追加 | 説明を追記 |
| アーキテクチャルールを変更 | 「レイヤー責務ルール」表を更新 |
