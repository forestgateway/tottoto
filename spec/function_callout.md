
# ガントチャート吹き出し機能仕様（Copilot実装向け）

## 1. 目的

ガントチャート上のタスクバーに「吹き出し（コメント）」を表示し、以下を実現する。

- タスクに対する補足情報を視覚的に表示できる
- タスク日程変更時にも吹き出し位置が意図どおり追随する
- 複数吹き出しを同一タスクに保持できる
- 表示スタイル（現状は BubbleLine）で描画できる

## 2. スコープ

### 2.1 対象

- ガントチャート画面での吹き出し追加・編集・表示
- 吹き出しデータの永続化
- BubbleLine スタイルでの描画

### 2.2 非対象（今回実装しない）

- 吹き出し内リッチテキスト（太字、色、リンクなど）
- 画像添付
- 吹き出しテンプレート管理
- 複数スタイル（ChatGpt / MaterialCard / Fluent）への切り替え

## 3. 用語定義

- アンカー: 吹き出しの基準位置
- オフセット基準:
  - `AbsoluteDateTime`: 固定日時
  - `StartDate`: タスク開始日基準
  - `EndDate`: タスク終了日基準
- 表示モード:
  - `AlwaysVisible`: 常に表示
  - `HoverOnly`: ホバー時のみ表示

## 4. データ仕様

各タスクは 0 件以上の吹き出しを持てる。

### 4.1 吹き出しエンティティ

- `Id`: 一意キー
- `Text`: 吹き出し本文
- `PositionMode`: `AbsoluteDateTime | StartDate | EndDate`
- `AbsoluteDateTime`: 固定日時（`PositionMode = AbsoluteDateTime` の時に使用）
- `OffsetDays`: 基準日からの相対日数（`StartDate/EndDate` の時に使用）
- `Style`: `BubbleLine`
- `VisibilityMode`: `AlwaysVisible | HoverOnly`
- `CreatedAt`, `UpdatedAt`: 更新管理用

### 4.2 位置計算ルール

- `AbsoluteDateTime`: `X = AbsoluteDateTime` をチャート座標へ変換
- `StartDate`: `X = Task.StartDate + OffsetDays`
- `EndDate`: `X = Task.EndDate + OffsetDays`
- タスク期間変更時は再計算して再描画する

## 5. ユーザー操作仕様

## 5.1 ガントチャート日付セル右クリック

表示メニュー:

- `吹き出しを追加`

動作:

- 右クリックした日時を初期値として吹き出しを作成
- 初期 `PositionMode` は `EndDate`
- 追加直後に編集モード（テキスト入力）へ遷移

## 5.2 吹き出し右クリック

表示メニュー:

- `テキスト編集`
- `表示位置` > `日時固定` / `開始日` / `終了日`
- `自動的に隠す（HoverOnly）` と `常に表示（AlwaysVisible）` の切替
- `削除`

## 6. 表示仕様

- 吹き出しはタスクバー上にオーバーレイ描画する
- 同一タスクに複数吹き出しを表示できる
- 重なりが発生した場合、縦方向にスタックして可読性を維持する
- `HoverOnly` の場合はホバー中のみ表示する

## 7. スタイル仕様

現行実装の対象スタイルは 1 種。

- `BubbleLine`（UIparts BubbleTooltip 準拠）

以下は将来拡張時の参考サンプル XAML（見た目調整は実装時に微調整可）。

### 7.1 Minimal Line

~~~xaml
<Grid>
    <Border Background="White"
            CornerRadius="8"
            Padding="12"
            BorderBrush="#DDDDDD"
            BorderThickness="1">
        <TextBlock Text="This is a sample callout."
                   Foreground="#66000000" />
    </Border>
    <Polygon Fill="White"
             Stroke="#DDDDDD"
             StrokeThickness="1"
             Points="24,0 40,16 8,16"
             HorizontalAlignment="Left"
             VerticalAlignment="Bottom"
             Margin="24,0,0,-8" />
