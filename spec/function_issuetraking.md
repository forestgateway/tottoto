  
 # 🔗 課題管理連携（Issue Tracking）機能 仕様書（Copilot 用）

## 🎯 概要
todochart に **GitLab / JIRA などの課題管理サービスの Issue を取り込み**、
既存のタスクリストと同じツリー UI 上で表示・更新できるようにする。

本機能は **既存のタスクリスト機構を流用して実装する** ことを前提とする。
つまり、独自の別画面を新設するのではなく、
**1つの Issue 取得設定 = 1つのスケジュールタブ（JSON ファイル）** として扱う。

---

# ✅ 現状仕様との整合方針

## 1. 既存 UI との対応
現在の左ペインの列構成は以下である。
- `★` : マーク
- `タスク名` : `Name`
- `状態` : `ItemStatus` に基づく表示
- `ML` : メモ / リンクアイコン

そのため、Issue 連携では以下の対応とする。
- Issue タイトル → `Name`
- Issue 詳細 URL → `Link`
- Issue 説明、担当者、ラベル、外部ステータス名 → `Memo` または Issue 用メタ情報
- 画面上の状態表示 → 既存 `ItemStatus` へマッピング
- `ML` 列は既存通り **メモ / リンク表示** に使用する

> **注意**  
> 現状の UI には「担当者」専用列は存在しない。  
> 一覧に担当者を常時表示したい場合は、別タスクとして列追加の UI 改修を行う。
> 今回の基本方針では、**既存タスクリストとの整合性を優先**する。

## 2. 既存データモデルとの対応
既存モデルは `ScheduleFolder` / `ScheduleToDo` / `ScheduleItemBase` を使用している。
Issue アイテムは原則として **`ScheduleToDo` として表現**する。

- `Name` : Issue タイトル
- `Memo` : 説明、担当者、ラベル、元ステータス文字列など
- `Link` : Web ブラウザで開く Issue URL
- `BeginDate` : 開始日が取れる場合のみ設定
- `EndDate` : 期限日が取れる場合のみ設定
- `Completed` : 完了状態にマッピングできる場合に設定

期限日がない Issue は、既存タスクと同様に `EndDate = null` とし、
状態欄では「期限なし」として扱えるようにする。

---

# 🆕 新規作成と設定編集ダイアログ

## 1. 新規作成メニュー
現状の「新規作成」は通常のタスクリスト作成であるため、メニュー表記を以下に整理する。

- `新規作成(タスクリスト)`
- `新規作成(Issue Tracking)`

`新規作成(Issue Tracking)` を選択すると、
Issue 取得用の設定ダイアログを開き、新しい Issue Tracking 用スケジュールタブを作成する。

## 2. 設定ダイアログ入力項目
最低限、以下を入力できるようにする。

- `Provider` : `GitLab` / `JIRA`
- `DisplayName` : タブ名・ルート名に表示する名称
- `BaseUrl` : サービスの URL
- `ProjectId` または `ProjectKey`
- `ApiToken`
- `Query` または取得条件（担当者、状態、ラベル等）

必要に応じて以下を追加してよい。
- `GroupBy` : `none` / `milestone` / `assignee` / `status`
- `AutoRefreshOnOpen` : 起動時に自動更新するか

## 3. 設定編集
一度作成した Issue Tracking スケジュールは、
後から同じ設定ダイアログで URL / Token / Query を編集できるようにする。

---

# 📋 ツリー表示ルール

## 1. 表示単位
- 1 つの Issue Tracking 設定につき 1 タブ
- タブ内のルートフォルダ名は `DisplayName`
- 取得した Issue はその配下に並べる

## 2. 初期表示方針
まずは **フラットな Issue 一覧** として表示する。
必要になれば後で `milestone` や `assignee` 単位でフォルダ分けする。

## 3. 行表示内容
既存タスクリストとの整合のため、1 行の表示は以下とする。
- `タスク名` 列 : Issue タイトル
- `状態` 列 : 既存 `ItemStatus` に変換した結果
- `ML` 列 : メモあり / リンクあり のアイコン表示

