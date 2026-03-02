# AozoraEpub3 .NET — UI 設計仕様書

> デザイナー向けドキュメント。本アプリに必要な設定項目の全一覧、推奨UI技術スタック、
> 制約条件・禁止事項を記載する。

---

## 1. アプリ概要

青空文庫形式テキスト（.txt / .zip / .rar）および小説投稿サイトURL（なろう・カクヨム等）を
EPUB3形式に変換するデスクトップアプリケーション。

**主な操作フロー**

```
[ファイル / URL を指定] → [設定を確認・変更] → [変換実行] → [進捗表示] → [出力先フォルダを開く]
```

---

## 2. 推奨UIライブラリ・技術スタック

### 2-A. Avalonia UI（推奨・第1候補）

| 項目 | 内容 |
|------|------|
| NuGet | `Avalonia` `Avalonia.Desktop` `Avalonia.Themes.Fluent` |
| バージョン | 11.x（.NET 10 対応済み） |
| 対応OS | Windows / macOS / Linux（クロスプラットフォーム） |
| UIパラダイム | XAML + MVVM（ReactiveUI または CommunityToolkit.Mvvm） |
| 公式サイト | https://avaloniaui.net/ |

**採用理由：** .NET 10 完全対応、WPFライクなXAML、クロスプラットフォーム、アクティブなコミュニティ。

### 2-B. WPF（第2候補・Windows専用でよい場合）

| 項目 | 内容 |
|------|------|
| NuGet | 追加不要（.NET 10 SDK に同梱） |
| 対応OS | Windows のみ |
| UIパラダイム | XAML + MVVM（CommunityToolkit.Mvvm 推奨） |

**採用理由：** Windows環境のみでよければ成熟度・サポートリソースが豊富。

### 共通パッケージ

| パッケージ | 用途 |
|-----------|------|
| `CommunityToolkit.Mvvm` | ViewModel / RelayCommand / ObservableProperty |
| `AozoraEpub3.Core`（本プロジェクト） | 変換処理の実体（UIから直接参照） |

> **禁止：** `System.CommandLine` はCLI専用。GUIプロジェクトでは使用しない。

---

## 3. 画面構成（推奨レイアウト）

```
┌─────────────────────────────────────────────────────────────┐
│  メニューバー（ファイル / 設定 / ヘルプ）                       │
├──────────────────────────────┬──────────────────────────────┤
│  入力エリア                   │  設定パネル（タブ切替）          │
│  ・ファイルD&Dゾーン           │  [基本] [章認識] [改ページ]      │
│  ・URLテキストボックス          │  [Web変換] [出力]               │
│  ・入力ファイル一覧リスト        │                                 │
├──────────────────────────────┴──────────────────────────────┤
│  [変換実行] ボタン       進捗バー  キャンセルボタン               │
├─────────────────────────────────────────────────────────────┤
│  ログ出力エリア（スクロール可、最終行に自動スクロール）              │
└─────────────────────────────────────────────────────────────┘
```

---

## 4. 設定項目 全一覧

### タブ1：基本設定

#### 入力

| 設定名 | UIコントロール | デフォルト | 説明 |
|--------|--------------|-----------|------|
| 入力ファイル | FileListView + ドラッグ&ドロップ | — | .txt / .zip / .rar / .cbz |
| 入力エンコード（`-enc`） | ComboBox | MS932 | MS932 / UTF-8 |
| ファイル名から表題・著者を使用（`-tf`） | CheckBox | OFF | ファイル名 `[著者名] タイトル` 形式を優先 |

#### 出力

| 設定名 | UIコントロール | デフォルト | 説明 |
|--------|--------------|-----------|------|
| 出力先ディレクトリ（`--dst / -d`） | TextBox + 参照ボタン | 入力ファイルと同じ | — |
| 出力拡張子（`-ext`） | ComboBox | .epub | .epub / .kepub.epub |
| 出力ファイル名を入力ファイル名に合わせる（`-of`） | CheckBox | OFF（著者名+タイトル） | ONにすると入力ファイル名がそのまま出力名 |
| 対象端末（`-device`） | ComboBox | 汎用 | 汎用 / Kindle |

#### 書誌情報

