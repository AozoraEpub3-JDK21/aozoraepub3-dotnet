# 引き継ぎ指示書 — UX改善：3層サイドバー

## ゴール

VISION.md の「読む人を作る人に変えるアプリ」に沿い、サイドバーを
**3+1アイテム（📖 読む / ✏️ 書く / 📘 本にする / ⚙️ 設定）** に整理する。

---

## 現状と問題

- 現在のサイドバーは 8 アイテム（ローカル変換 / Web変換 / プレビュー / 検証 / 執筆 / カード執筆 / 本にする / 設定）
- `docs/VISION.md` と `docs/feature-app-redesign-v2.md` では **3つしかないのがポイント** と明記
- 以下の変更が必要

---

## 変更するファイル（全4ファイル + 新規2ファイル）

### 1. 新規作成：`src/AozoraEpub3.Gui/ViewModels/ReadViewModel.cs`

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>
/// 層1「読む」のコンテナ ViewModel。
/// URL変換（WebConvertViewModel）とファイル変換（LocalConvertViewModel）を
/// タブ切り替えで統合する。
/// </summary>
public sealed partial class ReadViewModel : ViewModelBase
{
    public LocalConvertViewModel LocalConvertVm { get; }
    public WebConvertViewModel WebConvertVm { get; }

    public Action<string>? OnConversionCompleted { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentSubPage))]
    [NotifyPropertyChangedFor(nameof(IsFileMode))]
    private bool _isUrlMode = true;

    public bool IsFileMode => !IsUrlMode;

    public ViewModelBase CurrentSubPage => IsUrlMode ? (ViewModelBase)WebConvertVm : LocalConvertVm;

    public ReadViewModel()
    {
        WebConvertVm   = new WebConvertViewModel();
        LocalConvertVm = new LocalConvertViewModel();
        LocalConvertVm.OnConversionCompleted = path => OnConversionCompleted?.Invoke(path);
    }

    [RelayCommand]
    private void SwitchToUrl()  => IsUrlMode = true;

    [RelayCommand]
    private void SwitchToFile() => IsUrlMode = false;
}
```

---

### 2. 新規作成：`src/AozoraEpub3.Gui/Views/ReadView.axaml`

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:AozoraEpub3.Gui.ViewModels"
             x:Class="AozoraEpub3.Gui.Views.ReadView"
             x:DataType="vm:ReadViewModel">

  <Grid RowDefinitions="Auto,*">

    <!-- モード切り替えタブ -->
    <Border Grid.Row="0"
            BorderThickness="0,0,0,1"
            BorderBrush="{DynamicResource SystemControlForegroundBaseLowBrush}"
            Background="{DynamicResource SystemControlBackgroundChromeMediumLowBrush}"
            Padding="12,0">
      <StackPanel Orientation="Horizontal" Spacing="4" Height="44">
        <Button Content="🌐  URLから読む"
                Command="{Binding SwitchToUrlCommand}"
                Classes="mode-tab"
                Classes.active="{Binding IsUrlMode}"
                VerticalAlignment="Center" />
        <Button Content="📄  ファイルから読む"
                Command="{Binding SwitchToFileCommand}"
                Classes="mode-tab"
                Classes.active="{Binding IsFileMode}"
                VerticalAlignment="Center" />
      </StackPanel>
    </Border>

    <!-- サブコンテンツ（LocalConvertView / WebConvertView を自動解決） -->
    <ContentControl Grid.Row="1"
                    Content="{Binding CurrentSubPage}"
                    HorizontalContentAlignment="Stretch"
                    VerticalContentAlignment="Stretch" />
  </Grid>

  <UserControl.Styles>
    <Style Selector="Button.mode-tab">
      <Setter Property="Background"      Value="Transparent" />
      <Setter Property="BorderThickness"  Value="0" />
      <Setter Property="CornerRadius"    Value="6" />
      <Setter Property="Padding"         Value="16,6" />
      <Setter Property="FontSize"        Value="13" />
      <Setter Property="Cursor"          Value="Hand" />
    </Style>
    <Style Selector="Button.mode-tab:pointerover /template/ ContentPresenter">
      <Setter Property="Background" Value="{DynamicResource SystemControlHighlightListLowBrush}" />
    </Style>
    <Style Selector="Button.mode-tab.active">
      <Setter Property="Background" Value="{DynamicResource SystemAccentColor}" />
    </Style>
    <Style Selector="Button.mode-tab.active /template/ ContentPresenter">
      <Setter Property="Background"               Value="{DynamicResource SystemAccentColor}" />
      <Setter Property="TextElement.Foreground"   Value="White" />
    </Style>
    <Style Selector="Button.mode-tab.active:pointerover /template/ ContentPresenter">
      <Setter Property="Background" Value="{DynamicResource SystemAccentColorDark1}" />
    </Style>
  </UserControl.Styles>

</UserControl>
```

