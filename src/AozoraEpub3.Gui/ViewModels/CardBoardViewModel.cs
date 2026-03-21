using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AozoraEpub3.Core.Editor;
using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>
/// 初心者カードモードの ViewModel。
/// カードリスト管理、エディタ、プレビュー連携、EPUB変換を担当する。
/// </summary>
public sealed partial class CardBoardViewModel : ViewModelBase
{
    private LivePreviewService _previewService;
    private CancellationTokenSource? _debounceCts;
    private CancellationTokenSource? _saveDebounceCts;
    private const int DebounceMs = 400;
    private const int SaveDebounceMs = 5000;

    private CardCollection _collection = new();
    private string _collectionId = "";

    // ───── カードリスト ────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalCards))]
    [NotifyPropertyChangedFor(nameof(TotalWordCount))]
    [NotifyPropertyChangedFor(nameof(StatsText))]
    private ObservableCollection<StoryCard> _cards = [];

    [ObservableProperty]
    private StoryCard? _selectedCard;

    [ObservableProperty]
    private string _activeCardBody = "";

    [ObservableProperty]
    private string _activeCardTitle = "";

    [ObservableProperty]
    private string _previewHtml = "";

    [ObservableProperty]
    private int _selectedModeIndex;

    [ObservableProperty]
    private bool _isPreviewVisible = true;

