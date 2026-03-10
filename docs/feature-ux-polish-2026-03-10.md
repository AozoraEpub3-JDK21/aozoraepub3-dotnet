# UX ポリッシュ実装指示書 v1.0
> 作成: 2026-03-10  |  担当: アーキテクト・レビュー
> 対象ブランチ: `main`（そのまま直コミット可）

---

## ビジョンとの照合

VISION.md の基本方針「**読む人を作る人に変えるアプリ**」に基づき、
本指示書は UI の細部品質を高める**仕上げ作業**を扱う。
大きな構造変更（3層サイドバー）は完了済みのため、
ここでは**使い心地の一貫性**を整えることを目的とする。

---

## 対象ファイル一覧

| # | 種別 | ファイルパス | 変更規模 |
|---|------|-------------|---------|
| 1 | 修正 | `src/AozoraEpub3.Cli/Program.cs` | 1行 |
| 2 | 修正 | `src/AozoraEpub3.Gui/Views/SettingsPageView.axaml` | 約20行 |
| 3 | 修正 | `src/AozoraEpub3.Gui/ViewModels/SettingsPageViewModel.cs` | 約10行 |

---

## 残件 #4 — CLI `--interval` デフォルト値の統一

### 背景と設計判断

GUI の `WebConvertViewModel` は初期値 **700ms** を採用している。
CLI の `--interval` は現状 **1500ms** のままで乖離している。
小説サイトの多くは 700ms で問題なく動作することが実績で確認されており、
GUI と CLI で挙動を揃えることがユーザーの混乱を防ぐ。

### 実装

**`src/AozoraEpub3.Cli/Program.cs`**

```diff
 var intervalOption = new Option<int>("--interval")
 {
-    DefaultValueFactory = _ => 1500,
+    DefaultValueFactory = _ => 700,
     Description = "URL変換時のダウンロード間隔 (ミリ秒)"
 };
```

> **注意点**: `WebAozoraConverter` 側に `Math.Max(500, DownloadIntervalMs)` の
> 下限ガードが実装済みのため、500ms 未満に設定しても 500ms にクランプされる。
> 700ms は安全圏内。

---

## 残件 #2/#3 — 設定画面フォント選択の UX 改善

### 背景と設計判断

現在「エディタフォント」「プレビューフォント」の両セクションは
**TextBox（手入力）** でフォント名を受け付けている。
これは上級者向けには柔軟だが、一般ユーザーが正確なフォント名を
知っていることを前提とした設計であり VISION のターゲットと合わない。

**改善方針**: システムにインストール済みのフォントを一覧表示する
`ComboBox` に変更する。空文字（テーマデフォルト）を先頭に置き、
「指定なし」を明示的に選べるようにする。

#### アーキテクチャ上のポイント

- Avalonia では `FontManager.Current.SystemFonts` でシステムフォント一覧を取得できる。
- フォント名リストは ViewModel で一度だけ生成してキャッシュする（UI スレッド初期化を避けるため `Lazy<string[]>` を使用）。
- `ComboBox` の `SelectedItem` は `string?` にバインドし、`null` または空文字はデフォルト扱いとする。
- 既存の `FontSettingsChanged` イベントは変更不要。

---

### 実装 A — `SettingsPageViewModel.cs`

フォント一覧プロパティを追加し、バインド先を `string?`（nullable）に変更する。

```csharp
// ───── フォント設定 ──────────────────────────────────────────────────────

// システムフォント一覧（空文字先頭 = テーマデフォルト）
public string[] AvailableFonts { get; } = BuildFontList();

private static string[] BuildFontList()
{
    var systemFonts = FontManager.Current.SystemFonts
        .Select(f => f.Name)
        .OrderBy(n => n)
        .Distinct()
        .ToArray();
    // 先頭に "（テーマデフォルト）" を示す空文字を追加
    return ["", .. systemFonts];
}

[ObservableProperty]
private string _editorFontFamily = "";

[ObservableProperty]
private double _editorFontSize = 14;

[ObservableProperty]
private string _previewFontFamily = "";

[ObservableProperty]
private double _previewFontSize = 16;
```

