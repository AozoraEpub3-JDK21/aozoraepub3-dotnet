# 「書く」タブ統合 & ツールバー整理 実装指示書
> 作成: 2026-03-10  |  担当: アーキテクト・レビュー
> 前提: `docs/feature-ux-polish-2026-03-10.md` のビルドが通っていること

---

## ゴールと設計方針

「読む」が ReadViewModel でカード/URL をタブ切り替えしているように、
「書く」も **WriteViewModel** というコンテナを作り、
**カード（デフォルト）↔ エディタ** をタブで切り替える。

あわせて、両画面のツールバーが横スクロール必須になっている問題を解消する。
最小ウィンドウ幅 800px・サイドバー 180px ＝ **コンテンツ幅 620px** で
ツールバーが1行に収まることを合格基準とする。

---

## 変更ファイル一覧

| # | 種別 | ファイル | 規模 |
|---|------|---------|------|
| 1 | 新規 | `ViewModels/WriteViewModel.cs` | 小（~30行） |
| 2 | 新規 | `Views/WriteView.axaml` | 小（~60行） |
| 3 | 新規 | `Views/WriteView.axaml.cs` | 極小 |
| 4 | 更新 | `Views/EditorView.axaml` | ツールバー部分のみ |
| 5 | 更新 | `Views/CardBoardView.axaml` | ツールバー部分のみ |
| 6 | 更新 | `ViewModels/MainWindowViewModel.cs` | 中（WriteVm への移行） |
| 7 | 更新 | `App.axaml` | DataTemplate 1行追加 |

---

## Part 1 — WriteViewModel（新規作成）

ReadViewModel と同じコンテナパターン。
`CardBoardVm` / `EditorVm` の所有権をここに移す。

**ファイル: `src/AozoraEpub3.Gui/ViewModels/WriteViewModel.cs`**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>
/// 層2「書く」のコンテナ ViewModel。
/// カードボード（デフォルト）とフルテキストエディタをタブ切り替えで統合する。
/// </summary>
public sealed partial class WriteViewModel : ViewModelBase
{
    public CardBoardViewModel CardBoardVm { get; }
    public EditorViewModel    EditorVm    { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSubPage))]
    [NotifyPropertyChangedFor(nameof(IsEditorMode))]
    private bool _isCardMode = true;   // デフォルト: カード

    public bool IsEditorMode => !IsCardMode;

    public ViewModelBase CurrentSubPage =>
        IsCardMode ? (ViewModelBase)CardBoardVm : EditorVm;

    public WriteViewModel(CardBoardViewModel cardBoardVm, EditorViewModel editorVm)
    {
        CardBoardVm = cardBoardVm;
        EditorVm    = editorVm;
    }

    [RelayCommand] private void SwitchToCard()   => IsCardMode = true;
    [RelayCommand] private void SwitchToEditor() => IsCardMode = false;
}
```

> **設計メモ**: DI コンストラクタ注入にした理由 — `CardBoardVm` と `EditorVm` は
> `MainWindowViewModel` のイベント配線が完了してから渡す必要があるため、
> `WriteViewModel` 内部で `new` せずに外から受け取る。

---

## Part 2 — WriteView（新規作成）

ReadView と同じ構造。タブトグル + ContentControl。

**ファイル: `src/AozoraEpub3.Gui/Views/WriteView.axaml`**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:AozoraEpub3.Gui.ViewModels"
             x:Class="AozoraEpub3.Gui.Views.WriteView"
             x:DataType="vm:WriteViewModel">

  <Grid RowDefinitions="Auto,*">

    <!-- モード切り替えタブ -->
    <Border Grid.Row="0"
            BorderThickness="0,0,0,1"
            BorderBrush="{DynamicResource SystemControlForegroundBaseLowBrush}"
            Background="{DynamicResource SystemControlBackgroundChromeMediumLowBrush}"
            Padding="12,0">
      <StackPanel Orientation="Horizontal" Spacing="4" Height="40">
        <Button Content="🃏  カード"
                Command="{Binding SwitchToCardCommand}"
                Classes="mode-tab"
                Classes.active="{Binding IsCardMode}"
                VerticalAlignment="Center" />
        <Button Content="📝  エディタ"
                Command="{Binding SwitchToEditorCommand}"
                Classes="mode-tab"
                Classes.active="{Binding IsEditorMode}"
                VerticalAlignment="Center" />
      </StackPanel>
    </Border>

    <!-- サブコンテンツ（CardBoardView / EditorView を自動解決） -->
    <ContentControl Grid.Row="1"
                    Content="{Binding CurrentSubPage}"
                    HorizontalContentAlignment="Stretch"
                    VerticalContentAlignment="Stretch" />
  </Grid>

  <UserControl.Styles>
    <!-- ReadView と同じ mode-tab スタイル -->
    <Style Selector="Button.mode-tab">
      <Setter Property="Background"     Value="Transparent" />
      <Setter Property="BorderThickness" Value="0" />
      <Setter Property="CornerRadius"   Value="6" />
      <Setter Property="Padding"        Value="14,5" />
      <Setter Property="FontSize"       Value="13" />
      <Setter Property="Cursor"         Value="Hand" />
    </Style>
    <Style Selector="Button.mode-tab:pointerover /template/ ContentPresenter">
      <Setter Property="Background"
              Value="{DynamicResource SystemControlHighlightListLowBrush}" />
    </Style>
    <Style Selector="Button.mode-tab.active /template/ ContentPresenter">
      <Setter Property="Background"             Value="{DynamicResource SystemAccentColor}" />
      <Setter Property="TextElement.Foreground" Value="White" />
    </Style>
    <Style Selector="Button.mode-tab.active:pointerover /template/ ContentPresenter">
      <Setter Property="Background"
              Value="{DynamicResource SystemAccentColorDark1}" />
    </Style>
  </UserControl.Styles>

</UserControl>
```

