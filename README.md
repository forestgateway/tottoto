# Tottoto

タスク管理 & ガントチャートアプリ（Windows / WPF）

## ライセンス

MIT License — 詳細は [LICENSE](LICENSE) を参照してください。

## Release 配布フロー

- タグ `v*` を push すると GitHub Actions が起動します。
- `win-x64` の Release ビルドを作成し、配布用 zip を生成します。
- zip の SHA-256 を生成し、`cosign` の keyless 署名を付与します。
- 生成物は GitHub Release に自動添付されます。

## 配布物の検証

Release には以下が添付されます。

- `tottoto-<version>-win-x64.zip`
- `tottoto-<version>-win-x64.zip.sha256`
- `tottoto-<version>-win-x64.zip.sha256.sig`
- `tottoto-<version>-win-x64.zip.sha256.pem`
- `tottoto-<version>-win-x64.zip.sha256.bundle`

### 1. ZIP のハッシュを照合

PowerShell:

```powershell
Get-FileHash -Algorithm SHA256 .\tottoto-<version>-win-x64.zip
```

出力されたハッシュが `.sha256` ファイルの先頭値と一致することを確認します。

### 2. cosign 署名を検証

```powershell
cosign verify-blob `
	--signature tottoto-<version>-win-x64.zip.sha256.sig `
	--certificate tottoto-<version>-win-x64.zip.sha256.pem `
	--certificate-oidc-issuer https://token.actions.githubusercontent.com `
	--certificate-identity-regexp "https://github.com/forestgateway/tottoto/.github/workflows/.*@refs/tags/.*" `
	tottoto-<version>-win-x64.zip.sha256
```

検証が成功すれば、公開されたハッシュファイルが GitHub Actions 実行主体で署名されたことを確認できます。

## Add your files

- [ ] [Create](https://docs.gitlab.com/ee/user/project/repository/web_editor.html#create-a-file) or [upload](https://docs.gitlab.com/ee/user/project/repository/web_editor.html#upload-a-file) files
- [ ] [Add files using the command line](https://docs.gitlab.com/topics/git/add_files/#add-files-to-a-git-repository) or push an existing Git repository with the following command:

```
cd existing_repo
git remote add origin https://github.com/forestgateway/tottoto.git
git branch -M main
git push -uf origin main
```

# Tottoto

タスク管理 & ガントチャートアプリ（Windows / WPF）

軽くて使いやすい Windows 向けのタスク管理アプリケーションです。ガントチャート表示、Issue 連携、アーカイブ機能を備えています。

## 目次

- [Tottoto](#tottoto)
  - [ライセンス](#ライセンス)
  - [Release 配布フロー](#release-配布フロー)
  - [配布物の検証](#配布物の検証)
    - [1. ZIP のハッシュを照合](#1-zip-のハッシュを照合)
    - [2. cosign 署名を検証](#2-cosign-署名を検証)
  - [Add your files](#add-your-files)
- [Tottoto](#tottoto-1)
  - [目次](#目次)
  - [主な特徴](#主な特徴)
  - [Requirements](#requirements)
  - [インストール（利用者向け）](#インストール利用者向け)
  - [Build \& Run（開発者向け）](#build--run開発者向け)
  - [Release 配布と自動化](#release-配布と自動化)
  - [配布物の検証](#配布物の検証-1)
  - [Contributing](#contributing)
  - [License](#license)
  - [Authors and acknowledgment](#authors-and-acknowledgment)
  - [License](#license-1)
  - [Project status](#project-status)

## 主な特徴

- ガントチャート表示とタスクツリー
- Issue トラッキング連携（GitLab / Jira 等）
- アーカイブ機能・吹き出し注釈表示

## Requirements

- Windows 10 / 11
- .NET 8 ランタイム（ランタイム同梱の配布を行う場合はセルフコンテインド化を検討してください）

## インストール（利用者向け）

1. GitHub Releases から最新版の `tottoto-<version>-win-x64.zip` をダウンロードします。
2. ZIP を展開し、`Tottoto.exe` をダブルクリックして起動してください。

※ 初回起動時に Windows SmartScreen の警告が出る可能性があります（無料の方法では完全に回避できません）。検証手順は下の「配布物の検証」をご覧ください。

## Build & Run（開発者向け）

開発環境でのビルド／発行手順（PowerShell）：

```powershell
dotnet restore ./src/TodoChart.csproj
dotnet publish ./src/TodoChart.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -o ./artifacts/publish
```

出力先 `./artifacts/publish` に exe と依存ファイルが生成されます。

## Release 配布と自動化

- タグ `v*` を push すると、CI（GitHub Actions）が Release 用ビルドを行い、配布用 zip を生成します。
- zip の SHA-256 を作成し、`cosign` による keyless 署名と証明書（sig / pem / bundle）を生成して Release に添付します。

詳細はワークフロー定義 [.github/workflows/release-zip-signed.yml](.github/workflows/release-zip-signed.yml) を参照してください。

## 配布物の検証

Release には以下が添付されます：

- `tottoto-<version>-win-x64.zip`
- `tottoto-<version>-win-x64.zip.sha256`
- `tottoto-<version>-win-x64.zip.sha256.sig`
- `tottoto-<version>-win-x64.zip.sha256.pem`
- `tottoto-<version>-win-x64.zip.sha256.bundle`

1) ZIP のハッシュ照合（PowerShell）

```powershell
Get-FileHash -Algorithm SHA256 .\tottoto-<version>-win-x64.zip
```

出力ハッシュが `.sha256` ファイルの先頭値と一致することを確認してください。

2) cosign による署名検証（例）

```powershell
cosign verify-blob `
	--signature tottoto-<version>-win-x64.zip.sha256.sig `
	--certificate tottoto-<version>-win-x64.zip.sha256.pem `
	--certificate-oidc-issuer https://token.actions.githubusercontent.com `
	--certificate-identity-regexp "https://github.com/forestgateway/tottoto/.github/workflows/.*@refs/tags/.*" `
	tottoto-<version>-win-x64.zip.sha256
```

検証が成功すれば、公開されたハッシュファイルが GitHub Actions 実行主体で署名されたことを確認できます。

## Contributing

貢献ガイドやコーディング規約は `src/CONTRIBUTING.md` を参照してください。

## License

MIT License — 詳細は [LICENSE](LICENSE) を参照してください。
## Authors and acknowledgment
Show your appreciation to those who have contributed to the project.

## License
For open source projects, say how it is licensed.

## Project status
If you have run out of energy or time for your project, put a note at the top of the README saying that development has slowed down or stopped completely. Someone may choose to fork your project or volunteer to step in as a maintainer or owner, allowing your project to keep going. You can also make an explicit request for maintainers.
