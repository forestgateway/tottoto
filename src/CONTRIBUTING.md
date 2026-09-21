# CONTRIBUTING.md — Tottoto (todochart) 開発ガイドライン

> **⚠️ 機能追加・修正の前に必ずこのファイルを読むこと。**
> 変更後は「主要クラス一覧」を必ず更新すること。

---

## アーキテクチャ概要

```
WPF / .NET 8 / MVVM パターン
名前空間ルート: todochart
```

```
src/
├── Models/          # データ構造・ステータス計算ロジック
├── ViewModels/      # UI 状態管理・コマンド定義・FlatList 生成
├── Views/           # XAML UI 定義 + code-behind（最小限）
├── Services/        # ファイル I/O・外部 API・設定永続化
├── Controls/        # DrawingContext による高速描画カスタムコントロール
├── Converters/      # XAML バインディング用 IValueConverter
├── Themes/          # スタイル・テーマ定義
└── App.xaml         # アプリケーションエントリポイント
```

---

## レイヤー責務ルール

| レイヤー | 責務 | 禁止事項 |
|---|---|---|
| `Models` | データ構造・ステータス計算 | UI 参照・Service 呼び出し |
| `ViewModels` | UI 状態管理・コマンド定義・FlatList 生成 | 直接ファイル I/O |
| `Views` | XAML バインディング・ウィンドウ定義 | ビジネスロジック記述 |
| `Services` | ファイル I/O・外部 API 通信・設定永続化 | UI 参照 |
| `Controls` | カスタム描画コントロール（DrawingContext 使用） | ビジネスロジック記述 |
| `Converters` | XAML バインディング用 IValueConverter 実装 | 副作用を持つ処理 |
| `Themes` | スタイル・テーマ定義 | ロジック |

---

## 主要クラス一覧

### Models

| クラス / 型 | ファイル | 役割 |
|---|---|---|
| `ScheduleItemBase` | `Models/ScheduleItemBase.cs` | ToDo・フォルダ共通の基底クラス。ステータス計算ロジックを持つ |
| `ScheduleToDo` | `Models/ScheduleToDo.cs` | 葉ノード（ToDoタスク）。`Completed` フラグを持つ |
| `ScheduleFolder` | `Models/ScheduleFolder.cs` | フォルダノード。子アイテムを持ち、子の状態から自身のステータスを集約 |
| `ItemStatus` | `Models/ItemStatus.cs` | タスクの状態を表す enum（Wait / Progress / Alert / Overdue / Complete など） |
| `ArchivedItem` | `Models/ArchivedItem.cs` | アーカイブ済みタスクのデータ構造 |
| `Callout` | `Models/Callout.cs` | タスクに付与する吹き出し注釈のデータ構造 |
| `CalloutStyle` | `Models/CalloutStyle.cs` | 吹き出しの表示スタイルを表す enum |
| `CalloutPositionMode` | `Models/CalloutPositionMode.cs` | 吹き出し配置方法を表す enum |
| `CalloutVisibilityMode` | `Models/CalloutVisibilityMode.cs` | 吹き出し表示条件を表す enum |
| `IssueCacheItem` | `Models/IssueCacheItem.cs` | Issue Tracking から取得した Issue のキャッシュデータ |
| `IssueTrackingSettings` | `Models/IssueTrackingSettings.cs` | Issue Tracking 連携の設定値（URL・トークン等） |
| `HolidayData` | `Models/HolidayData.cs` | 休日設定で使用する個別休日データ（Date/Name/Level）の表示・編集用レコード |
| `HolidayYearOption` | `Models/HolidayYearOption.cs` | 祝日反映対象の西暦とチェック状態を保持する選択オプション |
| `RecurrenceRule` | `Models/RecurrenceRule.cs` | 繰り返し予定の設定（頻度・間隔・曜日・月内日付・休日重複時の挙動）を保持。`ScheduleToDo` が任意で保持する |
| `RecurrenceFrequency` | `Models/RecurrenceFrequency.cs` | 繰り返しの頻度を表す enum（Daily / Weekly / Monthly） |
| `RecurrenceHolidayShift` | `Models/RecurrenceHolidayShift.cs` | 繰り返しの各回が休日と重なった場合の挙動を表す enum（None / Before / After） |

### ViewModels