**ファイル: `src/AozoraEpub3.Gui/Views/WriteView.axaml.cs`**

```csharp
using Avalonia.Controls;

namespace AozoraEpub3.Gui.Views;

public partial class WriteView : UserControl
{
    public WriteView() { InitializeComponent(); }
}
```

---

## Part 3 — ツールバー整理（EditorView & CardBoardView）

### 問題の根因

現在のツールバーは以下の要素が横一列に並んでいる:

```
[新規][開く][保存] | ComboBox | [ルビ][傍点][太字][見出し][改頁] | [縦書][整形] | [Preview] | [校正] | [EPUB変換] | [本にする→] | [？]
```

800px ウィンドウ・620px コンテンツ幅でこれを収めることは物理的に不可能。

### 整理方針

1. **ファイル操作（新規/開く/保存）** を「≡」ドロップダウンメニューに格納する
2. **モード切替 ComboBox** を WriteView のタブに昇格させたので**削除**
3. **校正・本にする→** をドロップダウンの「ツール」セクションに移動
4. 残るプライマリボタンをコンパクト化（Padding 削減）

### 整理後のツールバー構成（共通部分）

```
[≡ ▼]  |  [ルビ][傍点][太字][見出し][改頁]  |  [縦書][整形]  |  [Preview]  |  [変換]  [？]
```

推定幅: 約 500px → 620px に余裕を持って収まる。

---

### 実装 A — EditorView.axaml のツールバー

ツールバー `StackPanel`（Row 0 の ScrollViewer 内）を以下に**全文置き換え**する:

