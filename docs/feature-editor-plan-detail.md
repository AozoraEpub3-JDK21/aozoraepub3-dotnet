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
| `chuki_tag.txt` | `Core/Resources/` | 青空文庫注記→HTML タグ変換表（728行） |
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
│  └──────────┬───────────────────────────────────────┘    │
│             │ TextChanged (debounce 300-500ms)           │
│             ▼                                            │
│  ┌──────────────────────────────────────────────────┐    │
│  │ EditorConversionEngine (バックグラウンド)          │    │
│  │  1. ハイブリッド記法 → 青空文庫記法               │    │
│  │  2. 自動整形 (Lint/Formatter)                    │    │
│  │  3. AozoraEpub3Converter → XHTML 断片           │    │
│  └──────────┬───────────────────────────────────────┘    │
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
| E1-3 | 小説向け自動整形 (Lint) | `Core/Editor/NovelFormatter.cs` | 三点リーダー、ダッシュ、感嘆符後スペース |
| E1-4 | 単体テスト | `Tests/EditorConversionEngineTests.cs` | 各変換ルールのテスト |
| E1-5 | AozoraTextFinalizer 連携 | (既存コードの薄いラッパー) | 既存の空行圧縮・字下げ等を再利用 |

### Phase E2: ライブプレビュー基盤
**ゴール:** テキスト入力 → XHTML 生成 → WebView2 表示がリアルタイムで動く

| # | タスク | クラス/ファイル | 概要 |
|---|--------|----------------|------|
| E2-1 | LivePreviewService 実装 | `Gui/Services/LivePreviewService.cs` | デバウンス + バックグラウンド変換 + XHTML 生成 |
| E2-2 | AozoraEpub3Converter 拡張 | `Core/Converter/` | シングルチャプター XHTML 出力メソッド追加 |
| E2-3 | PreviewWindow 実装 | `Gui/Views/PreviewWindow.axaml` | 独立ウィンドウ（WebView2Host 再利用）|
| E2-4 | WebView2Host 拡張 | `Gui/Controls/WebView2Host.cs` | `UpdateContent(html)` メソッド追加（innerHTML 差分更新）|
| E2-5 | 縦書き CSS テンプレート | (既存 `vertical_text.sbn` ベース) | ライブプレビュー用の最小 CSS |

### Phase E3: エディタ UI
**ゴール:** GUI 上でハイブリッド記法テキストを入力し、リアルタイムプレビューが動く

| # | タスク | クラス/ファイル | 概要 |
|---|--------|----------------|------|
| E3-1 | EditorViewModel 実装 | `Gui/ViewModels/EditorViewModel.cs` | テキスト管理、モード選択、ツールバーコマンド |
| E3-2 | EditorView (MVP: TextBox) | `Gui/Views/EditorView.axaml` | メインエディタ画面（TextBox ベース）|
| E3-3 | ツールバー実装 | (EditorView 内) | ルビ・傍点・太字・見出し・改ページ挿入ボタン |
| E3-4 | モード切替 ComboBox | (EditorView 内) | なろう/カクヨム/汎用 |
| E3-5 | ショートカットキー | (EditorView コードビハインド) | Ctrl+R(ルビ), Ctrl+B(太字) 等 |
| E3-6 | MainWindow 統合 | サイドバーに「執筆」ボタン追加 | SPA ルーティングに EditorViewModel 追加 |

### Phase E4: ファイル操作 + UX 改善
**ゴール:** 実用的な執筆ツールとして使える

| # | タスク | 概要 |
|---|--------|------|
| E4-1 | ファイル新規作成/開く/保存 | `.txt` / `.md` ファイルの読み書き |
| E4-2 | 最近使ったファイル | 設定に保存、起動時に復元 |
| E4-3 | 自動保存（ドラフト） | 30秒ごとに `.draft` に自動バックアップ |
| E4-4 | 文字数カウント | ステータスバーにリアルタイム表示 |
| E4-5 | 検索/置換 | Ctrl+F / Ctrl+H |
| E4-6 | Undo/Redo 強化 | TextBox 標準の Undo + 拡張 |

### Phase E5: 高度な機能（将来）
| # | タスク | 概要 |
|---|--------|------|
| E5-1 | AvaloniaEdit 導入 | シンタックスハイライト（記法の色分け）|
| E5-2 | 変換ルール外部化 | JSON/YAML カスタム辞書読み込み |
| E5-3 | EPUB 直接書き出し | エディタからの直接 EPUB 生成 |
| E5-4 | 章分割エディタ | 複数ファイルのプロジェクト管理 |
| E5-5 | 校正支援 | 表記ゆれチェック、重複表現検出 |

---

## 3. Phase E1 詳細設計

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
    │  > 引用 → ［＃ここから１字下げ］引用[\＃ここで字下げ終わり]
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

---

## 4. ライブプレビューの性能目標

| 指標 | 目標値 | 手法 |
|------|--------|------|
| タイピング遅延 | 0ms | 変換処理は UI スレッド外 |
| プレビュー更新 | 300-500ms debounce 後 | `Task.Delay` + CancellationToken |
| 1万行テキスト変換 | < 100ms | GeneratedRegex + StringWriter |
| WebView2 DOM 更新 | < 50ms | innerHTML 差分注入 |
| メモリ使用量 | < 50MB 追加 | XHTML は毎回使い捨て |

---

## 5. 技術的リスクと対策

| リスク | 影響 | 対策 |
|--------|------|------|
| AozoraEpub3Converter が2パス前提 | ライブプレビューで1パス目（GetBookInfo）が重い | 初回のみ実行しキャッシュ。編集中は2パス目のみ |
| WebView2 の innerHTML 更新でちらつき | UX 悪化 | `requestAnimationFrame` 内で更新、スクロール位置保持 |
| 大規模テキスト（10万文字超）でのパフォーマンス | 変換遅延 | 可視範囲のチャプターのみ変換する差分戦略 |
| Avalonia TextBox の制限 | 行番号、ハイライトなし | Phase E5 で AvaloniaEdit に差し替え |
| 変換ルールの競合（ルビ内の傍点等） | 誤変換 | 変換順序を厳密に定義、エスケープ記法を用意 |
