# E7-C-Lite 実装指示書 — 初心者カードモード

> Claude CLI (Claude Code) 向けの実装指示書
> 対象: AozoraEpub3-dotnet (D:\git\aozoraepub3-dotnet)
> 基準: docs/VISION.md, docs/feature-editor-phase7.md セクション3

---

## 概要

現在の執筆モード（EditorView: 1テキスト＝1冊）に加えて、
「話単位のカード」で書ける初心者カードモードを追加する。

ユーザー感情目標: **「こんなに書けた！」**
書いた話がカードとして並ぶことで積み上げを実感できる。

---

## 前提知識

### アーキテクチャ
- Avalonia UI 11 + CommunityToolkit.Mvvm
- SPA ルーティング: MainWindowViewModel.CurrentPage を切替、App.axaml の DataTemplate で View 自動解決
- dotnet: `C:\Program Files\dotnet\dotnet.exe`（PATH未設定、フルパス使用）
- ビルド: `cd /d D:\git\aozoraepub3-dotnet && "C:\Program Files\dotnet\dotnet.exe" build src\AozoraEpub3.Gui\AozoraEpub3.Gui.csproj`

### 既存の執筆モード
- EditorViewModel (ViewModels/EditorViewModel.cs): テキスト管理、デバウンス、プレビュー連携
- EditorView (Views/EditorView.axaml): TextBox + WebView2Host 分割表示
- LivePreviewService: ハイブリッド記法 → 青空文庫記法 → XHTML 変換
- サイドバーの「執筆」ボタンで NavigateTo("editor") → EditorViewModel

---

## 実装タスク（順番通りに実装すること）

### タスク1: カードデータモデル（Core層）

**ファイル:** `src/AozoraEpub3.Core/Editor/CardModels.cs`

```csharp
namespace AozoraEpub3.Core.Editor;

/// <summary>カードのステータス</summary>
public enum CardStatus { Draft, Writing, Done }

/// <summary>1話分のカード</summary>
public sealed class StoryCard
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public CardStatus Status { get; set; } = CardStatus.Draft;
    public int WordCount => Body.Length;  // 日本語なので文字数≒単語数
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;

    /// <summary>本文の先頭N文字を返す（カード表示用）</summary>
    public string GetPreviewText(int maxChars = 40)
        => Body.Length <= maxChars ? Body.Replace("\n", " ")
            : Body[..maxChars].Replace("\n", " ") + "…";
}

/// <summary>カードの集合（1作品分）</summary>
public sealed class CardCollection
{
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public List<StoryCard> Cards { get; set; } = [];
    public int TotalWordCount => Cards.Sum(c => c.WordCount);
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
```

**ポイント:**
- シンプルに始める。project.json や .aep3proj は E7-C-Full で導入
- 初心者カードはインメモリ管理 + JSON保存で十分

### タスク2: カード永続化サービス（Gui層）

**ファイル:** `src/AozoraEpub3.Gui/Services/CardStorageService.cs`

```csharp
namespace AozoraEpub3.Gui.Services;

/// <summary>
/// カードコレクションの保存/読み込み。
/// 保存先: %APPDATA%\AozoraEpub3\cards\{collection-id}.json
/// </summary>
public static class CardStorageService
{
    private static readonly string BaseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AozoraEpub3", "cards");

    public static CardCollection? Load(string collectionId);
    public static void Save(CardCollection collection, string collectionId);
    public static List<string> ListCollections(); // 保存済みコレクション一覧
    public static string CreateNew(string title, string author); // 新規作成、IDを返す
}
```

**ポイント:**
- System.Text.Json でシリアライズ
- 自動保存: 5秒デバウンスで変更を検出して保存（タスク5で実装）

### タスク3: CardBoardViewModel（メインのViewModel）

**ファイル:** `src/AozoraEpub3.Gui/ViewModels/CardBoardViewModel.cs`

このViewModelが初心者カードモードの全体を制御する。

