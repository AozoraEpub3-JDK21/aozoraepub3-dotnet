using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AozoraEpub3.Core.Editor;
using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>
/// 編集カードモード（層3 — 本にする）の ViewModel。
/// 章 > 話の階層構造、プロジェクトファイル管理、目標文字数などを扱う。
/// </summary>
public sealed partial class CardEditorViewModel : ViewModelBase
{
    private readonly ProjectService _projectService = new();
    private readonly ProjectCombineService _combineService = new();
    private LivePreviewService _previewService;
    private CancellationTokenSource? _debounceCts;
    private CancellationTokenSource? _saveDebounceCts;
    private const int DebounceMs = 400;
    private const int SaveDebounceMs = 3000;

    private ProjectData? _project;
    private string _projectDir = "";

    // ───── ツリー構造 ────────────────────────────────────────────────────

    /// <summary>ツリー表示用のフラット化されたアイテムリスト</summary>
    [ObservableProperty]
    private ObservableCollection<TreeCardItem> _treeItems = [];

    [ObservableProperty]
    private TreeCardItem? _selectedTreeItem;

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
    private bool _isProjectLoaded;

    // ───── テーマ・チートシート ──────────────────────────────────────────

    [ObservableProperty]
    private EditorTheme _currentTheme = EditorThemes.DarkDefault;

    public CheatSheetViewModel CheatSheet { get; } = new();

    // ───── 統計 ──────────────────────────────────────────────────────────

    public int TotalWordCount => _project?.TotalWordCount ?? 0;
    public int TotalEpisodes => _project?.TotalEpisodes ?? 0;
    public int TotalChapters => _project?.TotalChapters ?? 0;
    public int TargetWordCount => _project?.TargetWordCount ?? 0;
    public double ProgressPercent => _project?.ProgressPercent ?? 0;

    public string StatsText
    {
        get
        {
            var stats = $"全体: {TotalWordCount:N0}字 / {TotalEpisodes}話 / {TotalChapters}章";
            if (TargetWordCount > 0)
                stats += $" | 目標: {TargetWordCount:N0}字 ({ProgressPercent:F1}%)";
            return stats;
        }
    }

    // ───── イベント ──────────────────────────────────────────────────────

    public event Action<string>? PreviewUpdateRequested;
    public event Action<SnippetInsertRequest>? SnippetInsertRequested;
    public event Action<EditorTheme>? ThemeChanged;
    public event Action<string>? EpubConversionRequested;
    public event Func<Task<string?>>? OpenProjectRequested;
    public event Func<string?, Task<string?>>? SaveProjectAsRequested;

    public string[] ModeNames { get; } = ["標準", "なろう", "カクヨム"];

    // ───── コンストラクタ ────────────────────────────────────────────────

    public CardEditorViewModel()
    {
        _previewService = new LivePreviewService(ConversionProfile.Default)
        {
            Theme = _currentTheme
        };
    }

    partial void OnCurrentThemeChanged(EditorTheme value)
    {
        _previewService.Theme = value;
        ThemeChanged?.Invoke(value);
        if (!string.IsNullOrEmpty(ActiveCardBody))
            SchedulePreviewUpdate(ActiveCardBody);
    }

    // ───── プロジェクト操作 ──────────────────────────────────────────────

    [RelayCommand]
    private async Task NewProject()
    {
        if (SaveProjectAsRequested == null) return;
        var path = await SaveProjectAsRequested.Invoke(null);
        if (path == null) return;

        var parentDir = Path.GetDirectoryName(path) ?? Path.GetTempPath();
        var name = Path.GetFileNameWithoutExtension(path);
        _projectDir = _projectService.CreateNew(parentDir, name, "");
        _project = _projectService.Load(_projectDir);
        IsProjectLoaded = true;
        RebuildTree();
        UpdateStats();
    }

    [RelayCommand]
    private async Task OpenProject()
    {
        if (OpenProjectRequested == null) return;
        var path = await OpenProjectRequested.Invoke();
        if (path == null) return;

        // project.json が選ばれた場合はその親ディレクトリを使う
        if (Path.GetFileName(path) == "project.json")
            path = Path.GetDirectoryName(path) ?? path;

        _projectDir = path;
        _project = _projectService.Load(_projectDir);
        if (_project == null) return;

        IsProjectLoaded = true;
        RebuildTree();
        UpdateStats();

        if (TreeItems.Count > 0)
            SelectTreeItem(TreeItems[0]);
    }