```xml
<StackPanel Orientation="Horizontal" Spacing="4" Margin="8,4">

  <!-- ≡ ファイルメニュー -->
  <Button Content="≡" Padding="6,2" FontSize="14"
          ToolTip.Tip="ファイル操作">
    <Button.Flyout>
      <MenuFlyout>
        <MenuItem Header="新規 (Ctrl+N)"  Command="{Binding NewFileCommand}" />
        <MenuItem Header="開く (Ctrl+O)"  Command="{Binding OpenFileCommand}" />
        <MenuItem Header="保存 (Ctrl+S)"  Command="{Binding SaveFileCommand}" />
        <Separator/>
        <MenuItem Header="校正"           Command="{Binding ProofreadCommand}" />
        <MenuItem Header="EPUB変換"       Command="{Binding ConvertToEpubCommand}" />
      </MenuFlyout>
    </Button.Flyout>
  </Button>

  <Separator/>

  <!-- 記法挿入 -->
  <Button Content="ルビ"   Command="{Binding InsertSnippetCommand}" CommandParameter="ruby"
          ToolTip.Tip="Ctrl+R" Padding="4,2" FontSize="12"/>
  <Button Content="傍点"   Command="{Binding InsertSnippetCommand}" CommandParameter="emphasis"
          ToolTip.Tip="Ctrl+E" Padding="4,2" FontSize="12"/>
  <Button Content="太字"   Command="{Binding InsertSnippetCommand}" CommandParameter="bold"
          ToolTip.Tip="Ctrl+B" Padding="4,2" FontSize="12"/>
  <Button Content="見出し" Command="{Binding InsertSnippetCommand}" CommandParameter="heading"
          ToolTip.Tip="Ctrl+H" Padding="4,2" FontSize="12"/>
  <Button Content="改頁"   Command="{Binding InsertSnippetCommand}" CommandParameter="pagebreak"
          Padding="4,2" FontSize="12"/>

  <Separator/>

  <!-- 書式 -->
  <ToggleButton Content="縦書" IsChecked="{Binding IsVertical}"  Padding="4,2" FontSize="12"/>
  <Button Content="自動修正" Command="{Binding FormatTextCommand}"
          ToolTip.Tip="……・──など日本語記号を自動補正 (Ctrl+Shift+F)"
          Padding="4,2" FontSize="12"/>

  <Separator/>

  <!-- プレビュー・ヘルプ -->
  <ToggleButton Content="縦書確認" IsChecked="{Binding IsPreviewVisible}"
                ToolTip.Tip="書きながら縦書きプレビュー (F5)"
                Padding="4,2" FontSize="12"/>

  <Separator/>

  <!-- 変換（強調）-->
  <Button Content="EPUB変換" Command="{Binding ConvertToEpubCommand}"
          Padding="4,2" FontSize="12" FontWeight="SemiBold"/>

  <Separator/>

  <ToggleButton Content="？" IsChecked="{Binding CheatSheet.IsVisible}"
                ToolTip.Tip="F1" Padding="4,2" FontSize="12"/>

</StackPanel>
```

> **変更点まとめ**:
> - `ScrollViewer` ラッパーは**削除**（もう不要なため）
> - 新規/開く/保存/校正 → `≡` MenuFlyout に移動
> - `ComboBox`（ModeNames）→ WriteView タブに昇格したため**削除**
> - 「整形」→「**自動修正**」にリネーム＋ツールチップで何をするか明示
> - 「Preview」→「**縦書確認**」にリネーム（EPUBプレビューと区別するため）
> - `Padding="4,2"` に統一してボタン幅を詰める

---

### 実装 B — CardBoardView.axaml のツールバー

EditorView と同様。ただし「本にする→」「ファイル操作なし」の差分あり:

