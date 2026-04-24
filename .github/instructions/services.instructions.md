---
---
applyTo: "src/Services/**"
---

## Services レイヤー規則

- UI（Window・ViewModel）への参照を持たないこと
- ファイル I/O・外部 API・設定永続化の処理はここに集約すること
- 新しい外部連携（Issue Tracker など）は `IIssueProvider` インターフェースを実装すること