| 設定名 | UIコントロール | デフォルト | 説明 |
|--------|--------------|-----------|------|
| 表題種別（`-t`） | ComboBox | 表題→著者名 | 表題→著者名 / 著者名→表題 / 表題→著者名(副題優先) / 表題のみ / なし |
| 表紙画像（`--cover / -c`） | ComboBox + TextBox + 参照ボタン | 指定なし | 指定なし / 先頭の挿絵 / 同名画像ファイル / ファイル指定 |
| 表紙ページ挿入（`CoverPage`） | CheckBox | OFF | — |
| 表紙ページを目次に含める（`CoverPageToc`） | CheckBox | OFF | — |
| 表紙最大行数（`MaxCoverLine`） | NumericUpDown | 0（無制限） | この行数より後の挿絵は表紙に使用しない |

#### ページ構成

| 設定名 | UIコントロール | デフォルト | 説明 |
|--------|--------------|-----------|------|
| 縦書き / 横書き（`-hor`） | RadioButton | 縦書き | 縦書き / 横書き |
| 目次ページ挿入（`TocPage`） | CheckBox | OFF | — |
| 目次縦書き（`TocVertical`） | CheckBox | OFF | — |
| 表題を目次に含める（`InsertTitleToc`） | CheckBox | ON | — |
| 表題ページ出力（`TitlePageWrite`） | CheckBox | OFF | — |
| 表題ページ種別（`TitlePage`） | ComboBox | 通常 | 通常 / 中央 / 横書き（TitlePageWriteがONの時のみ有効） |

---

### タブ2：文字・変換設定

| 設定名 | UIコントロール | デフォルト | 説明 |
|--------|--------------|-----------|------|
| 挿絵なし（`NoIllust`） | CheckBox | OFF | 画像を無視してテキストのみ変換 |
| マーク用ID付与（`MarkId`） | CheckBox | OFF | — |
| 自動横組み（`AutoYoko`） | CheckBox | ON | 縦書き時に数字・英字を自動横組み |
| 数字1桁 自動横組み（`AutoYokoNum1`） | CheckBox | ON | — |
| 数字3桁 自動横組み（`AutoYokoNum3`） | CheckBox | ON | — |
| 英数字1桁 自動横組み（`AutoYokoEQ1`） | CheckBox | ON | — |
| 濁点種別（`DakutenType`） | ComboBox | 1 | 0:なし / 1:通常 / 2:外字 |
| IVS BMP出力（`IvsBMP`） | CheckBox | OFF | — |
| IVS SSP出力（`IvsSSP`） | CheckBox | ON | — |
| スペース禁則処理（`SpaceHyphenation`） | ComboBox | 0 | 0:なし / 1:あり / 2:強制 |
| コメント出力（`CommentPrint`） | CheckBox | OFF | 注釈コメントをEPUBに出力 |
| コメント変換（`CommentConvert`） | CheckBox | OFF | 注釈コメントを本文として変換 |
| 空行削除（`RemoveEmptyLine`） | ComboBox | 0（しない） | 0:しない / 1:1行に / 2:すべて削除 |
| 最大空行数（`MaxEmptyLine`） | NumericUpDown | 無制限 | 連続空行の上限（RemoveEmptyLineが0以外の時有効） |

---

### タブ3：章認識設定

| 設定名 | UIコントロール | デフォルト | 説明 |
|--------|--------------|-----------|------|
| 章名最大文字数（`ChapterNameLength`） | NumericUpDown | 64 | — |
| 連続章除外（`ChapterExclude`） | CheckBox | ON | 数字が連続する章を目次から除外 |
| 次行を章名に使用（`ChapterUseNextLine`） | CheckBox | ON | — |
| 節を章として認識（`ChapterSection`） | CheckBox | ON | — |
| 見出しを章として認識（`ChapterH`） | CheckBox | OFF | — |
| H1見出し認識（`ChapterH1`） | CheckBox | OFF | ChapterH=ONの時有効 |
| H2見出し認識（`ChapterH2`） | CheckBox | OFF | ChapterH=ONの時有効 |
| H3見出し認識（`ChapterH3`） | CheckBox | OFF | ChapterH=ONの時有効 |
| 同行章認識（`SameLineChapter`） | CheckBox | OFF | — |
| 章名キーワード（`ChapterName`） | CheckBox | OFF | 「第〇章」等のキーワードで認識 |
| 数字のみの章認識（`ChapterNumOnly`） | CheckBox | OFF | — |
| 数字タイトル章認識（`ChapterNumTitle`） | CheckBox | OFF | — |
| 括弧付き数字章認識（`ChapterNumParen`） | CheckBox | OFF | — |
| 括弧付き数字タイトル章認識（`ChapterNumParenTitle`） | CheckBox | OFF | — |
| カスタム章パターン使用（`ChapterPattern`） | CheckBox | OFF | — |
| カスタム章パターン（`ChapterPatternText`） | TextBox | — | 正規表現文字列（ChapterPattern=ONの時有効） |