**必要なプロパティ:**
```csharp
// カードリスト
[ObservableProperty] private ObservableCollection<StoryCard> _cards;

// 選択中のカード
[ObservableProperty] private StoryCard? _selectedCard;

// 選択中カードの本文（エディタにバインド）
[ObservableProperty] private string _activeCardBody = "";

// プレビューHTML（WebView2にバインド）
[ObservableProperty] private string _previewHtml = "";

// 全体統計
public int TotalCards => Cards.Count;
public int TotalWordCount => Cards.Sum(c => c.WordCount);
public string StatsText => $"📚 全{TotalCards}話 / 合計 {TotalWordCount:N0}字";

// モード（なろう/カクヨム/汎用）
[ObservableProperty] private int _selectedModeIndex;

// プレビュー表示フラグ
[ObservableProperty] private bool _isPreviewVisible = true;

// 縦書きフラグ
[ObservableProperty] private bool _isVertical = true;
```

**必要なコマンド:**
```csharp
[RelayCommand] private void AddCard();       // 新しいカードを追加
[RelayCommand] private void DeleteCard();    // 選択カードを削除（確認ダイアログ付き）
[RelayCommand] private void SelectCard(StoryCard card); // カード選択 → エディタに反映
[RelayCommand] private void ChangeStatus(string status); // ステータス変更
[RelayCommand] private async Task ConvertToEpub(); // 全カードを結合してEPUB変換
```

**デバウンスプレビュー:**
- EditorViewModel と同じロジック: activeCardBody の変更を 400ms デバウンスして LivePreviewService で XHTML 生成
- PreviewUpdateRequested イベントで View に通知

**カード結合（EPUB変換用）:**
```csharp
private string CombineCardsToText()
{
    var sb = new StringBuilder();
    sb.AppendLine(_collection.Title);
    sb.AppendLine(_collection.Author);
    sb.AppendLine();
    foreach (var card in Cards.Where(c => c.Status != CardStatus.Draft || true))
    {
        sb.AppendLine("---"); // 改ページ
        if (!string.IsNullOrEmpty(card.Title))
            sb.AppendLine($"# {card.Title}");
        sb.AppendLine();
        sb.AppendLine(card.Body);
        sb.AppendLine();
    }
    return sb.ToString();
}
```

### タスク4: CardBoardView（メインのView）

**ファイル:** `src/AozoraEpub3.Gui/Views/CardBoardView.axaml` + `.axaml.cs`

**レイアウト:**
```
┌──────────────────────────────────────────────────────┐
│ ToolBar: [モード選択] | [ルビ][傍点][太字] | [EPUB変換]  │
├──────────────────────┬───────────────────────────────┤
│ カードリスト（左 250px）│ エディタ＋プレビュー（右）      │
│                      │                               │
│ ┌──────────────┐     │ ┌───────────┬───────────┐    │
│ │ 📖 サンプル    │     │ │ TextBox   │ WebView2  │    │
│ │ 主人公が駅の… │     │ │ (編集)    │ (プレビュー)│    │
│ │ 1,200字 ✏️   │     │ │           │           │    │
│ └──────────────┘     │ └───────────┴───────────┘    │
│                      │                               │
│ ┌──────────────┐     │                               │
│ │ + 自分の話を… │     │                               │
│ └──────────────┘     │                               │
│                      │                               │
├──────────────────────┴───────────────────────────────┤
│ StatusBar: 📚 全2話 / 合計 3,450字                     │
└──────────────────────────────────────────────────────┘
```

**AXAML 構造（概要）:**
```xml
<UserControl x:Class="AozoraEpub3.Gui.Views.CardBoardView"
             x:DataType="vm:CardBoardViewModel">
  <Grid RowDefinitions="Auto,*,Auto">
    <!-- Row 0: ToolBar（EditorViewと同等のもの） -->
    <!-- Row 1: Grid ColumnDefinitions="250,Auto,*" -->
    <!--   Col 0: カードリスト（ListBox + AddCardButton） -->
    <!--   Col 1: GridSplitter -->
    <!--   Col 2: Grid ColumnDefinitions="*,Auto,*" = エディタ + プレビュー -->
    <!-- Row 2: StatusBar -->
  </Grid>
</UserControl>
```