---

### 3. 新規作成：`src/AozoraEpub3.Gui/Views/ReadView.axaml.cs`

```csharp
using Avalonia.Controls;

namespace AozoraEpub3.Gui.Views;

public partial class ReadView : UserControl
{
    public ReadView()
    {
        InitializeComponent();
    }
}
```

---

### 4. 更新：`src/AozoraEpub3.Gui/Views/MainWindow.axaml`

既存ファイルを **全文置き換え** する：

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:AozoraEpub3.Gui.ViewModels"
        x:Class="AozoraEpub3.Gui.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="{DynamicResource Str_App_Title}"
        Icon="avares://AozoraEpub3.Gui/Assets/icon.ico"
        Width="1100" Height="720"
        MinWidth="800" MinHeight="560"
        WindowStartupLocation="CenterScreen">

  <Grid RowDefinitions="Auto,*">

    <!-- ヘッダー -->
    <Border Grid.Row="0"
            IsVisible="{Binding IsHeaderVisible}"
            Background="{DynamicResource SystemControlBackgroundChromeMediumLowBrush}"
            BorderBrush="{DynamicResource SystemControlForegroundBaseLowBrush}"
            BorderThickness="0,0,0,1"
            Padding="16,0">
      <Grid ColumnDefinitions="*,Auto" Height="48">
        <TextBlock Grid.Column="0"
                   Text="{DynamicResource Str_App_Title}"
                   FontSize="16" FontWeight="SemiBold"
                   VerticalAlignment="Center" />
        <StackPanel Grid.Column="1" Orientation="Horizontal"
                    Spacing="4" VerticalAlignment="Center">
          <Button Content="{DynamicResource Str_Lang_Ja}"
                  Command="{Binding SetLanguageCommand}"
                  CommandParameter="ja"
                  Classes.accent="{Binding IsJapanese}"
                  Padding="10,4" />
          <Button Content="{DynamicResource Str_Lang_En}"
                  Command="{Binding SetLanguageCommand}"
                  CommandParameter="en"
                  Classes.accent="{Binding !IsJapanese}"
                  Padding="10,4" />
        </StackPanel>
      </Grid>
    </Border>

    <!-- SplitView（サイドバー + コンテンツ） -->
    <SplitView Grid.Row="1"
               IsPaneOpen="{Binding IsSidebarVisible}"
               DisplayMode="CompactInline"
               PanePlacement="Left"
               PaneBackground="{DynamicResource SystemControlBackgroundChromeMediumBrush}"
               OpenPaneLength="180"
               CompactPaneLength="0">

      <SplitView.Pane>
        <Grid RowDefinitions="*,Auto">

          <!-- 上部ナビゲーション（3層） -->
          <StackPanel Grid.Row="0" Margin="0,16,0,0" Spacing="2">

            <!-- ① 読む -->
            <Button Command="{Binding NavigateToCommand}"
                    CommandParameter="read"
                    HorizontalAlignment="Stretch"
                    HorizontalContentAlignment="Left"
                    Padding="16,12"
                    Classes="nav-item"
                    Classes.active="{Binding IsReadPage}">
              <StackPanel Orientation="Horizontal" Spacing="10">
                <TextBlock Text="📖" FontSize="18" VerticalAlignment="Center" />
                <TextBlock Text="読む" FontSize="14" VerticalAlignment="Center" />
              </StackPanel>
            </Button>

            <!-- ② 書く -->
            <Button Command="{Binding NavigateToCommand}"
                    CommandParameter="write"
                    HorizontalAlignment="Stretch"
                    HorizontalContentAlignment="Left"
                    Padding="16,12"
                    Classes="nav-item"
                    Classes.active="{Binding IsWritePage}">
              <StackPanel Orientation="Horizontal" Spacing="10">
                <TextBlock Text="✏️" FontSize="18" VerticalAlignment="Center" />
                <TextBlock Text="書く" FontSize="14" VerticalAlignment="Center" />
              </StackPanel>
            </Button>

            <!-- ③ 本にする -->
            <Button Command="{Binding NavigateToCommand}"
                    CommandParameter="publish"
                    HorizontalAlignment="Stretch"
                    HorizontalContentAlignment="Left"
                    Padding="16,12"
                    Classes="nav-item"
                    Classes.active="{Binding IsPublishPage}">
              <StackPanel Orientation="Horizontal" Spacing="10">
                <TextBlock Text="📘" FontSize="18" VerticalAlignment="Center" />
                <TextBlock Text="本にする" FontSize="14" VerticalAlignment="Center" />
              </StackPanel>
            </Button>

          </StackPanel>

          <!-- 下部固定：設定 -->
          <StackPanel Grid.Row="1" Margin="0,0,0,12">
            <Button Command="{Binding NavigateToCommand}"
                    CommandParameter="settings"
                    HorizontalAlignment="Stretch"
                    HorizontalContentAlignment="Left"
                    Padding="16,12"
                    Classes="nav-item"
                    Classes.active="{Binding IsSettingsPage}">
              <StackPanel Orientation="Horizontal" Spacing="10">
                <TextBlock Text="⚙️" FontSize="18" VerticalAlignment="Center" />
                <TextBlock Text="{DynamicResource Str_Nav_Settings}"
                           FontSize="14" VerticalAlignment="Center" />
              </StackPanel>
            </Button>
          </StackPanel>

        </Grid>
      </SplitView.Pane>

      <SplitView.Content>
        <ContentControl Content="{Binding CurrentPage}"
                        HorizontalContentAlignment="Stretch"
                        VerticalContentAlignment="Stretch" />
      </SplitView.Content>

    </SplitView>
  </Grid>

  <Window.Styles>
    <Style Selector="Button.nav-item">
      <Setter Property="Background"      Value="Transparent" />
      <Setter Property="BorderThickness"  Value="0" />
      <Setter Property="CornerRadius"    Value="0" />
      <Setter Property="FontSize"        Value="14" />
    </Style>
    <Style Selector="Button.nav-item /template/ ContentPresenter">
      <Setter Property="Background" Value="Transparent" />
    </Style>
    <Style Selector="Button.nav-item:pointerover /template/ ContentPresenter">
      <Setter Property="Background"
              Value="{DynamicResource SystemControlHighlightListLowBrush}" />
    </Style>
    <Style Selector="Button.nav-item.active">
      <Setter Property="Background"
              Value="{DynamicResource SystemControlHighlightListMediumBrush}" />
    </Style>
    <Style Selector="Button.nav-item.active /template/ ContentPresenter">
      <Setter Property="Background"
              Value="{DynamicResource SystemControlHighlightListMediumBrush}" />
    </Style>
    <Style Selector="Button.nav-item.active:pointerover /template/ ContentPresenter">
      <Setter Property="Background"
              Value="{DynamicResource SystemControlHighlightListAccentLowBrush}" />
    </Style>
    <Style Selector="Button.accent">
      <Setter Property="Background" Value="{DynamicResource SystemAccentColor}" />
    </Style>
  </Window.Styles>