    [ObservableProperty]
    private bool _isVertical = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditorMode))]
    private bool _isGalleryMode = true;

    public bool IsEditorMode => !IsGalleryMode;

    // ───── 統計 ──────────────────────────────────────────────────────────

    public int TotalCards => Cards.Count;
    public int TotalWordCount => Cards.Sum(c => c.WordCount);
    public string StatsText => $"全{TotalCards}話 / 合計 {TotalWordCount:N0}字";

    // ───── マイルストーン ────────────────────────────────────────────────

    [ObservableProperty]
    private string _milestoneMessage = "";

    private readonly int[] _milestones = [1000, 3000, 5000, 10000, 20000, 50000];
    private readonly HashSet<int> _achievedMilestones = [];

    // ───── テーマ ──────────────────────────────────────────────────────────

    [ObservableProperty]
    private EditorTheme _currentTheme = EditorThemes.DarkDefault;

    /// <summary>チートシート ViewModel</summary>
    public CheatSheetViewModel CheatSheet { get; } = new();

    // ───── イベント ──────────────────────────────────────────────────────

    /// <summary>プレビュー更新要求イベント。引数は XHTML 文字列。</summary>
    public event Action<string>? PreviewUpdateRequested;

    /// <summary>スニペット挿入要求イベント。View 側で TextBox に反映する。</summary>
    public event Action<SnippetInsertRequest>? SnippetInsertRequested;

    /// <summary>EPUB変換要求。MainWindowViewModel がハンドルして OpenPreview する。</summary>
    public event Action<string>? EpubConversionRequested;

    /// <summary>テーマ変更通知イベント。View がエディタ色を更新する。</summary>
    public event Action<EditorTheme>? ThemeChanged;

    /// <summary>マイグレーション実行要求。MainWindowViewModel がプロジェクト画面に遷移する。</summary>
    public event Action<CardCollection>? MigrateToProjectRequested;

    public string[] ModeNames { get; } = ["標準", "なろう", "カクヨム"];

    /// <summary>ドラッグ＆ドロップ後などに統計を再通知する。</summary>
    public void NotifyStatsChanged()
    {
        OnPropertyChanged(nameof(TotalCards));
        OnPropertyChanged(nameof(TotalWordCount));
        OnPropertyChanged(nameof(StatsText));
    }

    // ───── マイグレーション提案 ────────────────────────────────────────────

    [ObservableProperty]
    private bool _isMigrationProposalVisible;

    [ObservableProperty]
    private string _migrationProposalText = "";

    /// <summary>提案を完全に抑制するフラグ（設定で永続化）</summary>
    public bool SuppressMigrationProposals { get; set; }

    // ───── コンストラクタ ────────────────────────────────────────────────

    public CardBoardViewModel()
    {
        _previewService = new LivePreviewService(ConversionProfile.Default)
        {
            Theme = _currentTheme
        };
        LoadOrCreateCollection();
    }

    partial void OnCurrentThemeChanged(EditorTheme value)
    {
        _previewService.Theme = value;
        ThemeChanged?.Invoke(value);
        if (!string.IsNullOrEmpty(ActiveCardBody))
            SchedulePreviewUpdate(ActiveCardBody);
    }

    private void LoadOrCreateCollection()
    {
        var collections = CardStorageService.ListCollections();
        if (collections.Count > 0)
        {
            _collectionId = collections[0];
            _collection = CardStorageService.Load(_collectionId) ?? new CardCollection();
        }
        else
        {
            _collectionId = CardStorageService.CreateNew("", "");
            _collection = CardStorageService.Load(_collectionId) ?? new CardCollection();
        }

        Cards = new ObservableCollection<StoryCard>(_collection.Cards);

        // 初回起動時（カードが0枚のとき）にサンプルカードを表示
        if (Cards.Count == 0)
        {
            var sample = CreateSampleCard();
            Cards.Add(sample);
            _collection.Cards.Add(sample);
            ScheduleSave();
        }

        if (Cards.Count > 0)
            SelectCard(Cards[0]);
    }

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

    // ───── テキスト変更時のデバウンス処理 ─────────────────────────────────

    partial void OnActiveCardTitleChanged(string value)
    {
        if (SelectedCard == null) return;
        if (SelectedCard.Title == value) return;
        SelectedCard.Title = value;
        ScheduleSave();
    }

    partial void OnActiveCardBodyChanged(string value)
    {
        if (SelectedCard == null) return;

        SelectedCard.Body = value;
        SelectedCard.ModifiedAt = DateTime.Now;

        OnPropertyChanged(nameof(TotalWordCount));
        OnPropertyChanged(nameof(StatsText));

        CheckMilestones();
        CheatSheet.NotifyInput(TotalWordCount);
        SchedulePreviewUpdate(value);
        ScheduleSave();
    }

    private void SchedulePreviewUpdate(string text)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var ct = _debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceMs, ct);
                if (ct.IsCancellationRequested) return;

                var xhtml = _previewService.ConvertToXhtml(text, IsVertical);
                PreviewHtml = xhtml;
                PreviewUpdateRequested?.Invoke(xhtml);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Preview error: {ex.Message}");
            }
        }, ct);
    }

    private void ScheduleSave()
    {
        _saveDebounceCts?.Cancel();
        _saveDebounceCts = new CancellationTokenSource();
        var ct = _saveDebounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SaveDebounceMs, ct);
                if (ct.IsCancellationRequested) return;

                _collection.Cards = [.. Cards];
                CardStorageService.Save(_collection, _collectionId);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save error: {ex.Message}");
            }
        }, ct);
    }

    partial void OnSelectedModeIndexChanged(int value)
    {
        var profile = value switch
        {
            1 => ConversionProfile.Narou,
            2 => ConversionProfile.Kakuyomu,
            _ => ConversionProfile.Default
        };
        _previewService = new LivePreviewService(profile);
        if (!string.IsNullOrEmpty(ActiveCardBody))
            SchedulePreviewUpdate(ActiveCardBody);
    }

    partial void OnIsVerticalChanged(bool value)
    {
        if (!string.IsNullOrEmpty(ActiveCardBody))
            SchedulePreviewUpdate(ActiveCardBody);
    }

    // ───── コマンド ──────────────────────────────────────────────────────

    [RelayCommand]
    private void AddCard()
    {
        var card = new StoryCard
        {
            Title = $"第{Cards.Count + 1}話"
        };
        Cards.Add(card);
        _collection.Cards.Add(card);
        OnPropertyChanged(nameof(TotalCards));
        OnPropertyChanged(nameof(StatsText));
        SelectCard(card);
        IsGalleryMode = false;
        ScheduleSave();
        CheckMigrationTrigger();
    }

    [RelayCommand]
    private void DeleteCard()
    {
        if (SelectedCard == null) return;

        var index = Cards.IndexOf(SelectedCard);
        Cards.Remove(SelectedCard);
        _collection.Cards.Remove(SelectedCard);

        OnPropertyChanged(nameof(TotalCards));
        OnPropertyChanged(nameof(TotalWordCount));
        OnPropertyChanged(nameof(StatsText));

        if (Cards.Count > 0)
        {
            var newIndex = Math.Min(index, Cards.Count - 1);
            SelectCard(Cards[newIndex]);
        }
        else
        {
            SelectedCard = null;
            ActiveCardBody = "";
        }
        ScheduleSave();
    }

    [RelayCommand]
    private void SelectCard(StoryCard card)
    {
        SelectedCard = card;
        ActiveCardBody = card.Body;
        ActiveCardTitle = card.Title;
    }

    [RelayCommand]
    private void OpenCard(StoryCard card)
    {
        SelectCard(card);
        IsGalleryMode = false;
    }

    [RelayCommand]
    private void BackToGallery()
    {
        IsGalleryMode = true;
    }

    [RelayCommand]
    private void ChangeStatus(string status)
    {
        if (SelectedCard == null) return;
        if (Enum.TryParse<CardStatus>(status, out var s))
        {
            SelectedCard.Status = s;
            ScheduleSave();
        }
    }

    [RelayCommand]
    private void MoveCardUp()
    {
        if (SelectedCard == null) return;
        var index = Cards.IndexOf(SelectedCard);
        if (index <= 0) return;
        Cards.Move(index, index - 1);
        _collection.Cards = [.. Cards];
        ScheduleSave();
    }

    [RelayCommand]
    private void MoveCardDown()
    {
        if (SelectedCard == null) return;
        var index = Cards.IndexOf(SelectedCard);
        if (index < 0 || index >= Cards.Count - 1) return;
        Cards.Move(index, index + 1);
        _collection.Cards = [.. Cards];
        ScheduleSave();
    }

    [RelayCommand]
    private void DuplicateCard()
    {
        if (SelectedCard == null) return;
        var copy = new StoryCard
        {
            Title = SelectedCard.Title + "（コピー）",
            Body = SelectedCard.Body,
            Status = CardStatus.Draft
        };
        var index = Cards.IndexOf(SelectedCard);
        Cards.Insert(index + 1, copy);
        _collection.Cards = [.. Cards];
        OnPropertyChanged(nameof(TotalCards));
        OnPropertyChanged(nameof(TotalWordCount));
        OnPropertyChanged(nameof(StatsText));
        SelectCard(copy);
        ScheduleSave();
    }

    [RelayCommand]
    private void InsertSnippet(string snippetId)
    {
        var service = new EditorSuggestService();
        var result = service.GetSnippet(snippetId, null);
        SnippetInsertRequested?.Invoke(new SnippetInsertRequest(
            result.TextToInsert, result.CursorOffset, result.IsLineLevel));
    }

    [RelayCommand]
    private void TogglePreview()
    {
        IsPreviewVisible = !IsPreviewVisible;
    }

    [RelayCommand]
    private void FormatText()
    {
        var formatter = new NovelFormatter(GetCurrentProfile());
        ActiveCardBody = formatter.Format(ActiveCardBody);
    }

    [RelayCommand]
    private async Task ConvertToEpub()
    {
        CheckMigrationTriggerOnConvert();

        var combinedText = CombineCardsToText();

        var engine = new EditorConversionEngine(GetCurrentProfile());
        var formatter = new NovelFormatter(GetCurrentProfile());
        var formattedText = formatter.Format(combinedText);
        var aozoraText = engine.Convert(formattedText);

        var tempPath = Path.Combine(Path.GetTempPath(), "aep3_cards_temp.txt");
        await File.WriteAllTextAsync(tempPath, aozoraText);

        EpubConversionRequested?.Invoke(tempPath);
    }

    // ───── マイグレーション提案コマンド ────────────────────────────────────

    [RelayCommand]
    private void DismissMigrationProposal()
    {
        IsMigrationProposalVisible = false;
    }

    [RelayCommand]
    private void SuppressMigrationProposals_Command()
    {
        SuppressMigrationProposals = true;
        IsMigrationProposalVisible = false;
    }

    [RelayCommand]
    private void AcceptMigration()
    {
        IsMigrationProposalVisible = false;
        _collection.Cards = [.. Cards];
        MigrateToProjectRequested?.Invoke(_collection);
    }

    /// <summary>マイグレーション提案を表示する（条件チェック付き）</summary>
    private void ShowMigrationProposal(string message)
    {
        if (SuppressMigrationProposals) return;
        if (IsMigrationProposalVisible) return; // 既に表示中
        MigrationProposalText = message;
        IsMigrationProposalVisible = true;
    }

    /// <summary>カード数に応じたマイグレーショントリガーチェック</summary>
    private void CheckMigrationTrigger()
    {
        if (SuppressMigrationProposals) return;
        if (Cards.Count > 5)
            ShowMigrationProposal("5話を超えました！章分けしてみませんか？");
    }

    /// <summary>EPUB変換時のマイグレーショントリガーチェック</summary>
    private void CheckMigrationTriggerOnConvert()
    {
        if (SuppressMigrationProposals) return;
        if (Cards.Count >= 3)
            ShowMigrationProposal("変換前に構成を確認しますか？「本にする」モードで章立てや構成を整理できます。");
    }

    // ───── マイルストーン ────────────────────────────────────────────────

    private void CheckMilestones()
    {
        var total = TotalWordCount;
        foreach (var m in _milestones)
        {
            if (total >= m && _achievedMilestones.Add(m))
            {
                var msg = m switch
                {
                    1000  => "1,000字！原稿用紙2.5枚分です",
                    3000  => "3,000字！短編の入口です",
                    5000  => "5,000字！もう立派な短編です",
                    10000 => "10,000字突破！短編小説1本分です",
                    20000 => "20,000字！中編の領域です",
                    50000 => "50,000字！長編に突入しました",
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

    // ───── ヘルパー ──────────────────────────────────────────────────────

    private string CombineCardsToText()
    {
        var sb = new StringBuilder();
        sb.AppendLine(_collection.Title);
        sb.AppendLine(_collection.Author);
        sb.AppendLine();
        foreach (var card in Cards)
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

    private ConversionProfile GetCurrentProfile() => SelectedModeIndex switch
    {
        1 => ConversionProfile.Narou,
        2 => ConversionProfile.Kakuyomu,
        _ => ConversionProfile.Default
    };
}