| クラス / 型 | ファイル | 役割 |
|---|---|---|
| `ViewModelBase` | `ViewModels/ViewModelBase.cs` | `INotifyPropertyChanged` 基底クラス。`SetField` ヘルパーを提供 |
| `RelayCommand` | `ViewModels/RelayCommand.cs` | `ICommand` の汎用実装 |
| `MainViewModel` | `ViewModels/MainViewModel.cs` | メイン画面全体の状態管理。`ScheduleEntry` 一覧・`FlatItems` 生成を担う |
| `ScheduleEntry` | `ViewModels/ScheduleEntry.cs` | 1 つのスケジュールファイルに対応する ViewModel。ルートフォルダを保持 |
| `TaskRowViewModel` | `ViewModels/TaskRowViewModel.cs` | ガントチャート 1 行分の表示 ViewModel。ツリー罫線情報も持つ |
| `ChartCellInfo` | `ViewModels/ChartCellInfo.cs` | ガントチャート 1 日分のセル情報（表示専用 `sealed record`） |
| `TaskPropertiesViewModel` | `ViewModels/TaskPropertiesViewModel.cs` | タスク詳細編集ダイアログの ViewModel。繰り返し予定の設定も担う |
| `TodayScheduleViewModel` | `ViewModels/TodayScheduleViewModel.cs` | 今日のスケジュール表示ウィンドウの ViewModel |
| `IssueTrackingSettingsViewModel` | `ViewModels/IssueTrackingSettingsViewModel.cs` | Issue Tracking 設定ダイアログの ViewModel |
| `ArchiveListViewModel` | `ViewModels/ArchiveListViewModel.cs` | アーカイブ一覧ウィンドウの ViewModel |
| `HolidaySettingsViewModel` | `ViewModels/HolidaySettingsViewModel.cs` | 休日設定ダイアログの ViewModel（曜日レベル、祝日取得、任意休日追加・削除） |
| `HolidayYearSelectionViewModel` | `ViewModels/HolidayYearSelectionViewModel.cs` | 祝日反映対象の年チェックリストを管理する ViewModel |
| `CalloutViewModel` | `ViewModels/CalloutViewModel.cs` | 吹き出し注釈の表示状態・位置計算を扱う ViewModel |
| `UpdateCheckViewModel` | `ViewModels/UpdateCheckViewModel.cs` | バージョン更新確認ダイアログの ViewModel |
| `RecurrenceDayOption` | `ViewModels/RecurrenceDayOption.cs` | 繰り返し設定の曜日選択チェックボックス 1 つ分の表示用 ViewModel |

### Views

| ファイル | 役割 |
|---|---|
| `Views/MainWindow.xaml` | メイン画面（タスクツリー + ガントチャート） |
| `Views/TaskPropertiesWindow.xaml` | タスク詳細編集ダイアログ |
| `Views/TodayScheduleWindow.xaml` | 今日のスケジュール一覧表示 |
| `Views/IssueTrackingSettingsWindow.xaml` | Issue Tracking 連携設定ダイアログ |
| `Views/HolidaySettingsWindow.xaml` | 休日設定ダイアログ |
| `Views/HolidayYearSelectionWindow.xaml` | 祝日反映対象の西暦をチェック選択するダイアログ |
| `Views/ArchiveListWindow.xaml` | アーカイブ済みタスク一覧 |
| `Views/UpdateCheckWindow.xaml` | バージョン更新確認ダイアログ |

### Services

| クラス / 型 | ファイル | 役割 |
|---|---|---|
| `AppSettings` | `Services/AppSettings.cs` | アプリ設定（ウィンドウ位置・列幅・起動時更新確認フラグなど）の保存・読み込み（JSON） |
| `ScheduleFileService` | `Services/ScheduleFileService.cs` | スケジュールデータの JSON 形式での保存・読み込み |
| `ArchiveService` | `Services/ArchiveService.cs` | アーカイブファイル（`*.archive`）の読み書き |
| `HolidayService` | `Services/HolidayService.cs` | 祝日・休日判定ロジック |
| `IIssueProvider` | `Services/IIssueProvider.cs` | Issue Tracking プロバイダーのインターフェース |
| `GitLabIssueProvider` | `Services/GitLabIssueProvider.cs` | GitLab Issues API との連携実装 |
| `JiraIssueProvider` | `Services/JiraIssueProvider.cs` | Jira Cloud API との連携実装 |
| `JiraOnPremIssueProvider` | `Services/JiraOnPremIssueProvider.cs` | Jira オンプレミス API との連携実装 |
| `RedmineIssueProvider` | `Services/RedmineIssueProvider.cs` | Redmine Issues API との連携実装 |
| `IssueTrackingHelper` | `Services/IssueTrackingHelper.cs` | Issue 状態文字列 → `ItemStatus` マッピングなどのユーティリティ |
| `LinkPreviewService` | `Services/LinkPreviewService.cs` | URL のタイトル取得（GitLab/Jira は API、Teams は URL 構造、その他は HTML `<title>` スクレイピング）|
| `UpdateCheckService` | `Services/UpdateCheckService.cs` | GitHub Releases API によるバージョン確認・zip ダウンロード・バッチ更新 |