</Window>
```

---

### 5. 更新：`src/AozoraEpub3.Gui/ViewModels/MainWindowViewModel.cs`

既存ファイルを **全文置き換え** する：

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AozoraEpub3.Core.Editor;
using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Gui.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    // ── 子 ViewModel ──────────────────────────────────────────────────────
    public ReadViewModel       ReadVm      { get; } = new();
    public EditorViewModel     EditorVm    { get; } = new();
    public CardBoardViewModel  CardBoardVm { get; } = new();
    public CardEditorViewModel CardEditorVm { get; } = new();
    public SettingsPageViewModel SettingsVm { get; } = new();
    public PreviewViewModel    PreviewVm   { get; } = new();
    public ValidateViewModel   ValidateVm  { get; } = new();

    // 後方互換アクセサ
    public LocalConvertViewModel LocalConvertVm => ReadVm.LocalConvertVm;
    public WebConvertViewModel   WebConvertVm   => ReadVm.WebConvertVm;

    // ── SPA ルーティング ──────────────────────────────────────────────────
    [ObservableProperty]
    private ViewModelBase _currentPage;

    /// <summary>サイドバー選択状態：read / write / publish / settings</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReadPage))]
    [NotifyPropertyChangedFor(nameof(IsWritePage))]
    [NotifyPropertyChangedFor(nameof(IsPublishPage))]
    [NotifyPropertyChangedFor(nameof(IsSettingsPage))]
    private string _currentPageId = "read";

    public bool IsReadPage     => CurrentPageId == "read";
    public bool IsWritePage    => CurrentPageId == "write";
    public bool IsPublishPage  => CurrentPageId == "publish";
    public bool IsSettingsPage => CurrentPageId == "settings";

    public MainWindowViewModel()
    {
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

    [RelayCommand]
    private void NavigateTo(string page)
    {
        if (IsPreviewMaximized) IsPreviewMaximized = false;

        CurrentPageId = page switch
        {
            "read" or "local" or "web" => "read",
            "write" or "editor"        => "write",
            "cards"                    => "write",
            "publish" or "project"     => "publish",
            "settings"                 => "settings",
            _ => CurrentPageId   // preview / validate は内部遷移
        };

        if (page == "web")   ReadVm.IsUrlMode = true;
        if (page == "local") ReadVm.IsUrlMode = false;

        CurrentPage = page switch
        {
            "read" or "web" or "local" => ReadVm,
            "write" or "editor"        => EditorVm,
            "cards"                    => CardBoardVm,
            "publish" or "project"     => CardEditorVm,
            "settings"                 => SettingsVm,
            "preview"                  => PreviewVm,
            "validate"                 => ValidateVm,
            _                          => ReadVm
        };
    }

    // ── 言語・最大化 ──────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJapanese))]
    private string _currentLanguage = "ja";
    public bool IsJapanese => CurrentLanguage == "ja";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHeaderVisible))]
    [NotifyPropertyChangedFor(nameof(IsSidebarVisible))]
    private bool _isPreviewMaximized;
    public bool IsHeaderVisible  => !IsPreviewMaximized;
    public bool IsSidebarVisible => !IsPreviewMaximized;

    [RelayCommand]
    private void ToggleLanguage()
    {
        LocalizationService.Toggle();
        CurrentLanguage = LocalizationService.CurrentLanguage;
    }

    [RelayCommand]
    private void SetLanguage(string lang)
    {
        LocalizationService.SetLanguage(lang);
        CurrentLanguage = LocalizationService.CurrentLanguage;
    }

    // ── プレビュー連携 ────────────────────────────────────────────────────
    private void OnValidateRequested(string epubPath)
    {
        ValidateVm.ValidateCurrentEpub(epubPath, ValidateVm.JarPath);
        CurrentPage = ValidateVm;
    }

    private void OnJumpToFile(string fileName)
    {
        if (!PreviewVm.IsEpubLoaded) return;
        var metaPath = SourceMappingService.GetMetaFilePath(PreviewVm.EpubFilePath);
        var map = SourceMappingService.Load(metaPath);
        if (map == null) return;
        var chapter = map.Chapters.FirstOrDefault(c =>
            c.Href.EndsWith(fileName, StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(c.Href, StringComparison.OrdinalIgnoreCase));
        if (chapter != null)
        {
            PreviewVm.GoToPageCommand.Execute(chapter.SpineIndex);
            CurrentPage = PreviewVm;
        }
    }

    private void OnMigrateToProject(CardCollection collection)
    {
        var projectService = new ProjectService();
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AozoraEpub3", "projects");
        Directory.CreateDirectory(appDataDir);
        var projectDir = projectService.ImportFromCardCollection(appDataDir, collection);
        CardEditorVm.LoadProject(projectDir);
        CurrentPage   = CardEditorVm;
        CurrentPageId = "publish";
    }

    private void OnCardEpubConversion(string tempTextPath)
    {
        ReadVm.LocalConvertVm.AddFilePaths([tempTextPath]);
        ReadVm.IsUrlMode = false;
        CurrentPage   = ReadVm;
        CurrentPageId = "read";
    }

    private void OnEditorThemeChanged(EditorTheme theme)
    {
        EditorVm.CurrentTheme    = theme;
        CardBoardVm.CurrentTheme  = theme;
        CardEditorVm.CurrentTheme = theme;
    }

    private void OnFontSettingsChanged() { }

    public void OpenPreview(string epubPath)
    {
        PreviewVm.OpenEpubCommand.Execute(epubPath);
        CurrentPage = PreviewVm;
    }

    // ── 設定の永続化 ─────────────────────────────────────────────────────
    private void LoadSettings()
    {
        var s = AppSettingsStorage.Load();
        ReadVm.LocalConvertVm.Settings.LoadFrom(s);
        ReadVm.LocalConvertVm.OutputDirectory  = s.LastOutputDirectory;
        ReadVm.WebConvertVm.DownloadIntervalMs = s.DownloadIntervalMs;

        if (!string.IsNullOrEmpty(s.AppLanguage))
        {
            LocalizationService.SetLanguage(s.AppLanguage);
            CurrentLanguage        = s.AppLanguage;
            SettingsVm.SelectedLanguage = s.AppLanguage;
        }
        if (!string.IsNullOrEmpty(s.AppTheme))        SettingsVm.SelectedTheme = s.AppTheme;
        if (!string.IsNullOrEmpty(s.EpubcheckJarPath))
        {
            ValidateVm.JarPath         = s.EpubcheckJarPath;
            SettingsVm.EpubcheckJarPath = s.EpubcheckJarPath;
        }
        if (!string.IsNullOrEmpty(s.EditorThemeId))
        {
            SettingsVm.SetEditorThemeById(s.EditorThemeId);
            var theme = EditorThemes.GetById(s.EditorThemeId);
            EditorVm.CurrentTheme    = theme;
            CardBoardVm.CurrentTheme  = theme;
            CardEditorVm.CurrentTheme = theme;
        }
        CardBoardVm.SuppressMigrationProposals = s.SuppressMigrationProposals;
        if (!string.IsNullOrEmpty(s.EditorFontFamily))  SettingsVm.EditorFontFamily  = s.EditorFontFamily;
        if (s.EditorFontSize > 0)                       SettingsVm.EditorFontSize    = s.EditorFontSize;
        if (!string.IsNullOrEmpty(s.PreviewFontFamily)) SettingsVm.PreviewFontFamily = s.PreviewFontFamily;
        if (s.PreviewFontSize > 0)                      SettingsVm.PreviewFontSize   = s.PreviewFontSize;
    }

    public void SaveSettings()
    {
        var s = new GuiSettings();
        ReadVm.LocalConvertVm.Settings.SaveTo(s);
        s.LastOutputDirectory  = ReadVm.LocalConvertVm.OutputDirectory;
        s.DownloadIntervalMs   = ReadVm.WebConvertVm.DownloadIntervalMs;
        s.AppLanguage          = LocalizationService.CurrentLanguage;
        s.AppTheme             = SettingsVm.SelectedTheme;
        s.EpubcheckJarPath     = ValidateVm.JarPath;
        if (SettingsVm.EditorThemeIndex >= 0 && SettingsVm.EditorThemeIndex < EditorThemes.All.Length)
            s.EditorThemeId = EditorThemes.All[SettingsVm.EditorThemeIndex].Id;
        s.EditorFontFamily  = SettingsVm.EditorFontFamily;
        s.EditorFontSize    = SettingsVm.EditorFontSize;
        s.PreviewFontFamily = SettingsVm.PreviewFontFamily;
        s.PreviewFontSize   = SettingsVm.PreviewFontSize;
        s.SuppressMigrationProposals = CardBoardVm.SuppressMigrationProposals;
        AppSettingsStorage.Save(s);
    }
}
```

