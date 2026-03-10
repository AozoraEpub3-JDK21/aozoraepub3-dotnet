# UX 修正指示書 — URL D&D / 文言 / ナビゲーション
> 作成: 2026-03-10  |  担当: アーキテクト・レビュー
> 前提: `feature-write-tab-toolbar-2026-03-10.md` と並行実装可

---

## 修正 A — URL変換への D&D 対応

### 問題

`WebConvertView.axaml` に D&D の実装がない。
`LocalConvertView` はファイルパスの D&D に対応しているのに、
URL変換画面は URLをテキストで手入力する必要がある。

### 受け入れたい操作

| 操作 | 期待する動作 |
|------|-------------|
| ブラウザのアドレスバーからURLをD&D | TextBox に URL が貼り付けられる |
| テキストエディタから文字列をD&D | 文字列がURLとして TextBox に入る |
| ブラウザのタブをD&D（`text/uri-list`形式） | URLが抽出されて TextBox に入る |

### 実装方針

Avalonia の `DragDrop` を使い、WebConvertView のメインエリア全体（または URL TextBox 部分）を
ドロップターゲットにする。LocalConvertView のコードビハインドにある
ドロップ処理ロジックと同じパターンで実装する。

---

### 実装 — `WebConvertView.axaml`

URL入力エリアの `TextBox` に D&D 属性を追加する:

```xml
<!-- ── URL 入力エリア ── -->
<StackPanel Grid.Row="0" Spacing="6" Margin="0,0,0,16">
  <TextBlock Text="{DynamicResource Str_Web_UrlLabel}"
             FontSize="14" FontWeight="SemiBold" />

  <!-- ドロップターゲットとして機能する外枠 -->
  <Border x:Name="UrlDropZone"
          BorderThickness="2"
          CornerRadius="8"
          Padding="0">
    <Border.Styles>
      <Style Selector="Border#UrlDropZone">
        <Setter Property="BorderBrush" Value="Transparent" />
      </Style>
      <Style Selector="Border#UrlDropZone.drag-over">
        <Setter Property="BorderBrush" Value="{DynamicResource SystemAccentColor}" />
        <Setter Property="Background"  Value="{DynamicResource SystemAccentColorLight3}" />
      </Style>
    </Border.Styles>

    <TextBox Text="{Binding Url}"
             Watermark="{DynamicResource Str_Web_UrlPlaceholder}"
             FontSize="14"
             DragDrop.AllowDrop="True" />
  </Border>

  <!-- ドロップヒント -->
  <TextBlock Text="URLをここにドロップできます"
             FontSize="11" Opacity="0.45"
             HorizontalAlignment="Right"
             Margin="0,-2,4,0" />
</StackPanel>
```

### 実装 — `WebConvertView.axaml.cs`

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AozoraEpub3.Gui.ViewModels;

namespace AozoraEpub3.Gui.Views;

public partial class WebConvertView : UserControl
{
    public WebConvertView()
    {
        InitializeComponent();

        // D&D イベント登録
        var dropZone = this.FindControl<Border>("UrlDropZone")!;
        DragDrop.SetAllowDrop(dropZone, true);
        dropZone.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        dropZone.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        dropZone.AddHandler(DragDrop.DropEvent,      OnDrop);
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (HasUrl(e))
        {
            e.DragEffects = DragDropEffects.Copy;
            (sender as Border)?.Classes.Add("drag-over");
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }
    }

    private void OnDragLeave(object? sender, DragEventArgs e)
        => (sender as Border)?.Classes.Remove("drag-over");

    private void OnDrop(object? sender, DragEventArgs e)
    {
        (sender as Border)?.Classes.Remove("drag-over");

        var url = ExtractUrl(e);
        if (url != null && DataContext is WebConvertViewModel vm)
            vm.Url = url;
    }

    // --- ヘルパー ---

    private static bool HasUrl(DragEventArgs e)
        => e.Data.Contains(DataFormats.Text)
        || e.Data.Contains("text/uri-list");

    private static string? ExtractUrl(DragEventArgs e)
    {
        // text/uri-list（ブラウザタブのD&D）を優先
        if (e.Data.Get("text/uri-list") is string uriList)
        {
            var first = uriList.Split('\n', '\r')[0].Trim();
            if (!string.IsNullOrEmpty(first)) return first;
        }
        // フォールバック: プレーンテキスト
        return e.Data.GetText();
    }
}
```

---

## 修正 B — 出力先プレースホルダー文言の修正

### 問題

`Strings.ja.axaml`:
```xml
<sys:String x:Key="Str_Setting_OutputDirPlaceholder">（入力ファイルと同じ場所）</sys:String>
```

この文言は `LocalConvertView`（ファイル変換）では正しいが、
`WebConvertView`（URL変換）では「入力ファイル」が存在しないため意味が通らない。

### 修正方針

プレースホルダーキーを用途別に分割する。

**`Assets/Strings.ja.axaml`**

```xml
<!-- 変更前 -->
<sys:String x:Key="Str_Setting_OutputDirPlaceholder">（入力ファイルと同じ場所）</sys:String>

<!-- 変更後: 2つのキーに分割 -->
<sys:String x:Key="Str_Setting_OutputDirPlaceholder">（入力ファイルと同じ場所）</sys:String>
<sys:String x:Key="Str_Web_OutputDirPlaceholder">（空欄 = デスクトップ）</sys:String>
```

**`Assets/Strings.en.axaml`** にも対応する英語キーを追加:
```xml
<sys:String x:Key="Str_Web_OutputDirPlaceholder">(empty = Desktop)</sys:String>
```

**`WebConvertView.axaml`** の Watermark を変更:
```xml
<!-- 変更前 -->
<TextBox ... Watermark="{DynamicResource Str_Setting_OutputDirPlaceholder}" />

