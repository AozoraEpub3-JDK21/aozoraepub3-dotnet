# エディタ機能 詳細設計・マイルストーン

## 0. 既存コードベースとの整合性メモ

### 補正事項（たたき台からの修正）
| たたき台の記述 | 実際の状態 | 修正 |
|---|---|---|
| WPF/WinUI 3 での MVVM | Avalonia UI 11 + CommunityToolkit.Mvvm | Avalonia 前提で設計 |
| AvalonEdit | AvaloniaEdit (NuGet: `AvaloniaEdit`) | Avalonia 版を使用 |
| Event Aggregator | Avalonia には不要。ViewModel 間は直接イベント or メッセージング | CommunityToolkit.Mvvm の WeakReferenceMessenger 検討 |

### 既存で再利用可能なコンポーネント
| コンポーネント | 場所 | エディタ機能での用途 |
|---|---|---|
| `AozoraEpub3Converter` | `Core/Converter/` | ライブプレビューの XHTML 生成（2パス）|
| `AozoraTextFinalizer` | `Core/Web/` | 自動整形の一部（空行圧縮、字下げ、漢数字等）|
| `chuki_tag.txt` | `Core/Resources/` | 青空文庫注記→HTML タグ変換表（728行）— **注記サジェストの辞書ソース** |
| `WebView2Host` | `Gui/Controls/` | プレビューウィンドウの WebView2 ホスト |
| `EpubPreviewService` | `Gui/Services/` | EPUB 展開・ナビゲーション |
| `PreviewViewModel` | `Gui/ViewModels/` | プレビュー制御（ページ送り、目次等）|

### 新規に必要なコンポーネント
| コンポーネント | 責務 |
|---|---|
| `EditorConversionEngine` | ハイブリッド記法 → 青空文庫記法の変換エンジン |
| `LivePreviewService` | デバウンス付きリアルタイム変換 + XHTML 生成 |
| `EditorViewModel` | エディタ画面の ViewModel |
| `EditorView` | エディタ画面（AvaloniaEdit or TextBox）|
| `PreviewWindow` | 独立プレビューウィンドウ |
| `ConversionProfile` | なろう/カクヨム等のモード別変換ルール定義 |
| `EditorSuggestService` | サジェスト・自動補完・括弧ペアリングの統合サービス |
| `NovelFormatter` | 小説向け自動整形（Lint/Formatter） |

### レイヤー境界の明確化

```
┌─────────────────────────────────────────────────────────┐
│ エディタ入力支援レイヤー                                   │
│  - サジェスト・自動補完 ← chuki_tag.txt（青空文庫注記）     │
│  - 括弧ペアリング ← 日本語組版慣習                        │
│  - Lint（自動整形）← 小説執筆慣習                         │
│  ※ EPUB仕様・電書連ガイドは直接参照しない                   │
└─────────────────────┬───────────────────────────────────┘
                      │ 青空文庫記法テキスト
                      ▼
┌─────────────────────────────────────────────────────────┐
│ 変換・出力レイヤー                                        │
│  - AozoraEpub3Converter ← EPUB3仕様準拠                  │
│  - Epub3Writer ← 電書連ガイド準拠（CSS、メタデータ等）      │
│  ※ EPUB仕様・電書連ガイドはここで効く                      │
└─────────────────────────────────────────────────────────┘
```

---

## 1. アーキテクチャ概要

```
┌──────────────────────────────────────────────────────────┐
│ EditorView (メインウィンドウ内タブ or 独立)                │
│  ┌─────────────┐  ┌───────────┐  ┌──────────────────┐   │
│  │ ToolBar     │  │ ModeCombo │  │ Preview Button   │   │
│  │ (ルビ/傍点  │  │ (なろう/   │  │ (別ウィンドウ    │   │
│  │  /太字/見出 │  │  カクヨム) │  │  起動)           │   │
│  │  しボタン)  │  │           │  │                  │   │
│  └─────────────┘  └───────────┘  └──────────────────┘   │
│  ┌──────────────────────────────────────────────────┐    │
│  │ TextBox or AvaloniaEdit                          │    │
│  │ (ハイブリッド記法で入力)                           │    │
│  │                                                  │    │
│  │  ┌────────────────────────────┐                   │    │
│  │  │ SuggestPopup (候補リスト)   │ ← 注記/ルビ補完  │    │
│  │  └────────────────────────────┘                   │    │
│  └──────────┬───────────────────────────────────────┘    │
│             │                                            │
│             ├─ KeyDown → EditorSuggestService             │
│             │   (括弧ペアリング、注記補完、ルビアシスト)     │
│             │                                            │
│             │ TextChanged (debounce 300-500ms)           │
│             ▼                                            │
│  ┌──────────────────────────────────────────────────┐    │
│  │ EditorConversionEngine (バックグラウンド)          │    │
│  │  1. 自動整形（NovelFormatter / Lint）             │    │
│  │  2. ハイブリッド記法 → 青空文庫記法               │    │
│  │  3. AozoraEpub3Converter → XHTML 断片           │    │
│  └──────────┬───────────────────────────────────────┘    │
│  ┌──────────────────────────────────────────────────┐    │
│  │ StatusBar                                        │    │
│  │  文字数 | 行数 | モード | Lint 警告数             │    │
│  └──────────────────────────────────────────────────┘    │
└─────────────┼────────────────────────────────────────────┘
              │ XHTML (innerHTML差分更新)
              ▼
┌──────────────────────────────────────────────────────────┐
│ PreviewWindow (別ウィンドウ)                              │
│  ┌──────────────────────────────────────────────────┐    │
│  │ WebView2Host                                     │    │
│  │ (縦書き XHTML レンダリング)                       │    │
│  └──────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────┘
```