    public void LoadProject(string projectDir)
    {
        _projectDir = projectDir;
        _project = _projectService.Load(projectDir);
        if (_project == null) return;

        IsProjectLoaded = true;
        RebuildTree();
        UpdateStats();

        if (TreeItems.Count > 0)
            SelectTreeItem(TreeItems[0]);
    }

    [RelayCommand]
    private void SaveProject()
    {
        if (_project == null || string.IsNullOrEmpty(_projectDir)) return;
        SyncCurrentCardToProject();
        _projectService.Save(_projectDir, _project);
    }

    // ───── ツリー構築 ────────────────────────────────────────────────────

    private void RebuildTree()
    {
        var items = new ObservableCollection<TreeCardItem>();
        if (_project == null) { TreeItems = items; return; }

        foreach (var item in _project.Structure)
        {
            items.Add(new TreeCardItem(item, 0));
            if (item.Type == CardType.Chapter)
            {
                foreach (var child in item.Children)
                    items.Add(new TreeCardItem(child, 1));
            }
        }
        TreeItems = items;
    }

    // ───── カード選択・編集 ──────────────────────────────────────────────

    [RelayCommand]
    private void SelectTreeItem(TreeCardItem item)
    {
        // 前のカードの内容を保存
        SyncCurrentCardToProject();

        SelectedTreeItem = item;

        if (item.StructureItem.Type == CardType.Chapter)
        {
            ActiveCardTitle = item.StructureItem.Title;
            ActiveCardBody = "";
            return;
        }

        var card = _projectService.LoadCard(_projectDir, item.StructureItem);
        ActiveCardTitle = item.StructureItem.Title;
        ActiveCardBody = card.Body;
    }

    private void SyncCurrentCardToProject()
    {
        if (SelectedTreeItem == null || _project == null) return;

        var si = SelectedTreeItem.StructureItem;
        si.Title = ActiveCardTitle;

        if (si.Type != CardType.Chapter && !string.IsNullOrEmpty(si.File))
        {
            si.WordCount = ActiveCardBody.Length;
            var card = new CardItem
            {
                FileName = si.File,
                Body = ActiveCardBody
            };
            _projectService.SaveCard(_projectDir, card);
        }
    }

    partial void OnActiveCardBodyChanged(string value)
    {
        if (SelectedTreeItem == null) return;

        SelectedTreeItem.StructureItem.WordCount = value.Length;
        CheatSheet.NotifyInput(TotalWordCount);
        UpdateStats();
        SchedulePreviewUpdate(value);
        ScheduleSave();
    }

    partial void OnActiveCardTitleChanged(string value)
    {
        if (SelectedTreeItem == null) return;
        SelectedTreeItem.StructureItem.Title = value;
        SelectedTreeItem.UpdateDisplay();
        ScheduleSave();
    }