### Controls

| クラス | ファイル | 役割 |
|---|---|---|
| `GanttRowElement` | `Controls/GanttRowElement.cs` | ガントチャート 1 行を DrawingContext で高速描画 |
| `GanttHeaderElement` | `Controls/GanttHeaderElement.cs` | ガントチャートのヘッダー（日付行）を描画 |
| `TreeLinesElement` | `Controls/TreeLinesElement.cs` | ツリー罫線を DrawingContext で描画 |
| `DropIndicatorAdorner` | `Controls/DropIndicatorAdorner.cs` | ドラッグ＆ドロップ時のドロップ位置インジケーター |
| `CalloutOverlayControl` | `Controls/CalloutOverlayControl.xaml` | ガント領域に吹き出し注釈をオーバーレイ表示するコントロール |

### Converters

| クラス | ファイル | 役割 |
|---|---|---|
| `InverseBoolToVisibilityConverter` | `Converters/InverseBoolToVisibilityConverter.cs` | `true` → `Collapsed` に変換 |
| `IsEqualConverter` | `Converters/IsEqualConverter.cs` | 値の等値比較を行う汎用コンバーター |
| `IsEqualToParameterConverter` | `Converters/IsEqualToParameterConverter.cs` | 値と `ConverterParameter` の等値比較を行うコンバーター |
| `NullableBoolToTestColorConverter` | `Converters/NullableBoolToTestColorConverter.cs` | `bool?` 値をテスト結果色に変換 |
| `DateOnlyToDateTimeConverter` | `Converters/DateOnlyToDateTimeConverter.cs` | `DateOnly` と `DateTime?` を相互変換するコンバーター |

### Themes

| ファイル | 役割 |
|---|---|
| `Themes/BaseTheme.xaml` | アプリ共通のスタイル・ブラシ・テンプレート定義 |

---

## 繰り返し予定機能

- タスクプロパティの「日程」タブに繰り返し設定 UI を持つ（開始日・終了日は横並び配置）。
- 繰り返し設定は `ScheduleToDo.Recurrence`（`RecurrenceRule?`）として保持し、JSON では `recurrence` ノードとして永続化する（`ScheduleFileService`）。
- 終了条件は専用項目を設けず、既存の「終了日」を終了条件として流用する。
- **1タスク完結型**：タスクを複数生成せず、1つの `ScheduleToDo` のまま繰り返し予定を表現する。
  - ガントチャート上は飛び石表示（`TaskRowViewModel.RefreshChartCells` が `RecurrenceRule.GetOccurrencesInRange` で予定日集合を求め、該当日のみバーを描画）。
  - セル色分け: 過去の予定日=過去色（灰色）、直近サイクル（次回予定日）=現在のステータス色、未来の予定日=待機色。
  - ステータス計算（`ScheduleToDo.ComputeRecurrenceStatus`）は `RecurrenceRule.GetNextOccurrence` で求めた次回予定日を基準に、当日なら残1日、当日を過ぎれば次回予定日までのカウントで Warning/Progress を判定する（過ぎた回は不問で自動的に次回にスライドする＝パターン①）。
  - タスク一覧の「残N日」表示（`TaskRowViewModel.DaysText`）も同様に次回予定日を基準に計算する。
  - 繰り返し起点（anchor）は開始日が未設定の場合、今日の日付とする。
  - 繰り返し設定されたタスクはガントチャート上の Shift+ドラッグによる期間移動を無効化する。

---

## コーディング規約

- private フィールドは `_camelCase` プレフィックス
- コマンドは `ICommand`（`RelayCommand`）で定義し ViewModel に配置
- XAML code-behind はイベントの View への委譲のみ（ビジネスロジック禁止）
- `ObservableCollection` は ViewModel のみで使用
- `sealed record` / `record` を表示専用データに積極活用
- null 許容参照型（`nullable enable`）を使用

---

## 機能追加時のチェックリスト

- [ ] 責務に応じた適切なレイヤーに実装したか？
- [ ] 新クラス・新ファイルをこのドキュメントの「主要クラス一覧」に追記したか？
- [ ] ViewModel にコマンドを `ICommand` で定義したか？
- [ ] Service に副作用（I/O・外部 API）を集約したか？
- [ ] 新しい Issue Tracking プロバイダーは `IIssueProvider` を実装したか？

---

## このドキュメントのメンテナンスルール

| 変更内容 | 更新箇所 |
|---|---|
| 新しいクラスを追加 | 該当レイヤーの「主要クラス一覧」テーブルに追記 |
| クラスを削除・リネーム | 該当行を削除・更新 |
| 新機能・新サービスを追加 | 説明を追記 |
| アーキテクチャルールを変更 | 「レイヤー責務ルール」表を更新 |