### ライブプレビュー戦略: 「部分 XHTML 生成」方式

全 EPUB 生成は重すぎるため、以下のアプローチを採用する:

1. エディタのテキストを `StringReader` に渡す
2. `AozoraEpub3Converter` の変換ロジックを**シングルパス XHTML 出力**として切り出し
   - `GetBookInfo()` は初回のみ実行、以降はキャッシュ
   - `ConvertTextToEpub3()` の出力先を `StringWriter` にして XHTML 断片を取得
3. WebView2 に `document.getElementById('content').innerHTML = ...` で差分注入

---

## 2. マイルストーン（フェーズ分け）

### Phase E1: 変換エンジン MVP（バックエンドのみ）
**ゴール:** ハイブリッド記法テキスト → 青空文庫記法テキストの変換が動く

| # | タスク | クラス/ファイル | 概要 |
|---|--------|----------------|------|
| E1-1 | ConversionProfile 定義 | `Core/Editor/ConversionProfile.cs` | なろう/カクヨム/汎用のモード定義（enum + ルール集合）|
| E1-2 | EditorConversionEngine 実装 | `Core/Editor/EditorConversionEngine.cs` | `[GeneratedRegex]` による変換（ルビ・傍点・太字・見出し・改ページ・字下げ）|
| E1-3 | NovelFormatter 実装 | `Core/Editor/NovelFormatter.cs` | 三点リーダー、ダッシュ、感嘆符後スペース |
| E1-4 | 単体テスト | `Tests/EditorConversionEngineTests.cs` | 各変換ルールのテスト |
| E1-5 | AozoraTextFinalizer 連携 | (既存コードの薄いラッパー) | 既存の空行圧縮・字下げ等を再利用 |

### Phase E2: サジェスト・自動補完エンジン
**ゴール:** 括弧ペアリング、注記補完、ルビアシストが単体で動く

| # | タスク | クラス/ファイル | 概要 |
|---|--------|----------------|------|
| E2-1 | EditorSuggestService 実装 | `Gui/Services/EditorSuggestService.cs` | サジェスト統合サービス |
| E2-2 | 括弧ペアリングエンジン | (EditorSuggestService 内) | 日本語括弧の自動ペアリング |
| E2-3 | 注記補完辞書ローダー | `Core/Editor/ChukiDictionary.cs` | chuki_tag.txt からサジェスト候補を構築 |
| E2-4 | ルビ入力アシスト | (EditorSuggestService 内) | `|` + 漢字 → `《》` 自動挿入 |
| E2-5 | 単体テスト | `Tests/EditorSuggestServiceTests.cs` | ペアリング・補完・アシストのテスト |

### Phase E3: ライブプレビュー基盤
**ゴール:** テキスト入力 → XHTML 生成 → WebView2 表示がリアルタイムで動く

| # | タスク | クラス/ファイル | 概要 |
|---|--------|----------------|------|
| E3-1 | LivePreviewService 実装 | `Gui/Services/LivePreviewService.cs` | デバウンス + バックグラウンド変換 + XHTML 生成 |
| E3-2 | AozoraEpub3Converter 拡張 | `Core/Converter/` | シングルチャプター XHTML 出力メソッド追加 |
| E3-3 | PreviewWindow 実装 | `Gui/Views/PreviewWindow.axaml` | 独立ウィンドウ（WebView2Host 再利用）|
| E3-4 | WebView2Host 拡張 | `Gui/Controls/WebView2Host.cs` | `UpdateContent(html)` メソッド追加（innerHTML 差分更新）|
| E3-5 | 縦書き CSS テンプレート | (既存 `vertical_text.sbn` ベース) | ライブプレビュー用の最小 CSS |

### Phase E4: エディタ UI
**ゴール:** GUI 上でハイブリッド記法テキストを入力し、サジェスト・リアルタイムプレビューが動く

| # | タスク | クラス/ファイル | 概要 |
|---|--------|----------------|------|
| E4-1 | EditorViewModel 実装 | `Gui/ViewModels/EditorViewModel.cs` | テキスト管理、モード選択、ツールバーコマンド |
| E4-2 | EditorView (MVP: TextBox) | `Gui/Views/EditorView.axaml` | メインエディタ画面（TextBox ベース）|
| E4-3 | ツールバー実装 | (EditorView 内) | ルビ・傍点・太字・見出し・改ページ挿入ボタン |
| E4-4 | サジェストポップアップ UI | `Gui/Controls/SuggestPopup.axaml` | 候補リスト表示（Popup + ListBox）|
| E4-5 | 括弧ペアリング統合 | (EditorView コードビハインド) | KeyDown イベント → EditorSuggestService |
| E4-6 | 注記補完 UI 統合 | (EditorView コードビハインド) | `［＃` 入力 → ポップアップ表示 |
| E4-7 | ショートカットキー | (EditorView コードビハインド) | Ctrl+R(ルビ), Ctrl+B(太字) 等 |
| E4-8 | モード切替 ComboBox | (EditorView 内) | なろう/カクヨム/汎用 |
| E4-9 | MainWindow 統合 | サイドバーに「執筆」ボタン追加 | SPA ルーティングに EditorViewModel 追加 |

### Phase E5: ファイル操作 + UX 改善
**ゴール:** 実用的な執筆ツールとして使える