Issue のタイトル、またはリンクアイコン押下で、
既定ブラウザから Issue の詳細ページを開けるようにする。

---

# 🔄 Issue 状態のマッピング

GitLab / JIRA の状態名はそのままでは既存 `ItemStatus` に一致しないため、
表示時は以下のようにマッピングする。

- `Closed` / `Done` / `Resolved` → `Complete`
- `Open` / `To Do` / `Backlog` → `Wait`
- `In Progress` / `Doing` → `Progress`
- `Review` / `Test` / `Blocked` → `Warning`
- 未完了かつ期限超過 → `Error`

**元のステータス文字列は失わずに保存**し、
必要なら `Memo` または Issue キャッシュ側で保持する。

---

# 🔁 更新（Update）ボタン

## 1. 動作
Issue Tracking 用タブでは `Update` ボタンを用意する。
`Update` 押下時の処理は以下とする。

1. 現在の設定情報を参照して API を呼び出す
2. 最新の Issue 一覧を取得する
3. 取得結果を内部キャッシュに保存する
4. 画面上のツリー (`Children`) を最新内容で再構築する
5. JSON ファイルへ保存する

## 2. 起動時の表示
次回起動時は、まず **最後に保存された Issue キャッシュを表示**する。
そのため、アプリ起動直後に API に接続できなくても、前回取得分の一覧は参照できる。

## 3. エラー時の扱い
API 呼び出しが失敗した場合は、
- 既存のキャッシュ表示は維持する
- エラーメッセージを表示する
- 設定情報は失わない

---

# 💾 保存形式（JSON）

既存のスケジュール保存形式は JSON ベースであるため、
Issue Tracking も **同じ JSON ファイルを拡張して保存**する。

## 保存方針
- `Children` : 画面に表示するツリー用データ
- `IssueTrackingSettings` : 接続設定
- `IssueCache` : 最後に取得した Issue 一覧のバックアップ

### 例
```json
{
  "SavedAt": "2026/04/12 10:30:00",
  "AutoSave": false,
  "SourceType": "IssueTracking",
  "IssueTrackingSettings": {
    "Provider": "GitLab",
    "DisplayName": "Project A Issues",
    "BaseUrl": "https://gitlab.example.com",
    "ProjectId": "123",
    "Query": "assignee=me&state=opened"
  },
  "IssueCache": [
    {
      "Id": "456",
      "Title": "ログイン画面の不具合",
      "State": "opened",
      "Assignee": "tanaka",
      "WebUrl": "https://gitlab.example.com/.../issues/456",
      "DueDate": "2026-04-20"
    }
  ],
  "Children": []
}
```

> **補足**  
> API トークンを JSON に直接保存する場合は平文保存になるため、  
> 将来的には暗号化または OS の資格情報ストア利用を検討する。

---

# ⚙️ 処理フロー

## Issue Tracking 新規作成
1. ユーザーが `新規作成(Issue Tracking)` を選択
2. 設定ダイアログで Provider / URL / Token / Query を入力
3. 新しいスケジュールタブを作成
4. 初回 `Update` を実行して Issue 一覧を取得
5. JSON に設定とキャッシュを保存

## Update 実行
1. 設定情報を読み込む
2. API を呼び出して Issue 一覧を取得
3. `IssueCache` を更新
4. `Children` を再生成
5. 画面を再描画
6. JSON を保存

---

# 🧭 Copilot / 生成 AI に理解させたいポイント（重要）

- **Issue Tracking も既存のタスクリスト構造に寄せて実装する**
- **1 設定 = 1 スケジュールタブ = 1 JSON ファイル** とする
- **Issue 1 件は基本的に `ScheduleToDo` として扱う**
- **表示列は現状の `タスク名 / 状態 / ML` を維持する**
- **担当者は専用列ではなく、まずは `Memo` または詳細情報で扱う**
- **Issue URL は `Link` に入れ、クリックでブラウザを開く**
- **Update 時は API 取得結果を `IssueCache` と `Children` の両方に反映する**
- **起動時は API 呼び出し前でも前回キャッシュを表示できるようにする**