    // ───── デバウンスプレビュー ──────────────────────────────────────────

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
                SyncCurrentCardToProject();
                if (_project != null)
                    _projectService.Save(_projectDir, _project);
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
        _previewService = new LivePreviewService(profile) { Theme = CurrentTheme };
        if (!string.IsNullOrEmpty(ActiveCardBody))
            SchedulePreviewUpdate(ActiveCardBody);
    }

    partial void OnIsVerticalChanged(bool value)
    {
        if (!string.IsNullOrEmpty(ActiveCardBody))
            SchedulePreviewUpdate(ActiveCardBody);
    }

    // ───── カード追加・削除コマンド ──────────────────────────────────────

    [RelayCommand]
    private void AddEpisode()
    {
        if (_project == null) return;

        // 選択中のアイテムが章なら、その章に追加。なければ最後の章に追加。
        int chapterIndex = -1;
        if (SelectedTreeItem != null)
        {
            var si = SelectedTreeItem.StructureItem;
            if (si.Type == CardType.Chapter)
                chapterIndex = _project.Structure.IndexOf(si);
            else
            {
                // 親の章を探す
                for (int i = 0; i < _project.Structure.Count; i++)
                {
                    if (_project.Structure[i].Type == CardType.Chapter &&
                        _project.Structure[i].Children.Contains(si))
                    {
                        chapterIndex = i;
                        break;
                    }
                }
            }
        }

        if (chapterIndex < 0)
        {
            // 章がなければ作成
            _projectService.AddChapter(_project, "");
            chapterIndex = _project.Structure.Count - 1;
        }

        _projectService.AddEpisode(_project, _projectDir, chapterIndex, "");
        _projectService.Save(_projectDir, _project);
        RebuildTree();
        UpdateStats();

        // 追加されたアイテムを選択
        if (TreeItems.Count > 0)
            SelectTreeItem(TreeItems[^1]);
    }

    [RelayCommand]
    private void AddChapter()
    {
        if (_project == null) return;
        _projectService.AddChapter(_project, "");
        _projectService.Save(_projectDir, _project);
        RebuildTree();
        UpdateStats();
    }

    [RelayCommand]
    private void AddMemo()
    {
        if (_project == null) return;
        _projectService.AddMemo(_project, _projectDir, "");
        _projectService.Save(_projectDir, _project);
        RebuildTree();
        UpdateStats();
    }

    [RelayCommand]
    private void DeleteItem()
    {
        if (_project == null || SelectedTreeItem == null) return;

        var si = SelectedTreeItem.StructureItem;

        // Structure 直下から削除
        if (_project.Structure.Remove(si))
        {
            // 章ごと削除
        }
        else
        {
            // 章の子から削除
            foreach (var chapter in _project.Structure.Where(s => s.Type == CardType.Chapter))
            {
                if (chapter.Children.Remove(si))
                    break;
            }
        }

        // ファイルも削除
        if (!string.IsNullOrEmpty(si.File))
        {
            var filePath = Path.Combine(_projectDir, "cards", si.File);
            if (File.Exists(filePath))
                File.Delete(filePath);
        }

        _projectService.Save(_projectDir, _project);
        RebuildTree();
        UpdateStats();

        SelectedTreeItem = null;
        ActiveCardBody = "";
        ActiveCardTitle = "";
    }

    [RelayCommand]
    private void MoveItemUp()
    {
        if (_project == null || SelectedTreeItem == null) return;
        var si = SelectedTreeItem.StructureItem;
        var (list, index) = FindItemLocation(si);
        if (list == null || index <= 0) return;

        list.RemoveAt(index);
        list.Insert(index - 1, si);
        _projectService.Save(_projectDir, _project);
        RebuildTree();
        SelectedTreeItem = TreeItems.FirstOrDefault(t => t.StructureItem == si);
    }

    [RelayCommand]
    private void MoveItemDown()
    {
        if (_project == null || SelectedTreeItem == null) return;
        var si = SelectedTreeItem.StructureItem;
        var (list, index) = FindItemLocation(si);
        if (list == null || index < 0 || index >= list.Count - 1) return;

        list.RemoveAt(index);
        list.Insert(index + 1, si);
        _projectService.Save(_projectDir, _project);
        RebuildTree();
        SelectedTreeItem = TreeItems.FirstOrDefault(t => t.StructureItem == si);
    }

    [RelayCommand]
    private void DuplicateItem()
    {
        if (_project == null || SelectedTreeItem == null) return;
        var si = SelectedTreeItem.StructureItem;
        if (si.Type == CardType.Chapter) return; // 章の複製は非対応

        var (list, index) = FindItemLocation(si);
        if (list == null || index < 0) return;

        // カードファイルを複製
        var newFile = $"card_{Guid.NewGuid():N}.md";
        if (!string.IsNullOrEmpty(si.File))
        {
            var srcPath = Path.Combine(_projectDir, "cards", si.File);
            var dstPath = Path.Combine(_projectDir, "cards", newFile);
            if (File.Exists(srcPath))
                File.Copy(srcPath, dstPath);
        }

        var copy = new StructureItem
        {
            Type = si.Type,
            Title = si.Title + "（コピー）",
            File = newFile,
            WordCount = si.WordCount,
            Status = ProjectCardStatus.Draft
        };
        list.Insert(index + 1, copy);
        _projectService.Save(_projectDir, _project);
        RebuildTree();
        UpdateStats();
        SelectedTreeItem = TreeItems.FirstOrDefault(t => t.StructureItem == copy);
    }

    /// <summary>StructureItem の所属リストとインデックスを返す。</summary>
    private (List<StructureItem>? list, int index) FindItemLocation(StructureItem target)
    {
        if (_project == null) return (null, -1);

        var idx = _project.Structure.IndexOf(target);
        if (idx >= 0) return (_project.Structure, idx);

        foreach (var chapter in _project.Structure.Where(s => s.Type == CardType.Chapter))
        {
            idx = chapter.Children.IndexOf(target);
            if (idx >= 0) return (chapter.Children, idx);
        }
        return (null, -1);
    }

    /// <summary>ドラッグ＆ドロップ後のツリー再構築用。</summary>
    public void MoveItem(int sourceTreeIndex, int targetTreeIndex)
    {
        if (_project == null) return;
        if (sourceTreeIndex < 0 || sourceTreeIndex >= TreeItems.Count) return;
        if (targetTreeIndex < 0 || targetTreeIndex >= TreeItems.Count) return;
        if (sourceTreeIndex == targetTreeIndex) return;

        var sourceItem = TreeItems[sourceTreeIndex].StructureItem;
        var (srcList, srcIdx) = FindItemLocation(sourceItem);
        if (srcList == null || srcIdx < 0) return;

        // 同じリスト内の移動のみサポート（章間移動は複雑なので保留）
        var targetItem = TreeItems[targetTreeIndex].StructureItem;
        var (tgtList, tgtIdx) = FindItemLocation(targetItem);
        if (tgtList == null || tgtIdx < 0) return;
        if (srcList != tgtList) return; // 異なるリスト間は不可

        srcList.RemoveAt(srcIdx);
        if (tgtIdx > srcIdx) tgtIdx--;
        srcList.Insert(tgtIdx, sourceItem);

        _projectService.Save(_projectDir, _project);
        RebuildTree();
        UpdateStats();
        SelectedTreeItem = TreeItems.FirstOrDefault(t => t.StructureItem == sourceItem);
    }

    [RelayCommand]
    private void ChangeStatus(string status)
    {
        if (SelectedTreeItem == null) return;
        if (Enum.TryParse<ProjectCardStatus>(status, out var s))
        {
            SelectedTreeItem.StructureItem.Status = s;
            SelectedTreeItem.UpdateDisplay();
            ScheduleSave();
        }
    }

    // ───── ツールバーコマンド ────────────────────────────────────────────

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
        if (_project == null) return;

        SyncCurrentCardToProject();

        var combinedText = _combineService.Combine(_projectDir, _project);
        var engine = new EditorConversionEngine(GetCurrentProfile());
        var formatter = new NovelFormatter(GetCurrentProfile());
        var formattedText = formatter.Format(combinedText);
        var aozoraText = engine.Convert(formattedText);

        var tempPath = Path.Combine(Path.GetTempPath(), "aep3_project_temp.txt");
        await File.WriteAllTextAsync(tempPath, aozoraText);

        EpubConversionRequested?.Invoke(tempPath);
    }

    // ───── 目標文字数 ────────────────────────────────────────────────────

    [RelayCommand]
    private void SetTargetWordCount(string target)
    {
        if (_project == null) return;
        if (int.TryParse(target, out var count))
        {
            _project.TargetWordCount = count;
            UpdateStats();
            ScheduleSave();
        }
    }

    // ───── ヘルパー ──────────────────────────────────────────────────────

    private void UpdateStats()
    {
        OnPropertyChanged(nameof(TotalWordCount));
        OnPropertyChanged(nameof(TotalEpisodes));
        OnPropertyChanged(nameof(TotalChapters));
        OnPropertyChanged(nameof(TargetWordCount));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(StatsText));
    }

    private ConversionProfile GetCurrentProfile() => SelectedModeIndex switch
    {
        1 => ConversionProfile.Narou,
        2 => ConversionProfile.Kakuyomu,
        _ => ConversionProfile.Default
    };
}