---

### タブ4：改ページ設定

| 設定名 | UIコントロール | デフォルト | 説明 |
|--------|--------------|-----------|------|
| 強制改ページ有効（`PageBreak`） | CheckBox | OFF | — |
| 強制改ページサイズ（`PageBreakSize`） | NumericUpDown [KB] | 0 | PageBreak=ONの時有効 |
| 空行での改ページ（`PageBreakEmpty`） | CheckBox | OFF | PageBreak=ONの時有効 |
| 空行改ページ行数（`PageBreakEmptyLine`） | NumericUpDown | 0 | — |
| 空行改ページサイズ（`PageBreakEmptySize`） | NumericUpDown [KB] | 0 | — |
| 章での改ページ（`PageBreakChapter`） | CheckBox | OFF | PageBreak=ONの時有効 |
| 章改ページサイズ（`PageBreakChapterSize`） | NumericUpDown [KB] | 0 | — |

---

### タブ5：Web変換設定

> URL変換（`--url`）使用時のみ有効なタブ

#### 取得設定

| 設定名 | UIコントロール | デフォルト | 説明 |
|--------|--------------|-----------|------|
| 変換URL（`--url / -u`） | TextBox | — | なろう・カクヨム等のURL |
| ダウンロード間隔（`--interval`） | NumericUpDown [ms] | 1500 | サーバー負荷軽減のため短すぎる値を推奨しない（最小500ms） |
| web設定ディレクトリ（`--web-config`） | TextBox + 参照ボタン | 実行ファイルの隣の `web/` | extract.txt を格納するディレクトリ |

#### テキスト変換設定（`NarouFormatSettings`）

| 設定名 | UIコントロール | デフォルト | 説明 |
|--------|--------------|-----------|------|
| 前書き・後書きスタイル | ComboBox | css | css / simple / plain |
| あらすじを表紙に含める | CheckBox | ON | — |
| 掲載URLを表紙に含める | CheckBox | ON | — |
| 更新日時を各話に表示 | CheckBox | OFF | — |
| 初回公開日を各話に表示 | CheckBox | OFF | — |
| 本の終了マーカー表示 | CheckBox | OFF | — |
| 章中表紙でページ中央使用 | CheckBox | ON | — |
| 章中表紙で柱にタイトル表示 | CheckBox | ON | — |
| 行頭かぎ括弧に二分アキ挿入 | CheckBox | ON | — |
| 数字の漢数字化 | CheckBox | OFF | — |
| 漢数字単位化 | CheckBox | OFF | 漢数字化=ONの時有効 |
| 漢数字下位桁ゼロ数 | NumericUpDown | 2 | 漢数字単位化=ONの時有効 |
| 記号の全角化 | CheckBox | OFF | — |
| かぎ括弧内自動連結 | CheckBox | OFF | — |
| 行末読点での自動連結 | CheckBox | OFF | — |
| 前書き・後書き自動検出 | CheckBox | ON | — |
| 自動行頭字下げ | CheckBox | OFF | — |
| 改ページ直後見出し化 | CheckBox | ON | — |
| かぎ括弧開閉チェック（警告） | CheckBox | ON | — |
| なろう独自タグ処理 | CheckBox | ON | — |
| 分数変換 | CheckBox | OFF | — |
| 日付変換 | CheckBox | OFF | — |
| 日付フォーマット | TextBox | %Y年%m月%d日 | 日付変換=ONの時有効 |
| 三点リーダー変換 | CheckBox | OFF | — |
| 長音記号変換 | CheckBox | OFF | — |
| 最大処理ファイルサイズ | NumericUpDown [MB] | 100 | — |