**using の追加が必要:**
```csharp
using Avalonia.Media;
```

---

### 実装 B — `SettingsPageView.axaml`

「エディタフォント」「プレビューフォント」の `TextBox` 部分のみ置き換える。
それ以外（言語・テーマ・エディタテーマ）は変更しない。

#### エディタフォント セクション（変更前→変更後）

```xml
<!-- 変更前 -->
<TextBox Grid.Row="0" Grid.Column="0"
         Text="{Binding EditorFontFamily}"
         Watermark="フォント名（空=テーマデフォルト）"
         MinWidth="200" />

<!-- 変更後 -->
<ComboBox Grid.Row="0" Grid.Column="0"
          ItemsSource="{Binding AvailableFonts}"
          SelectedItem="{Binding EditorFontFamily}"
          MinWidth="200"
          IsEditable="True"
          VirtualizationMode="Simple">
  <ComboBox.ItemTemplate>
    <DataTemplate>
      <TextBlock Text="{Binding}"
                 FontFamily="{Binding}"
                 FontSize="13" />
    </DataTemplate>
  </ComboBox.ItemTemplate>
</ComboBox>
```

#### プレビューフォント セクション（変更前→変更後）

```xml
<!-- 変更前 -->
<TextBox Grid.Column="0"
         Text="{Binding PreviewFontFamily}"
         Watermark="フォント名（空=テーマデフォルト）"
         MinWidth="200" />

<!-- 変更後 -->
<ComboBox Grid.Column="0"
          ItemsSource="{Binding AvailableFonts}"
          SelectedItem="{Binding PreviewFontFamily}"
          MinWidth="200"
          IsEditable="True"
          VirtualizationMode="Simple">
  <ComboBox.ItemTemplate>
    <DataTemplate>
      <TextBlock Text="{Binding}"
                 FontFamily="{Binding}"
                 FontSize="13" />
    </DataTemplate>
  </ComboBox.ItemTemplate>
</ComboBox>
```

> **設計メモ**:
> - `IsEditable="True"` を残す理由: システムフォントに一覧されないフォント名も
>   直接入力できる後方互換性を維持するため。
> - `VirtualizationMode="Simple"` を指定する理由: フォント一覧は数百件になりうるため
>   仮想化で描画コストを抑える。
> - `ItemTemplate` でフォントをプレビュー表示する理由: ユーザーが一覧から
>   見た目で選べるようにするため（VISION の「初心者でも使えるアプリ」に対応）。
> - 空文字アイテム（先頭）は `FontFamily` バインディングで空になるが、
>   Avalonia は空の FontFamily を無視するため表示上は問題ない。

---

## ビルドと確認手順

```bash
# ビルド
dotnet build AozoraEpub3.slnx

# GUI 起動確認
dotnet run --project src/AozoraEpub3.Gui/AozoraEpub3.Gui.csproj

# CLI の interval デフォルト確認
dotnet run --project src/AozoraEpub3.Cli -- --help
# → --interval の Description に "(ミリ秒)" が表示され、デフォルトが 700 になっていること
```

### GUI 確認チェックリスト

- [ ] ⚙️ 設定 →「エディタフォント」がドロップダウンになっている
- [ ] ドロップダウンにシステムフォント一覧が表示される（先頭は空 = デフォルト）
- [ ] フォント名をドロップダウンで選ぶと各フォントで描画されてプレビューできる
- [ ] 手入力（isEditable）でフォント名を直接入力しても動作する
- [ ] 「プレビューフォント」も同様の動作になっている
- [ ] 設定を変更して再起動しても値が保持されている（AppSettingsStorage 経由）

---

## 完了後の次ステップ

本指示書の実装が完了したら、次のフェーズとして
`docs/feature-editor-phase7.md` に記載の **E7c カード執筆機能の強化** に着手する。
具体的な次のタスクは:

1. カードプレビュー（書いている文章をリアルタイムで EPUB 相当の縦書き表示）
2. 「📖 読む → ✏️ 書く」の自動遷移フローの洗練（プレビュー後の誘導 UI）