| # | タスク | 概要 |
|---|--------|------|
| E5-1 | ファイル新規作成/開く/保存 | `.txt` / `.md` ファイルの読み書き |
| E5-2 | 最近使ったファイル | 設定に保存、起動時に復元 |
| E5-3 | 自動保存（ドラフト） | 30秒ごとに `.draft` に自動バックアップ |
| E5-4 | 文字数カウント | ステータスバーにリアルタイム表示 |
| E5-5 | 検索/置換 | Ctrl+F / Ctrl+H |
| E5-6 | Undo/Redo 強化 | TextBox 標準の Undo + 拡張 |

### Phase E6: 高度な機能（将来）
| # | タスク | 概要 |
|---|--------|------|
| E6-1 | AvaloniaEdit 導入 | シンタックスハイライト（記法の色分け）|
| E6-2 | 変換ルール外部化 | JSON/YAML カスタム辞書読み込み |
| E6-3 | EPUB 直接書き出し | エディタからの直接 EPUB 生成 |
| E6-4 | 章分割エディタ | 複数ファイルのプロジェクト管理 |
| E6-5 | 校正支援 | 表記ゆれチェック、重複表現検出 |
| E6-6 | サジェスト辞書カスタマイズ | ユーザー定義の注記・スニペット辞書 |

---

## 3. Phase E1 詳細設計: 変換エンジン

### 3-1. ConversionProfile（変換プロファイル）

```csharp
public enum EditorMode { Generic, Narou, Kakuyomu }

public sealed class ConversionProfile
{
    public EditorMode Mode { get; init; }
    public bool EnableRuby { get; init; } = true;           // |漢字《かんじ》
    public bool EnableEmphasis { get; init; } = true;       // 《《強調》》
    public bool EnableBold { get; init; } = true;           // **太字**
    public bool EnableHeadings { get; init; } = true;       // # 見出し
    public bool EnablePageBreak { get; init; } = true;      // ---
    public bool EnableBlockquote { get; init; } = true;     // > 引用
    public bool AutoEllipsis { get; init; } = true;         // ... → ……
    public bool AutoDash { get; init; } = true;             // -- → ――
    public bool AutoExclamationSpace { get; init; } = true; // ！の後にスペース

    public static ConversionProfile Default => new() { Mode = EditorMode.Generic };
    public static ConversionProfile Narou => new() { Mode = EditorMode.Narou };
    public static ConversionProfile Kakuyomu => new()
    {
        Mode = EditorMode.Kakuyomu,
        EnableEmphasis = true,  // 《《》》はカクヨム式
    };
}
```

### 3-2. EditorConversionEngine 変換パイプライン

```
入力テキスト
    │
    ▼
[1] 自動整形（NovelFormatter）
    │  ... → ……, -- → ――, ！の後スペース
    ▼
[2] ハイブリッド記法 → 青空文庫記法（EditorConversionEngine）
    │  |漢字《》→ ｜漢字《》
    │  《《強調》》→ ［＃傍点］強調［＃傍点終わり］
    │  **太字** → ［＃太字］太字［＃太字終わり］
    │  # 見出し → ［＃大見出し］見出し［＃大見出し終わり］
    │  --- → ［＃改ページ］
    │  > 引用 → ［＃ここから１字下げ］引用［＃ここで字下げ終わり］
    ▼
[3] 青空文庫記法テキスト（AozoraEpub3Converter に渡せる形式）
    │
    ▼
[4] AozoraEpub3Converter → XHTML（ライブプレビュー用）
```

### 3-3. GeneratedRegex 活用方針

```csharp
public sealed partial class EditorConversionEngine
{
    // ルビ: |漢字《かんじ》 → ｜漢字《かんじ》
    [GeneratedRegex(@"\|([^\|《》\s]+)《([^》]+)》")]
    private static partial Regex RubyPattern();

    // 傍点: 《《テキスト》》 → ［＃傍点］テキスト［＃傍点終わり］
    [GeneratedRegex(@"《《([^》]+)》》")]
    private static partial Regex EmphasisPattern();

    // 太字: **テキスト** → ［＃太字］テキスト［＃太字終わり］
    [GeneratedRegex(@"\*\*([^\*]+)\*\*")]
    private static partial Regex BoldPattern();

    // 見出し: # テキスト → ［＃大見出し］テキスト［＃大見出し終わり］
    [GeneratedRegex(@"^(#{1,3})\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex HeadingPattern();

    // 改ページ: --- → ［＃改ページ］
    [GeneratedRegex(@"^-{3,}\s*$", RegexOptions.Multiline)]
    private static partial Regex PageBreakPattern();

    // 引用: > テキスト
    [GeneratedRegex(@"^>\s*(.+)$", RegexOptions.Multiline)]
    private static partial Regex BlockquotePattern();
}
```

### 3-4. 変換順序と競合回避

変換は以下の順序で実行する。順序が重要な理由をコメントで示す。

```
1. 傍点 《《…》》  ← 最初に処理（ルビの《》と競合するため）
2. ルビ |…《…》    ← 傍点処理後の残りの《》をルビとして処理
3. 太字 **…**
4. 見出し # …
5. 改ページ ---
6. 引用 > …
```

**エスケープ記法:** バックスラッシュでエスケープ可能とする。
- `\|` → リテラルの `|`（ルビ開始と解釈しない）
- `\*\*` → リテラルの `**`
- `\#` → リテラルの `#`

---

## 4. Phase E2 詳細設計: サジェスト・自動補完

### 4-1. 設計方針

エディタ上の入力支援は「**書く手を止めない**」ことを最優先する。