---

## 5. 操作フローと主要インタラクション

### 5-1. ファイル変換

1. ファイルをドラッグ&ドロップ、または「開く」ダイアログから複数選択
2. 設定を確認・変更
3. 「変換実行」ボタンをクリック
4. 進捗バーとログエリアにリアルタイム表示
5. 完了後、出力先フォルダを開くリンク/ボタンを表示

### 5-2. URL変換

1. 「Web変換」タブを開く
2. URLを貼り付け
3. 出力先・設定を確認
4. 「変換実行」 → 各話をダウンロードしながら進捗表示
5. 完了後、出力EPUBへのリンクを表示

### 5-3. 設定の保存・読み込み

- 設定はアプリ終了時に自動保存（JSON or INI形式）
- 「設定をエクスポート」「設定をインポート」メニューを提供
- INIファイル（`AozoraEpub3.ini`）の読み込みにも対応（後方互換）

---

## 6. 制約条件・禁止事項

### 機能制約

| 制約 | 理由 |
|------|------|
| **CBZ（画像のみzipを含む）は変換非対応**（警告表示のみ） | Core側が未サポート |
| ダウンロード間隔は **500ms未満を設定不可**（UI側で強制） | サーバーへの過負荷・BAN防止 |
| 出力パスは **指定ディレクトリ外に生成不可** | パストラバーサル対策（Core側で検証済み） |
| 変換処理は **UIスレッドで実行禁止**、必ず `Task.Run` / `async` で実行 | UIフリーズ防止 |
| **プログレスコールバック** は `IEpub3Writer.ProgressCallback`（`Action<int>`）で受け取る | Core側API仕様 |

### UI設計制約

| 制約 | 理由 |
|------|------|
| **日本語UI必須**（メインターゲットは日本語話者） | — |
| 章認識・改ページ設定は **上級者向けの折りたたみ可能セクション** として扱うことを推奨 | 初心者が混乱しないよう |
| ログエリアは **消去不可** にしない（クリアボタン任意） | 変換エラーの確認に使う |
| 設定変更は **変換実行前にのみ反映** でよい（リアルタイムプレビュー不要） | Core側がステートレス設計 |
| 「カスタム章パターン」テキストボックスは **正規表現の入力欄** であることを明示 | 誤入力防止 |

### 禁止事項

| 禁止 | 理由 |
|------|------|
| 変換中のファイル上書き確認ダイアログを **非表示にしない** | データ消失防止 |
| `AozoraEpub3.Core` の内部クラス（`AozoraEpub3Converter` 等）を **UI層から直接 `new` する以外の方法で生成しない**（DIコンテナ導入は任意） | Core側がシングルトン的なstaticテーブルを保持 |
| ログテキストを **UIスレッド外から直接UIコントロールに書き込まない** | スレッド安全性 |
| Web変換で取得した本文を **ディスクに保存してから再読み込みしない**（in-memoryで完結） | Core API設計（`StringReader` を使用） |

---

## 7. LogAppender 統合

UIでのログ表示は `AozoraEpub3.Core.Converter.LogAppender` の出力を購読する形で実装する。

```csharp
// LogAppender はデフォルトで Console に書くため、
// UI版では LogAppender.SetOutput(Action<string>) のような拡張か、
// TextWriter を差し替えて ViewModel の ObservableCollection<string> に追記する。
```

> 実装前に LogAppender の現行APIを確認し、差し替え可能かどうか検討すること。

---

## 8. 参照ファイル（実装時）

| 目的 | ファイル |
|------|---------|
| 全CLI設定の定義 | `src/AozoraEpub3.Cli/Program.cs` |
| INI設定キーの一覧 | `src/AozoraEpub3.Cli/Program.cs`（`ApplyIniSettings` 関数） |
| Web変換設定の全プロパティ | `src/AozoraEpub3.Core/Web/NarouFormatSettings.cs` |
| 書誌情報モデル | `src/AozoraEpub3.Core/Info/BookInfo.cs` |
| Writer インターフェース（進捗コールバック含む） | `src/AozoraEpub3.Core/Io/IEpub3Writer.cs` |
| テスト用スタブ実装例 | `tests/AozoraEpub3.Tests/AozoraEpub3ConverterTests.cs`（`StubEpub3Writer`） |