---

### 6. 更新：`src/AozoraEpub3.Gui/App.axaml`

`Application.DataTemplates` に `ReadViewModel → ReadView` のマッピングを追加する（先頭に追加）：

```xml
<DataTemplate DataType="{x:Type vm:ReadViewModel}">
  <views:ReadView />
</DataTemplate>
```

→ すでに追加済みの場合はスキップ。

---

## ビルド・確認手順

```bash
# 1. クリーンビルド
dotnet build AozoraEpub3.slnx

# 2. 起動確認
dotnet run --project src/AozoraEpub3.Gui/AozoraEpub3.Gui.csproj
```

確認ポイント：
- サイドバーが「📖 読む」「✏️ 書く」「📘 本にする」「⚙️ 設定」の 4 ボタンのみになっている
- 「読む」ページ内に「🌐 URLから読む」「📄 ファイルから読む」のトグルがある
- 各ボタンをクリックすると対応するページに遷移し、選択中ボタンがハイライトされる

---

## 次のステップ（完了後）

1. プレビュー完了後に「✏️ 自分でも書いてみる →」ボタンを追加（PreviewView）
2. 設定のフォントをテキスト入力からドロップダウンに変更（残件#2/#3）
3. CLI の `--interval` デフォルト 700ms（残件#4）