- 括弧ペアリングは**即座に**（キー入力と同時に）
- 注記補完は**トリガー文字入力後に**（ポップアップで候補提示）
- ルビアシストは**コンテキストに応じて**（漢字の後の `|` で発動）
- すべての補完は **Esc キーで即座にキャンセル**可能

### 4-2. 括弧自動ペアリング (Bracket Auto-Pairing)

#### 対象ペア一覧

| 開き括弧 | 閉じ括弧 | 用途 | 自動ペアリング |
|----------|----------|------|:--------------:|
| `「` | `」` | 会話文 | ON |
| `『` | `』` | 二重かぎ括弧 | ON |
| `（` | `）` | 全角丸括弧 | ON |
| `《` | `》` | ルビ・傍点 | ON |
| `［` | `］` | 注記開始 | ON |
| `｛` | `｝` | 全角波括弧 | ON |
| `(` | `)` | 半角丸括弧 | ON |
| `[` | `]` | 半角角括弧 | ON |
| `{` | `}` | 半角波括弧 | ON |

#### 動作仕様

```
入力: 「
結果: 「|」   (| はカーソル位置)

入力: 「こんにちは」  ← 閉じ括弧は上書きモード
       カーソルが」の直前にある状態で」を入力
       → カーソルが」の後ろに移動（二重挿入しない）
```

**スマートペアリングルール:**

1. **開き括弧入力時:**
   - 閉じ括弧を自動挿入し、カーソルを括弧の間に配置
   - ただし、次の条件では自動挿入しない:
     - カーソル直後が同じ閉じ括弧の場合（二重化防止）
     - 選択テキストがある場合 → 選択テキストを括弧で囲む

2. **閉じ括弧入力時:**
   - カーソル直後が同一の閉じ括弧なら、**挿入せずカーソルだけ移動**（スキップ動作）
   - そうでなければ通常挿入

3. **バックスペース時:**
   - 空の括弧ペア（例: `「」`）の間にカーソルがある場合、**ペアごと削除**
   - 括弧内に内容がある場合は通常のバックスペース動作

4. **選択テキストの括弧囲み:**
   - テキストを選択した状態で開き括弧を入力 → 選択テキストを括弧で囲む
   - 例: `こんにちは` を選択 → `「` を入力 → `「こんにちは」`

#### 実装: `EditorSuggestService.HandleBracketInput()`

```csharp
public sealed class BracketPairResult
{
    public required string TextToInsert { get; init; }
    public required int CursorOffset { get; init; }  // 挿入テキスト先頭からのカーソル位置
    public bool ShouldSkip { get; init; }             // true = 挿入せずカーソル移動のみ
    public bool ShouldDeletePair { get; init; }       // true = ペアごと削除（BS用）
}

public BracketPairResult? HandleBracketInput(
    char inputChar,
    char? charAfterCursor,
    char? charBeforeCursor,
    string? selectedText)
{
    // 開き括弧の場合
    if (IsOpenBracket(inputChar))
    {
        var closeBracket = GetCloseBracket(inputChar);

        // 選択テキストがある場合 → 囲む
        if (selectedText != null)
            return new() {
                TextToInsert = $"{inputChar}{selectedText}{closeBracket}",
                CursorOffset = selectedText.Length + 2
            };

        // 通常 → ペア挿入
        return new() {
            TextToInsert = $"{inputChar}{closeBracket}",
            CursorOffset = 1  // 開き括弧の直後
        };
    }

    // 閉じ括弧の場合 → スキップ判定
    if (IsCloseBracket(inputChar) && charAfterCursor == inputChar)
        return new() { ShouldSkip = true, TextToInsert = "", CursorOffset = 1 };

    return null;  // 通常入力
}
```

### 4-3. 注記補完 (Annotation Completion)

#### トリガーと動作フロー

```
入力: ［＃
  → SuggestPopup 表示（候補リスト）

候補リスト（前方一致フィルタリング）:
  ┌─────────────────────────┐
  │ 傍点                     │  ← ［＃傍点］…［＃傍点終わり］
  │ 太字                     │  ← ［＃太字］…［＃太字終わり］
  │ 大見出し                  │  ← ［＃大見出し］…［＃大見出し終わり］
  │ 中見出し                  │  ← ［＃中見出し］…
  │ 小見出し                  │  ← ［＃小見出し］…
  │ 改ページ                  │  ← ［＃改ページ］
  │ ここから１字下げ            │  ← ［＃ここから１字下げ］
  │ ここで字下げ終わり          │  ← ［＃ここで字下げ終わり］
  │ ...（chuki_tag.txt から）  │
  └─────────────────────────┘

続けて入力: ［＃ぼう
  → フィルタリングされて:
  ┌─────────────────────────┐
  │ 傍点                     │
  │ 傍線                     │
  └─────────────────────────┘

Enter / Tab で選択 → 注記テキストが挿入される
```

#### 注記カテゴリと挿入テンプレート