/// <summary>
/// ツリービュー表示用のラッパー。
/// StructureItem に表示レベル（インデント）情報を付加する。
/// </summary>
public sealed partial class TreeCardItem : ObservableObject
{
    public StructureItem StructureItem { get; }
    public int Level { get; }

    [ObservableProperty]
    private string _displayTitle = "";

    [ObservableProperty]
    private string _displayInfo = "";

    public TreeCardItem(StructureItem item, int level)
    {
        StructureItem = item;
        Level = level;
        UpdateDisplay();
    }

    public void UpdateDisplay()
    {
        var prefix = StructureItem.Type switch
        {
            CardType.Cover => "[表紙] ",
            CardType.Synopsis => "[あらすじ] ",
            CardType.Chapter => "■ ",
            CardType.Afterword => "[あとがき] ",
            CardType.Memo => "[メモ] ",
            _ => ""
        };
        DisplayTitle = prefix + StructureItem.Title;

        if (StructureItem.Type == CardType.Chapter)
        {
            var childCount = StructureItem.Children.Count;
            var childWords = StructureItem.Children.Sum(c => c.WordCount);
            DisplayInfo = $"{childCount}話 / {childWords:N0}字";
        }
        else if (StructureItem.Type != CardType.Cover)
        {
            var statusIcon = StructureItem.Status switch
            {
                ProjectCardStatus.Draft => "下書き",
                ProjectCardStatus.Writing => "執筆中",
                ProjectCardStatus.Done => "完成",
                ProjectCardStatus.Revision => "改稿中",
                _ => ""
            };
            DisplayInfo = $"{StructureItem.WordCount:N0}字 {statusIcon}";
        }
        else
        {
            DisplayInfo = "";
        }
    }
}