**カードリスト内のItemTemplate:**
```xml
<DataTemplate DataType="{x:Type editor:StoryCard}">
  <Border Padding="8" Margin="2" CornerRadius="4"
          Background="#2D2D30" BorderBrush="#3E3E42" BorderThickness="1">
    <StackPanel Spacing="4">
      <TextBlock Text="{Binding Title}" FontWeight="SemiBold"
                 FontSize="13" Foreground="#DCDCDC"/>
      <TextBlock Text="{Binding GetPreviewText}"
                 FontSize="11" Foreground="#858585"
                 TextTrimming="CharacterEllipsis"/>
      <StackPanel Orientation="Horizontal" Spacing="8">
        <TextBlock FontSize="11" Foreground="#6A9955">
          <Run Text="{Binding WordCount, StringFormat='{}{0:N0}字'}"/>
        </TextBlock>
        <!-- ステータスアイコンはConverterまたはコードビハインドで -->
      </StackPanel>
    </StackPanel>
  </Border>
</DataTemplate>
```

**コードビハインド (CardBoardView.axaml.cs):**
- EditorView.axaml.cs と同様に WebView2Host を手動で PreviewContainer に追加
- PreviewUpdateRequested イベントをハンドルして WebView2Host.NavigateToString() を呼ぶ
- SelectedCard 変更時にエディタテキストを切替

**EditorViewのコードビハインドから流用すべき箇所:**
- WebView2Host の初期化ロジック
- ショートカットキーのハンドリング（Ctrl+R, Ctrl+B等）
- スニペット挿入ロジック

### タスク5: SPAルーティングへの統合

**変更するファイル:**

1. **App.axaml** — DataTemplate 追加:
```xml
<DataTemplate DataType="{x:Type vm:CardBoardViewModel}">
  <views:CardBoardView />
</DataTemplate>
```

2. **MainWindowViewModel.cs** — CardBoardVm プロパティ追加:
```csharp
public CardBoardViewModel CardBoardVm { get; } = new();
```
NavigateTo メソッドに追加:
```csharp
"cards" => CardBoardVm,
```

3. **MainWindow.axaml** — サイドバーに「カード執筆」ボタン追加:
```xml
<!-- 既存の「執筆」ボタンの下に -->
<Button Content="カード執筆"
        Command="{Binding NavigateToCommand}"
        CommandParameter="cards"
        HorizontalAlignment="Stretch"
        HorizontalContentAlignment="Left"
        Padding="16,12"
        Classes="nav-item" />
```

**注: サイドバー構成はVISIONでは「📖読む / ✏️書く / 📘本にする」だが、
現時点では既存ナビゲーションに追加する形で実装。
サイドバー全面改修は別タスクとする。**

### タスク6: 初期サンプルカード

初回起動時（カードが0枚のとき）にサンプルカードを1枚表示する。

```csharp
private static StoryCard CreateSampleCard() => new()
{
    Title = "サンプル",
    Body = """
    　駅のホームで、見覚えのない手紙を拾った。
    　宛名も差出人もない。ただ一行、こう書かれていた。

    「明日の朝、同じ場所で待っています」

    　誰が書いたものだろう。捨てようとしたが、なぜか鞄にしまっていた。
    　翌朝、いつもより一本早い電車に乗った。ホームに降り立つと───
    """,
    Status = CardStatus.Draft
};
```

**設計意図:**
- 書きかけにする（完成品は心理的ハードルが上がる）
- 短いが「続きが気になる」内容にする
- ルビや傍点は使わない（初心者が最初に見る画面）
- 「サンプル」ラベルを小さく表示して、本物のデータではないことを示す

### タスク7: 節目の通知（文字数マイルストーン）

ステータスバーに一時的なメッセージを表示する仕組み。

**CardBoardViewModel に追加:**
```csharp
[ObservableProperty] private string _milestoneMessage = "";

private readonly int[] _milestones = [1000, 3000, 5000, 10000, 20000, 50000];
private readonly HashSet<int> _achievedMilestones = [];

private void CheckMilestones()
{
    var total = TotalWordCount;
    foreach (var m in _milestones)
    {
        if (total >= m && _achievedMilestones.Add(m))
        {
            var msg = m switch
            {
                1000  => "🎉 1,000字！原稿用紙2.5枚分です",
                3000  => "🎉 3,000字！短編の入口です",
                5000  => "🎉 5,000字！もう立派な短編です",
                10000 => "🎉 10,000字突破！短編小説1本分です",
                20000 => "🎉 20,000字！中編の領域です",
                50000 => "🎉 50,000字！長編に突入しました",
                _ => ""
            };
            ShowMilestone(msg);
        }
    }
}

private async void ShowMilestone(string msg)
{
    MilestoneMessage = msg;
    await Task.Delay(3000);
    if (MilestoneMessage == msg) MilestoneMessage = "";
}
```