| カテゴリ | 注記名 | 挿入されるテキスト | カーソル位置 |
|----------|--------|-------------------|-------------|
| 装飾（範囲） | 傍点 | `［＃傍点］{cursor}［＃傍点終わり］` | `{cursor}` 位置 |
| 装飾（範囲） | 太字 | `［＃太字］{cursor}［＃太字終わり］` | `{cursor}` 位置 |
| 装飾（範囲） | 斜体 | `［＃斜体］{cursor}［＃斜体終わり］` | `{cursor}` 位置 |
| 見出し（範囲） | 大見出し | `［＃大見出し］{cursor}［＃大見出し終わり］` | `{cursor}` 位置 |
| 見出し（範囲） | 中見出し | `［＃中見出し］{cursor}［＃中見出し終わり］` | `{cursor}` 位置 |
| 見出し（範囲） | 小見出し | `［＃小見出し］{cursor}［＃小見出し終わり］` | `{cursor}` 位置 |
| 構造（単体） | 改ページ | `［＃改ページ］` | 末尾 |
| 構造（単体） | 改丁 | `［＃改丁］` | 末尾 |
| 字下げ（開始） | ここから１字下げ | `［＃ここから１字下げ］` | 末尾 |
| 字下げ（開始） | ここから２字下げ | `［＃ここから２字下げ］` | 末尾 |
| 字下げ（終了） | ここで字下げ終わり | `［＃ここで字下げ終わり］` | 末尾 |
| 字詰め（開始） | ここから地付き | `［＃ここから地付き］` | 末尾 |
| 字詰め（終了） | ここで地付き終わり | `［＃ここで地付き終わり］` | 末尾 |
| ルビ | 注記付きルビ | `｜{cursor}《》` | `{cursor}` 位置 |

#### 辞書データソース: `ChukiDictionary`

```csharp
public sealed class ChukiSuggestItem
{
    public required string DisplayName { get; init; }    // ポップアップに表示する名前
    public required string Category { get; init; }       // カテゴリ（装飾/見出し/構造/字下げ）
    public required string InsertText { get; init; }     // 挿入するテキスト（{cursor} プレースホルダ含む）
    public required int CursorOffset { get; init; }      // 挿入後のカーソル位置（先頭からのオフセット）
}

public sealed class ChukiDictionary
{
    private readonly List<ChukiSuggestItem> _items;

    /// <summary>chuki_tag.txt + 組み込み定義からサジェスト辞書を構築</summary>
    public ChukiDictionary(string? chukiTagPath = null)
    {
        _items = BuildFromChukiTag(chukiTagPath);
    }

    /// <summary>前方一致で候補をフィルタリング</summary>
    public IReadOnlyList<ChukiSuggestItem> Search(string prefix)
        => _items.Where(i => i.DisplayName.StartsWith(prefix)).ToList();
}
```

`chuki_tag.txt` の解析方針:
- 各行の注記名（`＃` の後の日本語テキスト）をサジェスト候補として抽出
- 範囲注記（`…終わり` がある注記）は開始・終了をペアで管理
- 頻出注記（傍点、太字、見出し、改ページ等）はスコアを高くしてリスト上位に表示

### 4-4. ルビ入力アシスト (Ruby Input Assist)

#### 動作フロー

**パターン A: パイプ → 漢字 → 自動《》提示**

```
入力: |漢字
       ↑ ここでスペースや次の文字が入力されると…
  → 自動で《》を補完して |漢字《|》 に（| はカーソル位置）

入力: |漢字《かんじ》
  → 確定。閉じ括弧の》はスキップ動作
```

**パターン B: 漢字を選択 → Ctrl+R**

```
操作: 「漢字」を選択 → Ctrl+R
  → |漢字《|》 に変換（| はカーソル位置）
  → すぐにルビの読みを入力可能
```

**パターン C: ツールバーの「ルビ」ボタン**

```
操作: 「ルビ」ボタンをクリック
  → |《》 が挿入され、| のカーソルは | と 《 の間
  → テキスト選択中は選択テキストを | と 《 の間に配置
```

#### 実装: `EditorSuggestService.HandleRubyAssist()`

```csharp
public sealed class RubyAssistResult
{
    public required string TextToInsert { get; init; }
    public required int CursorOffset { get; init; }
    public required int SelectionStart { get; init; }   // 置換開始位置（選択テキスト用）
    public required int SelectionLength { get; init; }  // 置換長（選択テキスト用）
}

/// <summary>Ctrl+R or ツールバー「ルビ」ボタン</summary>
public RubyAssistResult InsertRuby(string? selectedText)
{
    if (string.IsNullOrEmpty(selectedText))
    {
        // 未選択: |《》を挿入、カーソルは | と《の間
        return new()
        {
            TextToInsert = "|《》",
            CursorOffset = 1,  // | の直後
            SelectionStart = 0,
            SelectionLength = 0
        };
    }

    // 選択あり: |選択テキスト《》、カーソルは《の直後
    return new()
    {
        TextToInsert = $"|{selectedText}《》",
        CursorOffset = selectedText.Length + 2,  // 《の直後
        SelectionStart = 0,
        SelectionLength = 0
    };
}
```

### 4-5. ツールバースニペット挿入 (Toolbar Snippet Insertion)

すべてのツールバーボタンは、挿入後のカーソル位置を正確に制御する。

| ボタン | ショートカット | 未選択時の挿入 | 選択時の挿入 | カーソル位置 |
|--------|---------------|---------------|-------------|-------------|
| ルビ | Ctrl+R | `\|《》` | `\|選択テキスト《》` | 《》の中 |
| 傍点 | Ctrl+E | `《《》》` | `《《選択テキスト》》` | 《《》》の中 |
| 太字 | Ctrl+B | `****` | `**選択テキスト**` | `**` の中 |
| 見出し | Ctrl+H | `# ` (行頭に) | `# 選択テキスト` | 行末 |
| 改ページ | Ctrl+Shift+P | `---\n` | `---\n` | 次の行頭 |
| 字下げ開始 | Ctrl+[ | `［＃ここから１字下げ］` | — | 末尾 |
| 字下げ終了 | Ctrl+] | `［＃ここで字下げ終わり］` | — | 末尾 |