```xml
<StackPanel Orientation="Horizontal" Spacing="4" Margin="8,4">

  <!-- ≡ ツールメニュー（CardBoard はファイル操作なし）-->
  <Button Content="≡" Padding="6,2" FontSize="14" ToolTip.Tip="その他">
    <Button.Flyout>
      <MenuFlyout>
        <MenuItem Header="EPUB変換" Command="{Binding ConvertToEpubCommand}" />
        <Separator/>
        <MenuItem Header="本にする →" Command="{Binding AcceptMigrationCommand}" />
      </MenuFlyout>
    </Button.Flyout>
  </Button>

  <Separator/>

  <!-- 記法挿入 -->
  <Button Content="ルビ"   Command="{Binding InsertSnippetCommand}" CommandParameter="ruby"
          ToolTip.Tip="Ctrl+R" Padding="4,2" FontSize="12"/>
  <Button Content="傍点"   Command="{Binding InsertSnippetCommand}" CommandParameter="emphasis"
          ToolTip.Tip="Ctrl+E" Padding="4,2" FontSize="12"/>
  <Button Content="太字"   Command="{Binding InsertSnippetCommand}" CommandParameter="bold"
          ToolTip.Tip="Ctrl+B" Padding="4,2" FontSize="12"/>
  <Button Content="見出し" Command="{Binding InsertSnippetCommand}" CommandParameter="heading"
          ToolTip.Tip="Ctrl+H" Padding="4,2" FontSize="12"/>
  <Button Content="改頁"   Command="{Binding InsertSnippetCommand}" CommandParameter="pagebreak"
          Padding="4,2" FontSize="12"/>

  <Separator/>

  <!-- 書式 -->
  <ToggleButton Content="縦書" IsChecked="{Binding IsVertical}"  Padding="4,2" FontSize="12"/>
  <Button Content="自動修正" Command="{Binding FormatTextCommand}"
          ToolTip.Tip="……・──など日本語記号を自動補正 (Ctrl+Shift+F)"
          Padding="4,2" FontSize="12"/>

  <Separator/>

  <ToggleButton Content="縦書確認" IsChecked="{Binding IsPreviewVisible}"
                ToolTip.Tip="書きながら縦書きプレビュー (F5)"
                Padding="4,2" FontSize="12"/>

  <Separator/>

  <ToggleButton Content="？" IsChecked="{Binding CheatSheet.IsVisible}"
                ToolTip.Tip="F1" Padding="4,2" FontSize="12"/>

</StackPanel>
```

> **変更点まとめ**:
> - `ScrollViewer` ラッパー**削除**
> - `ComboBox`（ModeNames）**削除**（WriteView タブへ）
> - 「本にする→」「EPUB変換」→ `≡` MenuFlyout に移動
> - 「整形」→「**自動修正**」、「Preview」→「**縦書確認**」にリネーム
> - よく使うボタン（ルビ〜縦書確認）はプライマリに残す

---

## Part 4 — MainWindowViewModel.cs の更新

### 変更の概要

- `EditorVm` と `CardBoardVm` の所有権を `WriteVm` に移す
- `NavigateTo("write")` / `"cards"` / `"editor"` をすべて `WriteVm` にルーティング

### WriteVm の初期化（コンストラクタ内）

```csharp
// ── 子 ViewModel ──────────────────────────────────────────────────────
public ReadViewModel        ReadVm      { get; } = new();
public EditorViewModel      EditorVm    { get; } = new();
public CardBoardViewModel   CardBoardVm { get; } = new();
public WriteViewModel       WriteVm     { get; }          // ← 追加
public CardEditorViewModel  CardEditorVm { get; } = new();
public SettingsPageViewModel SettingsVm { get; } = new();
public PreviewViewModel     PreviewVm   { get; } = new();
public ValidateViewModel    ValidateVm  { get; } = new();

public MainWindowViewModel()
{
    // WriteVm は CardBoardVm・EditorVm の配線後に生成
    WriteVm = new WriteViewModel(CardBoardVm, EditorVm);

    _currentPage = ReadVm;

    ReadVm.OnConversionCompleted = OpenPreview;

    PreviewVm.ToggleMaximizeRequested  += () => IsPreviewMaximized = !IsPreviewMaximized;
    PreviewVm.ValidateRequested        += OnValidateRequested;
    PreviewVm.NavigateToCardsRequested += () => NavigateTo("cards");

    ValidateVm.JumpToFileRequested += OnJumpToFile;

    CardBoardVm.EpubConversionRequested   += OnCardEpubConversion;
    CardBoardVm.MigrateToProjectRequested += OnMigrateToProject;
    CardEditorVm.EpubConversionRequested  += OnCardEpubConversion;
    EditorVm.EpubConversionRequested      += OnCardEpubConversion;

    SettingsVm.EditorThemeSelectionChanged += OnEditorThemeChanged;
    SettingsVm.FontSettingsChanged         += OnFontSettingsChanged;

    LoadSettings();
}
```