**StatusBarに表示:**
```xml
<!-- MilestoneMessage が空でないときだけ表示 -->
<TextBlock Text="{Binding MilestoneMessage}"
           IsVisible="{Binding MilestoneMessage, Converter={x:Static StringConverters.IsNotNullOrEmpty}}"
           Foreground="#6A9955" FontWeight="SemiBold"/>
```

### タスク8: EPUB変換連携

**CardBoardViewModel.ConvertToEpub():**

全カードを結合してEPUBに変換し、プレビューで開く。

```csharp
[RelayCommand]
private async Task ConvertToEpub()
{
    var combinedText = CombineCardsToText();
    
    // EditorConversionEngine でハイブリッド記法 → 青空文庫記法に変換
    var engine = new EditorConversionEngine(GetCurrentProfile());
    var formatter = new NovelFormatter(GetCurrentProfile());
    var formattedText = formatter.Format(combinedText);
    var aozoraText = engine.Convert(formattedText);
    
    // 一時ファイルに書き出し → 既存の AozoraEpub3Converter で EPUB 変換
    var tempPath = Path.Combine(Path.GetTempPath(), "aep3_cards_temp.txt");
    File.WriteAllText(tempPath, aozoraText);
    
    // ConversionService を使って変換（MainWindowViewModel 経由）
    EpubConversionRequested?.Invoke(tempPath);
}

/// <summary>EPUB変換要求。MainWindowViewModel がハンドルして OpenPreview する。</summary>
public event Action<string>? EpubConversionRequested;
```

**MainWindowViewModel への接続:**
```csharp
// コンストラクタに追加
CardBoardVm.EpubConversionRequested += OnCardEpubConversion;

private async void OnCardEpubConversion(string tempTextPath)
{
    // 既存の ConversionService を使ってEPUBに変換
    var outputDir = Path.GetTempPath();
    // ... 変換処理（LocalConvertViewModel の変換ロジックを参考にする）
    // 変換完了 → OpenPreview(epubPath);
}
```

**注: EPUB変換の詳細実装は既存の ConversionService / LocalConvertViewModel を
参考にすること。ここでは接続ポイントだけ示す。**

---

## テストの方針

| テスト | 内容 |
|--------|------|
| CardModels のユニットテスト | StoryCard.WordCount, GetPreviewText, CardCollection.TotalWordCount |
| CardStorageService のテスト | 保存→読み込みの往復テスト |
| CombineCardsToText のテスト | 複数カード結合 → 正しい青空文庫記法テキスト |

テストファイル: `tests/AozoraEpub3.Tests/CardModelsTests.cs`

---

## ビルド確認

各タスク完了後にビルド確認すること:
```
cd /d D:\git\aozoraepub3-dotnet
"C:\Program Files\dotnet\dotnet.exe" build src\AozoraEpub3.Gui\AozoraEpub3.Gui.csproj
```

既知の警告（無視してよい）:
- MSB3277: WindowsBase のバージョン競合
- NU1510: パッケージ警告

---

## 実装しないこと（スコープ外）

- ドラッグ＆ドロップによるカード並び替え → 後で追加
- カード複製 → 後で追加
- 章（チャプター）構造 → E7-C-Full で実装
- プロジェクトファイル (.aep3proj) → E7-C-Full で実装
- テーマ対応 → E7-A で実装
- チートシート → E7-B で実装

---

## 実装順のまとめ

1. `Core/Editor/CardModels.cs` — データモデル
2. `Gui/Services/CardStorageService.cs` — 永続化
3. `Gui/ViewModels/CardBoardViewModel.cs` — ViewModel
4. `Gui/Views/CardBoardView.axaml` + `.axaml.cs` — View
5. `App.axaml` + `MainWindowViewModel.cs` + `MainWindow.axaml` — ルーティング統合
6. サンプルカード + 節目通知
7. EPUB変換連携
8. テスト