#### スニペットエンジン

```csharp
public sealed class SnippetResult
{
    public required string TextToInsert { get; init; }
    public required int CursorOffset { get; init; }    // 挿入テキスト先頭からのカーソル位置
    public bool IsLineLevel { get; init; }             // true = 行頭に挿入（見出し等）
    public bool InsertNewlineAfter { get; init; }      // true = 改ページ等
}

public SnippetResult GetSnippet(string snippetId, string? selectedText)
{
    return snippetId switch
    {
        "ruby" => new()
        {
            TextToInsert = selectedText is null ? "|《》" : $"|{selectedText}《》",
            CursorOffset = selectedText is null ? 1 : selectedText.Length + 2
        },
        "emphasis" => new()
        {
            TextToInsert = selectedText is null ? "《《》》" : $"《《{selectedText}》》",
            CursorOffset = selectedText is null ? 2 : selectedText.Length + 2
        },
        "bold" => new()
        {
            TextToInsert = selectedText is null ? "****" : $"**{selectedText}**",
            CursorOffset = selectedText is null ? 2 : selectedText.Length + 2
        },
        "heading" => new()
        {
            TextToInsert = selectedText is null ? "# " : $"# {selectedText}",
            CursorOffset = selectedText is null ? 2 : selectedText.Length + 2,
            IsLineLevel = true
        },
        "pagebreak" => new()
        {
            TextToInsert = "---",
            CursorOffset = 3,
            IsLineLevel = true,
            InsertNewlineAfter = true
        },
        _ => throw new ArgumentException($"Unknown snippet: {snippetId}")
    };
}
```

### 4-6. サジェストポップアップ UI 仕様

```
┌───────────────────────────────────┐
│  カーソル位置（テキスト内）         │
│  ↓                               │
│  ┌─────────────────────────────┐  │
│  │ 傍点           装飾         │  │  ← 選択中（ハイライト）
│  │ 傍線           装飾         │  │
│  │ 太字           装飾         │  │
│  │ 大見出し        見出し       │  │
│  │ 改ページ        構造         │  │
│  └─────────────────────────────┘  │
└───────────────────────────────────┘
```

**UI 操作:**

| キー | 動作 |
|------|------|
| ↑ / ↓ | 候補選択 |
| Enter / Tab | 候補確定・挿入 |
| Esc | ポップアップを閉じる（入力は保持） |
| 文字入力 | フィルタリング更新 |
| BackSpace | フィルタリング文字を削除。トリガー文字まで消したらポップアップ閉じる |

**表示ルール:**
- 候補が 1 件のみの場合でも Enter/Tab で確定するまで自動挿入しない
- 候補が 0 件になったらポップアップを自動で閉じる
- 最大表示件数: 10 件（スクロール可能）
- カテゴリ名は右寄せで薄い色で表示

---

## 5. Phase E1-E2 の NovelFormatter（自動整形/Lint）詳細設計

### 5-1. Lint ルール一覧

| # | ルール名 | 入力例 | 出力例 | デフォルト | 備考 |
|---|---------|--------|--------|:---------:|------|
| L1 | 三点リーダー正規化 | `...` `。。。` `・・・` | `……` | ON | 2個セット |
| L2 | ダッシュ正規化 | `--` `──` | `――` | ON | 2倍ダッシュ |
| L3 | 感嘆符後スペース | `！次` `？次` `！？次` | `！　次` `？　次` `！？　次` | ON | 文末は除く |
| L4 | 行頭全角スペース | `　本文` | `　本文` | OFF | 段落字下げ自動挿入 |
| L5 | 連続空行圧縮 | 空行3行以上 | 空行2行まで | ON | 最大行数は設定可能 |
| L6 | 全角数字→漢数字 | `１２３` | そのまま | OFF | オプション（好み分かれる）|

### 5-2. Lint と変換エンジンの実行タイミング

```
ユーザー入力
    │
    ├─ [即座] 括弧ペアリング（EditorSuggestService）
    │         → テキスト変更イベント発火
    │
    ├─ [即座] 注記補完ポップアップ判定（EditorSuggestService）
    │         → トリガー文字検出のみ、テキスト変更なし
    │
    │  (300-500ms debounce)
    │
    ├─ [遅延] NovelFormatter.Format() ← Lint
    │         → テキスト内容を整形（差分のみ適用）
    │
    ├─ [遅延] EditorConversionEngine.Convert() ← 記法変換
    │         → 青空文庫記法テキスト生成
    │
    └─ [遅延] AozoraEpub3Converter → XHTML ← プレビュー更新
```

**重要: Lint はエディタのテキストを直接変更しない**

Lint の結果は以下の2つの方法で提示する:

1. **ステータスバーに警告数を表示**（例: `Lint: 3 warnings`）
2. **プレビュー生成時に自動適用**（変換パイプラインの一部として）

ユーザーが明示的に「整形」ボタン（またはショートカット）を押した場合のみ、エディタのテキストを直接変更する。これにより、**書いている最中にテキストが勝手に変わる**不快な体験を防ぐ。

---

## 6. ライブプレビューの性能目標

| 指標 | 目標値 | 手法 |
|------|--------|------|
| タイピング遅延 | 0ms | 変換処理は UI スレッド外 |
| 括弧ペアリング | < 1ms | 同期処理（単純な文字挿入） |
| サジェストポップアップ表示 | < 10ms | メモリ上の辞書検索 |
| プレビュー更新 | 300-500ms debounce 後 | Rx.NET `Throttle` (推奨) or `Task.Delay` + CancellationToken |
| 1万行テキスト変換 | < 100ms | GeneratedRegex + StringWriter |
| WebView2 DOM 更新 | < 50ms | innerHTML 差分注入 |
| メモリ使用量 | < 50MB 追加 | XHTML は毎回使い捨て |