### NavigateTo の更新

```csharp
[RelayCommand]
private void NavigateTo(string page)
{
    if (IsPreviewMaximized) IsPreviewMaximized = false;

    CurrentPageId = page switch
    {
        "read" or "local" or "web"          => "read",
        "write" or "editor" or "cards"      => "write",   // ← "cards" も "write" 扱い
        "publish" or "project"              => "publish",
        "settings"                          => "settings",
        _                                   => CurrentPageId
    };

    if (page == "web")     ReadVm.IsUrlMode  = true;
    if (page == "local")   ReadVm.IsUrlMode  = false;
    if (page == "editor")  WriteVm.IsCardMode = false;   // ← エディタタブを選択
    if (page == "cards")   WriteVm.IsCardMode = true;    // ← カードタブを選択

    CurrentPage = page switch
    {
        "read" or "web" or "local"          => ReadVm,
        "write" or "editor" or "cards"      => WriteVm,   // ← WriteVm を返す
        "publish" or "project"              => CardEditorVm,
        "settings"                          => SettingsVm,
        "preview"                           => PreviewVm,
        "validate"                          => ValidateVm,
        _                                   => ReadVm
    };
}
```

---

## Part 5 — App.axaml の更新

`Application.DataTemplates` に `WriteViewModel → WriteView` を追加（ReadViewModel の直後）:

```xml
<DataTemplate DataType="{x:Type vm:ReadViewModel}">
  <views:ReadView />
</DataTemplate>

<!-- ↓ ここを追加 -->
<DataTemplate DataType="{x:Type vm:WriteViewModel}">
  <views:WriteView />
</DataTemplate>
```

---

## ビルドと確認手順

```bash
dotnet build AozoraEpub3.slnx
dotnet run --project src/AozoraEpub3.Gui/AozoraEpub3.Gui.csproj
```

### 確認チェックリスト

- [ ] サイドバー「✏️ 書く」を押すと WriteView が開き「🃏 カード」タブがデフォルト選択
- [ ] 「📝 エディタ」タブに切り替えるとフルテキストエディタが表示される
- [ ] タブ切り替え後に「✏️ 書く」を再クリックしても最後に選択したタブが保持される
- [ ] カードボード画面でツールバーが横スクロールなしで収まっている
- [ ] エディタ画面でツールバーが横スクロールなしで収まっている
- [ ] 「≡」メニューからファイル操作・校正・本にする が呼び出せる
- [ ] プレビュー「自分でも書いてみる →」→ WriteView のカードタブへ遷移する

---

## 設計判断メモ（レビュー記録）

**Q: なぜカードをデフォルトにするのか？**
VISION「読む人を作る人に変える」の観点では、エディタより「カードを1枚書く」
という軽い行動を先に見せることが初心者の心理的障壁を下げるため。
エディタは上級者向けの「もう1つの選択肢」として控えめに配置する。

**Q: ツールバーの「本にする→」をメニューに隠すのはよいか？**
カードから本にするフローは重要だが、ツールバーの中では場違い感がある。
`WriteView` タブ右端に「→ 本にする」リンクを追加するほうがより自然かもしれない。
ただし今回は最小変更で実装し、使い勝手の評価後に判断する。

**Q: MenuFlyout は Avalonia で利用可能か？**
Avalonia 11 以降で `Button.Flyout` + `MenuFlyout` は標準サポート済み。
`ContextMenu` の代替として使用できる。
