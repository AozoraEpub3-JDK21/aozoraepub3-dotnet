# AozoraEpub3-dotnet

> **⚠️ 開発中のため、現時点では正常に動作しません。**
> このリポジトリは鋭意開発中です。安定版のリリースまでしばらくお待ちください。

---

**「読む人を作る人に変えるアプリ」**

なろう・カクヨムの小説をEPUBで読んでいた人が、「自分でも書いてみたい」と思ったとき。
このアプリが書いて、本にして、届けるまでを支えます。

Java製の [AozoraEpub3](https://github.com/hmdev/AozoraEpub3) を .NET 10 (C#) に移植し、
GUIエディタ・執筆機能・プレビューを加えた統合ツールです。

---

## 3つの層

```
層1: 読む ─── なろう/カクヨム/青空文庫のURL → EPUB（入口・最大の差別化）
      ↓ 「自分でも書いてみたい」
層2: 書く ─── 執筆エディタ + 縦書きリアルタイムプレビュー
      ↓ 「本の形にしたい」
層3: 本にする ─ 表紙・目次・テンプレートで仕上げる（ブックエディタ）
```

各層は独立して使えます。層1だけでも、EPUB変換ツールとして機能します。

---

## 主な機能

### 層1: 読む・変換
- なろう / カクヨム / 青空文庫のURLから直接EPUB生成
- ローカルテキストファイル（.txt / .zip）の変換
- 縦書きEPUBプレビュー
- epubcheck対応の正しいEPUB3生成

### 層2: 書く
- テキストエディタ（縦書きリアルタイムプレビュー付き）
- ルビ・傍点・見出しのツールバー操作
- なろう/カクヨム互換の記法サポート
- 注記（チュキ）補完・チートシート
- カード型執筆モード（初心者向け）

### 層3: 本にする
- 章構成エディタ（カードベース）
- 複数ファイルの結合・並べ替え
- EPUB出力

---

## ビルド・実行方法

### 必要環境
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### ビルド

```bash
dotnet build
```

### テスト実行

```bash
dotnet test
```

### CLI実行

```bash
# テキストファイルをEPUBに変換
dotnet run --project src/AozoraEpub3.Cli -- <ファイルパス>

# URLからEPUBを生成
dotnet run --project src/AozoraEpub3.Cli -- --url https://ncode.syosetu.com/nXXXX/ -d ./output
```

### URL変換のキャッシュ運用（再ダウンロード削減）

```bash
# 通常（既定）: キャッシュを使う。未キャッシュURLは取得して保存
dotnet run --project src/AozoraEpub3.Cli -- --url <URL> -d ./output --web-cache-mode on

# オフライン再実行: キャッシュ済みページのみで変換（未命中は失敗）
dotnet run --project src/AozoraEpub3.Cli -- --url <URL> -d ./output --web-cache-mode only

# キャッシュ無効
dotnet run --project src/AozoraEpub3.Cli -- --url <URL> -d ./output --web-cache-mode off
```

- `--web-cache-dir <PATH>` を指定するとキャッシュ保存先を固定できます。
- 未指定時の保存先は `--dst` 配下の `.webcache`（`--dst` 未指定時は LocalAppData 配下）です。
- 既存の環境変数 `AOZORA_WEB_CACHE_DISABLE` / `AOZORA_WEB_CACHE_ONLY` も引き続き利用可能です。

### GUI起動

```bash
dotnet run --project src/AozoraEpub3.Gui
```

### MSIインストーラーのビルド

Windows向けのMSIインストーラーを作成できます。

**必要ツール:**

```powershell
# WiX Toolset (初回のみ)
dotnet tool install -g wix --version 5.0.2
wix extension add WixToolset.UI.wixext/5.0.2 --global
```

**ビルド実行:**

```powershell
./installer/build.ps1 -Version 0.0.1
```

実行後、リポジトリルートに `AozoraEpub3-0.0.1-win-x64.msi` が生成されます。

| オプション | 説明 |
|-----------|------|
| `-Version 0.0.1` | バージョン番号 |
| `-SkipPublish` | `dotnet publish` をスキップして既存の `publish/` を使用 |
| `-Configuration Debug` | Debugビルド（デフォルト: Release） |

**GitHub Actions による自動ビルド:**

`v0.0.1` のようなタグをプッシュすると、MSI + ZIP を生成して GitHub Release (draft) を自動作成します。

```bash
git tag v0.0.1
git push origin v0.0.1
```

---

## プロジェクト構成

```
src/
  AozoraEpub3.Core/   — 変換エンジン・コアライブラリ
  AozoraEpub3.Cli/    — コマンドラインインターフェース
  AozoraEpub3.Gui/    — Avalonia UI 11 デスクトップアプリ
tests/
  AozoraEpub3.Tests/  — xUnit テスト
web/                  — サイト別Web抽出設定
```

---

## 対応プラットフォーム

- Windows
- macOS
- Linux

（Avalonia UI により全OS対応を目指しています）

---

## 元プロジェクト

このアプリは Java 製の [AozoraEpub3](https://github.com/hmdev/AozoraEpub3)（作者: hmdev 氏）を
ベースとしています。EPUB変換の中核ロジックはそちらの成果によるものです。

---

## ライセンス

開発中につき未定。元プロジェクトのライセンスに準拠する予定です。

---

> **現在の状態:** 開発中 / 動作保証なし
> フィードバック・Issue はお気軽にどうぞ。