---

## 7. 技術的リスクと対策

| リスク | 影響 | 対策 |
|--------|------|------|
| AozoraEpub3Converter が2パス前提 | ライブプレビューで1パス目（GetBookInfo）が重い | 初回のみ実行しキャッシュ。編集中は2パス目のみ |
| WebView2 の innerHTML 更新でちらつき | UX 悪化 | `requestAnimationFrame` 内で更新、スクロール位置保持 |
| 大規模テキスト（10万文字超）でのパフォーマンス | 変換遅延 | 可視範囲のチャプターのみ変換する差分戦略 |
| ~~Avalonia TextBox の制限~~ | ~~行番号、ハイライトなし~~ | **Phase E4 から AvaloniaEdit を採用**（下記「レビュー反映」参照） |
| 変換ルールの競合（ルビ内の傍点等） | 誤変換 | 変換順序を厳密に定義、エスケープ記法を用意 |
| サジェストポップアップの位置計算 | TextBox ではキャレット座標取得が困難 | Avalonia TextBox の `GetRectFromCharacterIndex()` 相当を使用。不可能な場合はエディタ下部に固定表示 |
| chuki_tag.txt の注記が多すぎる（728行） | ポップアップが煩雑 | カテゴリ分類 + 頻度スコアで上位10件に絞る。全件は下方向スクロール |
| 括弧ペアリングと IME の干渉 | 日本語入力中に括弧が二重挿入される | **`TextInput` イベントを使用**（`KeyDown` ではなく）。IME Composition 状態を判定しスキップ |
| WebView2 スクロール位置リセット | innerHTML 更新時にスクロールが先頭に戻る | JS で `scrollTop` を保存・復元（下記「レビュー反映」参照） |

---

## 8. キーボードショートカット一覧

| ショートカット | 動作 | Phase |
|--------------|------|-------|
| Ctrl+R | ルビ挿入 | E4 |
| Ctrl+B | 太字挿入 | E4 |
| Ctrl+E | 傍点挿入 | E4 |
| Ctrl+H | 見出し挿入（行頭 `#` 追加） | E4 |
| Ctrl+Shift+P | 改ページ挿入 | E4 |
| Ctrl+[ | 字下げ開始 | E4 |
| Ctrl+] | 字下げ終了 | E4 |
| Ctrl+Space | サジェスト強制表示 | E4 |
| Ctrl+Shift+F | 整形実行（Lint 適用） | E4 |
| Ctrl+S | 保存 | E5 |
| Ctrl+Shift+S | 名前を付けて保存 | E5 |
| Ctrl+N | 新規作成 | E5 |
| Ctrl+O | ファイルを開く | E5 |
| Ctrl+F | 検索 | E5 |
| Ctrl+Shift+H | 置換 | E5 |
| Ctrl+Z | Undo | E5 |
| Ctrl+Shift+Z | Redo | E5 |
| F5 | プレビューウィンドウ表示/非表示 | E4 |

---

## 9. EditorSuggestService 全体設計

```csharp
public sealed class EditorSuggestService
{
    private readonly ChukiDictionary _chukiDictionary;
    private readonly ConversionProfile _profile;

    // ── 括弧ペアリング ──
    public BracketPairResult? HandleBracketInput(
        char inputChar, char? charAfterCursor, char? charBeforeCursor,
        string? selectedText);

    public BracketPairResult? HandleBackspace(
        char? charBeforeCursor, char? charAfterCursor);

    // ── 注記補完 ──
    public bool ShouldShowSuggest(string textBeforeCursor);
    public IReadOnlyList<ChukiSuggestItem> GetSuggestions(string filterText);
    public SnippetResult ApplySuggestion(ChukiSuggestItem item, string? selectedText);

    // ── ルビアシスト ──
    public RubyAssistResult InsertRuby(string? selectedText);
    public bool ShouldTriggerRubyAssist(string textBeforeCursor);

    // ── ツールバースニペット ──
    public SnippetResult GetSnippet(string snippetId, string? selectedText);
}
```

### ViewModel との連携

```csharp
// EditorViewModel 内
public partial class EditorViewModel : ViewModelBase
{
    private readonly EditorSuggestService _suggestService;

    // サジェストポップアップの状態
    [ObservableProperty] private bool _isSuggestVisible;
    [ObservableProperty] private ObservableCollection<ChukiSuggestItem> _suggestItems;
    [ObservableProperty] private ChukiSuggestItem? _selectedSuggestItem;

    // テキスト入力時にサジェスト判定
    private void OnTextInput(char inputChar)
    {
        // 1. 括弧ペアリング
        var bracketResult = _suggestService.HandleBracketInput(
            inputChar, GetCharAfterCursor(), GetCharBeforeCursor(), SelectedText);
        if (bracketResult != null) { ApplyBracketResult(bracketResult); return; }

        // 2. 注記補完トリガー
        if (_suggestService.ShouldShowSuggest(GetTextBeforeCursor()))
        {
            IsSuggestVisible = true;
            UpdateSuggestFilter();
        }
    }
}
```

---

## 10. レビュー反映事項（Gemini レビュー 2026-03-07）

### 10-1. AvaloniaEdit を Phase E4 から採用（TextBox スキップ）

**元の計画:** Phase E4 で TextBox MVP → Phase E6 で AvaloniaEdit 差し替え