</Grid>
~~~

### 7.2 ChatGPT 風

~~~xaml
<Grid>
    <Border Background="#F3F3F3"
            CornerRadius="20"
            Padding="12,8"
            Margin="0,0,16,0">
        <TextBlock Text="This is a sample callout."
                   Foreground="#55000000" />
    </Border>
    <Path Fill="#F3F3F3"
          HorizontalAlignment="Right"
          VerticalAlignment="Bottom"
          Margin="0,0,4,4"
          Data="M 0 0 C 6 4, 6 10, 0 14 L 10 10 Z" />
</Grid>
~~~

### 7.3 Material Card Style

~~~xaml
<Grid>
    <Grid.Effect>
        <DropShadowEffect BlurRadius="12"
                          ShadowDepth="4"
                          Opacity="0.3"
                          Color="#80000000" />
    </Grid.Effect>
    <Border Background="White"
            CornerRadius="10"
            Padding="16">
        <TextBlock Text="This is a sample callout."
                   Foreground="#66000000" />
    </Border>
    <Polygon Fill="White"
             Points="0,0 16,0 8,10"
             HorizontalAlignment="Center"
             VerticalAlignment="Bottom"
             Margin="0,0,0,-10" />
</Grid>
~~~

### 7.4 Fluent

~~~xaml
<Grid>
    <Grid.Effect>
        <DropShadowEffect BlurRadius="16"
                          ShadowDepth="4"
                          Opacity="0.35"
                          Color="#80000000" />
    </Grid.Effect>
    <Border Background="#CCFFFFFF"
            CornerRadius="12"
            Padding="16"
            BorderBrush="#40FFFFFF"
            BorderThickness="1">
        <TextBlock Text="This is a sample callout."
                   Foreground="#66000000" />
    </Border>
    <Polygon Fill="#CCFFFFFF"
             Stroke="#40FFFFFF"
             StrokeThickness="1"
             Points="24,0 40,16 16,16"
             HorizontalAlignment="Left"
             VerticalAlignment="Bottom"
             Margin="24,0,0,-8" />
</Grid>
~~~

## 8. 機能要件（実装タスク化しやすい単位）

- FR-01: タスクに複数吹き出しを紐づけて保存できる
- FR-02: 日付セル右クリックから吹き出しを新規追加できる
- FR-03: 追加直後に本文編集できる
- FR-04: 表示位置モードを `AbsoluteDateTime/StartDate/EndDate` で切替できる
- FR-05: タスク移動・期間変更時に吹き出し位置が再計算される
- FR-06: `BubbleLine` スタイルで吹き出しを描画できる
- FR-07: `AlwaysVisible/HoverOnly` を切替できる
- FR-08: 吹き出し同士が重なる場合に視認可能な配置へ調整する

## 9. 受け入れ条件（Acceptance Criteria）

- AC-01: 吹き出し追加後、保存して再起動しても内容が復元される
- AC-02: `StartDate` 基準の吹き出しは、タスク開始日の変更に追随して移動する
- AC-03: `EndDate` 基準の吹き出しは、タスク終了日の変更に追随して移動する
- AC-04: `AbsoluteDateTime` 基準の吹き出しは、タスク期間変更後も固定日時を維持する
- AC-05: 保存・再起動後も `BubbleLine` スタイルで復元される
- AC-06: `HoverOnly` は非ホバー時に表示されない
- AC-07: 同一タスクに 3 件以上の吹き出しを設定しても、内容が判読可能である

## 10. 実装時メモ（Copilot向け）

- まずはモデル追加（吹き出しエンティティ）と永続化の更新を行う
- 次に ViewModel で CRUD と位置再計算ロジックを実装する
- 最後に View（XAML）でスタイルテンプレートとコンテキストメニューを接続する
- 重なり回避は初期実装で「縦方向スタック + 最小マージン確保」を採用する



