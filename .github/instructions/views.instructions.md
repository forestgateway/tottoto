---
---
applyTo: "src/Views/**"
---

## Views レイヤー規則

- XAML code-behind にビジネスロジックを書かないこと
- code-behind は UI イベント（アニメーション・フォーカス制御など）の View 内処理のみ許可
- DataContext は対応する ViewModel をバインドすること
- スタイル・ブラシ等は `src/Themes/BaseTheme.xaml` のリソースを参照すること