<!-- 変更後 -->
<TextBox ... Watermark="{DynamicResource Str_Web_OutputDirPlaceholder}" />
```

> **設計メモ**: 「空欄 = デスクトップ」にした理由 — Web変換はファイルから始まらないので、
> 出力先が空の場合はユーザーが見つけやすい場所（デスクトップ）をデフォルトにするのが
> 自然。ただしデスクトップの実際のパスは `Environment.GetFolderPath(SpecialFolder.Desktop)` で
> 取得すること（WebConvertViewModel の変換実行ロジック側で対応する）。

---

## 修正 C — PreviewView / ValidateView の導線整理

### 現状

3層サイドバーへの移行により、PreviewView と ValidateView は
**サイドバーから直接アクセスできなくなった**。

現在の到達経路:
- PreviewView: 変換完了時に自動遷移（`MainWindowViewModel.OpenPreview()`）
- ValidateView: PreviewView 内の「検証」ボタン（`ValidateRequested` イベント）

### 意図的な設計か？

**Yes**。3層構造の思想として「プレビュー = 読む体験の完成形」であり、
「変換して初めてプレビューが使えるもの」という文脈は正しい。
ただし**既存EPUBを手動で開きたい場合**の導線がない。

### 改善案（今回実装する）

PreviewView のウェルカム画面（`IsEpubLoaded == false` の時）には
すでに「EPUBを開く」ボタンがある。これをユーザーが自然に発見できるように、
「📖 読む」ページの下部または変換完了メッセージに誘導リンクを追加する。

**具体的には ReadView.axaml 内（ファイル変換タブ下部）にリンクを追加**:

```xml
<!-- 既存 EPUB を開いてプレビューする場合の導線（ReadView の最下部） -->
<TextBlock Grid.Row="2"
           Text="既存の EPUB をプレビューする →"
           FontSize="12" Opacity="0.5"
           HorizontalAlignment="Right"
           Margin="0,8,8,8"
           Cursor="Hand">
  <TextBlock.InputBindings>
    <PointerBinding Gesture="LeftClick" Command="{Binding OpenPreviewCommand}" />
  </TextBlock.InputBindings>
</TextBlock>
```

> **`OpenPreviewCommand`** は `ReadViewModel` に追加し、
> `MainWindowViewModel` の `NavigateTo("preview")` に委譲する。

### 「本にする」内 Preview の整理（ユーザーへの説明）

`CardEditorView.axaml` の「Preview」ToggleButton は
**執筆中のカードをリアルタイムに縦書きXHTMLプレビューする機能**（`LivePreviewService`）です。
epubcheck による EPUB 検証とは別物です。

| 機能 | 場所 | 目的 |
|------|------|------|
| Live Preview（縦書きXHTML） | 「書く」「本にする」のツールバー Preview ボタン | 書きながら見た目を確認 |
| EPUB プレビュー（WebView2） | 変換後に自動遷移する PreviewView | 完成したEPUBを読む |
| epubcheck 検証 | PreviewView 内の「検証」ボタン → ValidateView | EPUB規格準拠チェック |

ツールチップや UI ラベルでこの違いを明示することを推奨する:

```xml
<!-- CardEditorView / EditorView の Preview ToggleButton を以下に変更 -->
<ToggleButton Content="縦書確認"
              IsChecked="{Binding IsPreviewVisible}"
              ToolTip.Tip="書きながら縦書きプレビュー (F5)"
              Padding="4,2" FontSize="12"/>
```

---

## まとめ — 対応ファイル一覧

| # | ファイル | 変更内容 |
|---|---------|---------|
| 1 | `Views/WebConvertView.axaml` | URL入力エリアに D&D 対応の Border + 属性追加 |
| 2 | `Views/WebConvertView.axaml.cs` | DragDrop イベントハンドラを追加 |
| 3 | `Assets/Strings.ja.axaml` | `Str_Web_OutputDirPlaceholder` キーを追加 |
| 4 | `Assets/Strings.en.axaml` | 同上（英語） |
| 5 | `Views/ReadView.axaml` | プレビューへの導線リンクを追加 |
| 6 | `ViewModels/ReadViewModel.cs` | `OpenPreviewCommand` を追加 |
| 7 | `ViewModels/MainWindowViewModel.cs` | `ReadVm.OpenPreviewRequested` イベントを配線 |
| 8 | `Views/CardEditorView.axaml` | Preview ボタンのラベルを「縦書確認」に変更 |
| 9 | `Views/EditorView.axaml` | 同上 |
| 10 | `Views/CardBoardView.axaml` | 同上 |

---

## 確認チェックリスト

- [ ] ブラウザのアドレスバーから URL テキストをドロップすると WebConvertView の TextBox に入る
- [ ] ドロップ中はドロップゾーンがアクセントカラーの枠で強調される
- [ ] Web変換の出力先プレースホルダーが「（空欄 = デスクトップ）」になっている
- [ ] ファイル変換の出力先プレースホルダーは「（入力ファイルと同じ場所）」のまま
- [ ] 「書く」「本にする」の Preview ボタンのラベルが「縦書確認」になっている
- [ ] 「読む」ページ下部に「既存のEPUBをプレビューする →」リンクがある
- [ ] リンクをクリックすると PreviewView のウェルカム画面（EPUB未読み込み）に遷移する