**変更後:** Phase E4 から最初に AvaloniaEdit を採用

**理由:**
- サジェストポップアップのキャレット座標計算が TextBox では泥臭いハックが必要
- AvaloniaEdit なら `TextArea.Caret.CalculateCaretBoundingBox()` で簡単に座標が取れる
- TextBox → AvaloniaEdit の載せ替えコストが高い（イベントモデルの差異）
- Phase E6 は「シンタックスハイライト」のみに変更

**NuGet:** `AvaloniaEdit` (NuGet ID: `AvaloniaEdit`)

### 10-2. WebView2 スクロール位置保持

innerHTML 差分更新時にスクロール位置がリセットされる問題の対策。
`ExecuteScriptAsync` で以下の JS を実行する:

```javascript
const st = document.documentElement.scrollTop;
document.getElementById('content').innerHTML = '新しいHTML';
document.documentElement.scrollTop = st;
```

縦書き（writing-mode: vertical-rl）の場合は `scrollLeft` も保持:

```javascript
const st = document.documentElement.scrollTop;
const sl = document.documentElement.scrollLeft;
document.getElementById('content').innerHTML = '新しいHTML';
document.documentElement.scrollTop = st;
document.documentElement.scrollLeft = sl;
```

### 10-3. Debounce に Rx.NET (System.Reactive) を使用

**元の計画:** `Task.Delay` + `CancellationToken` による手動デバウンス

**変更後:** `System.Reactive` の `Throttle` を使用

**理由:**
- Avalonia は System.Reactive と相性が良い
- メモリリークの心配なく数行でエレガントに実装可能
- `IDisposable` ベースの購読管理

```csharp
// EditorViewModel 内
_textChangedSubscription = Observable
    .FromEventPattern<EventArgs>(editor, nameof(editor.TextChanged))
    .Throttle(TimeSpan.FromMilliseconds(400))
    .ObserveOn(RxApp.MainThreadScheduler)
    .Subscribe(_ => UpdatePreview());
```

**NuGet:** `System.Reactive` (GUI プロジェクトに追加)

### 10-4. IME 干渉対策: TextInput イベントの使用

**元の計画:** `KeyDown` でペアリング処理

**変更後:** `TextInput` イベント（または AvaloniaEdit の `TextEntering` / `TextEntered`）を使用

**理由:**
- `KeyDown` は IME 変換中（未確定文字列の入力中）にも発火し、誤爆する
- AvaloniaEdit の `TextEntering` イベントは IME 確定後に発火するため安全
- `TextEntering` で `e.Cancel = true` とすることでカスタム挿入が可能

```csharp
// EditorView コードビハインド
textEditor.TextArea.TextEntering += (s, e) =>
{
    if (e.Text?.Length == 1)
    {
        var result = _suggestService.HandleBracketInput(
            e.Text[0], GetCharAfterCaret(), GetCharBeforeCaret(), GetSelectedText());
        if (result != null)
        {
            e.Cancel = true; // デフォルト入力をキャンセル
            ApplyBracketResult(result);
        }
    }
};
```

---

## 11. 実装状況

### Phase E1: 変換エンジン MVP — **実装完了**

| ファイル | 状態 |
|---------|:----:|
| `Core/Editor/ConversionProfile.cs` | 完了 |
| `Core/Editor/EditorConversionEngine.cs` | 完了 |
| `Core/Editor/NovelFormatter.cs` | 完了 |
| `Tests/EditorConversionEngineTests.cs` | 完了 (26テスト) |
| `Tests/NovelFormatterTests.cs` | 完了 (14テスト) |

### Phase E2: サジェスト・自動補完エンジン — **実装完了**

| ファイル | 状態 |
|---------|:----:|
| `Core/Editor/ChukiDictionary.cs` | 完了 |
| `Core/Editor/EditorSuggestService.cs` | 完了 |
| `Tests/ChukiDictionaryTests.cs` | 完了 (9テスト) |
| `Tests/EditorSuggestServiceTests.cs` | 完了 (28テスト) |

### Phase E3: ライブプレビュー基盤 — **実装完了**

| ファイル | 状態 |
|---------|:----:|
| `Gui/Services/LivePreviewService.cs` | 完了 |
| `Gui/ViewModels/EditorViewModel.cs` | 完了 |
| `Gui/Views/EditorView.axaml` + `.cs` | 完了 |
| `Tests/LivePreviewServiceTests.cs` | 完了 (11テスト) |

- LivePreviewService: ハイブリッド記法 → 青空文庫記法 → XHTML 変換パイプライン
- PreviewWriter: 軽量 IEpub3Writer 実装（ZIP 出力なし）
- EditorViewModel: デバウンス付きプレビュー更新、モード切替、ツールバーコマンド
- EditorView: TextBox + WebView2Host 分割表示、ショートカットキー
- MainWindow 統合: サイドバー「執筆」ボタン追加

### Phase E4: エディタ UI — **実装完了**

- AvaloniaEdit (TextEditor) 導入: 行番号表示、WordWrap、コードエディタ品質
- 括弧ペアリング統合: TextEntering イベントで IME 安全な処理
- 注記補完ポップアップ: ［＃ トリガーで候補表示、↑↓選択、Enter/Tab確定
- ショートカットキー: Ctrl+R/E/B/H, Ctrl+Shift+F/P, F5
- EditorSuggestService に ShouldShowSuggest / GetSuggestions 追加
### Phase E5: ファイル操作 + UX 改善 — 未着手
### Phase E6: 高度な機能 — 未着手
