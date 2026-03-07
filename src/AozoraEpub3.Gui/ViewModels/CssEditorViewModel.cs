using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>
/// CSS テンプレートエディタの ViewModel。
/// 初心者モード（フォーム）と上級者モード（テキストエディタ）を切り替え可能。
/// </summary>
public sealed partial class CssEditorViewModel : ViewModelBase
{
    private string? _cssFilePath;
    private string _originalCssText = "";

    // ───── モード切り替え ─────────────────────────────────────────────

    /// <summary>上級者モード（テキストエディタ直接編集）が有効か</summary>
    [ObservableProperty]
    private bool _isAdvancedMode;

    /// <summary>CSS エディタが表示されているか</summary>
    [ObservableProperty]
    private bool _isVisible;

    /// <summary>変更があるか（Save ボタン有効化用）</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetCommand))]
    private bool _isDirty;

    // ───── 初心者モード: フォームフィールド ──────────────────────────

    [ObservableProperty] private string _pageMarginTop = "0";
    [ObservableProperty] private string _pageMarginRight = "0";
    [ObservableProperty] private string _pageMarginBottom = "0";
    [ObservableProperty] private string _pageMarginLeft = "0";

    [ObservableProperty] private string _bodyMarginTop = "0";
    [ObservableProperty] private string _bodyMarginRight = "0";
    [ObservableProperty] private string _bodyMarginBottom = "0";
    [ObservableProperty] private string _bodyMarginLeft = "0";

    [ObservableProperty] private int _fontSize = 100;
    [ObservableProperty] private float _lineHeight = 1.8f;
    [ObservableProperty] private bool _isVertical = true;
    [ObservableProperty] private bool _boldUseGothic;
    [ObservableProperty] private bool _gothicUseBold;

    // ───── 上級者モード: テキストエディタ ────────────────────────────

    [ObservableProperty] private string _cssText = "";

    // ───── ステータス ────────────────────────────────────────────────

    [ObservableProperty] private string _statusMessage = "";

    // ───── CSS 変更通知イベント ──────────────────────────────────────

    /// <summary>CSS が変更されたときに発火。引数は新しい CSS テキスト。</summary>
    public event Action<string>? CssChanged;

    // ───── フォームフィールド変更検知 ────────────────────────────────

    partial void OnPageMarginTopChanged(string value) => OnFormFieldChanged();
    partial void OnPageMarginRightChanged(string value) => OnFormFieldChanged();
    partial void OnPageMarginBottomChanged(string value) => OnFormFieldChanged();
    partial void OnPageMarginLeftChanged(string value) => OnFormFieldChanged();
    partial void OnBodyMarginTopChanged(string value) => OnFormFieldChanged();
    partial void OnBodyMarginRightChanged(string value) => OnFormFieldChanged();
    partial void OnBodyMarginBottomChanged(string value) => OnFormFieldChanged();
    partial void OnBodyMarginLeftChanged(string value) => OnFormFieldChanged();
    partial void OnFontSizeChanged(int value) => OnFormFieldChanged();
    partial void OnLineHeightChanged(float value) => OnFormFieldChanged();
    partial void OnIsVerticalChanged(bool value) => OnFormFieldChanged();
    partial void OnBoldUseGothicChanged(bool value) => OnFormFieldChanged();
    partial void OnGothicUseBoldChanged(bool value) => OnFormFieldChanged();

    partial void OnCssTextChanged(string value)
    {
        if (IsAdvancedMode)
        {
            IsDirty = value != _originalCssText;
            NotifyCssChanged(value);
        }
    }

    partial void OnIsAdvancedModeChanged(bool value)
    {
        if (value)
        {
            // フォーム → テキスト: 現在のフォーム値から CSS を生成
            CssText = BuildCssFromForm();
        }
        else
        {
            // テキスト → フォーム: テキストからフォーム値に読み込み
            LoadFormFromCss(CssText);
        }
    }

    private bool _suppressFormChanged;

    private void OnFormFieldChanged()
    {
        if (_suppressFormChanged || IsAdvancedMode) return;
        IsDirty = true;
        var css = BuildCssFromForm();
        CssText = css; // 上級者モードのテキストも同期
        NotifyCssChanged(css);
    }

    private void NotifyCssChanged(string css)
    {
        CssChanged?.Invoke(css);
    }

    // ───── EPUB CSS 読み込み ────────────────────────────────────────

    /// <summary>EPUB の CSS ファイルからエディタを初期化する。</summary>
    public void LoadFromEpub(EpubPreviewService previewService)
    {
        var cssFiles = previewService.GetCssFiles();
        if (cssFiles.Count == 0)
        {
            StatusMessage = "CSS file not found in EPUB";
            return;
        }

        // 最初のテキスト用 CSS を使用（vertical_text.css or horizontal_text.css）
        var cssFile = cssFiles.FirstOrDefault(c =>
            c.Href.Contains("vertical") || c.Href.Contains("horizontal"))
            ?? cssFiles[0];

        _cssFilePath = cssFile.AbsolutePath;
        _originalCssText = File.ReadAllText(_cssFilePath);
        CssText = _originalCssText;

        LoadFormFromCss(_originalCssText);
        IsDirty = false;
        StatusMessage = $"Loaded: {cssFile.Href}";
    }

    private void LoadFormFromCss(string cssText)
    {
        _suppressFormChanged = true;
        try
        {
            var p = CssTemplateService.ParseCss(cssText);
            PageMarginTop = p.PageMargin[0];
            PageMarginRight = p.PageMargin[1];
            PageMarginBottom = p.PageMargin[2];
            PageMarginLeft = p.PageMargin[3];
            BodyMarginTop = p.BodyMargin[0];
            BodyMarginRight = p.BodyMargin[1];
            BodyMarginBottom = p.BodyMargin[2];
            BodyMarginLeft = p.BodyMargin[3];
            FontSize = p.FontSize;
            LineHeight = p.LineHeight;
            IsVertical = p.IsVertical;
            BoldUseGothic = p.BoldUseGothic;
            GothicUseBold = p.GothicUseBold;
        }
        finally
        {
            _suppressFormChanged = false;
        }
    }

    private string BuildCssFromForm()
    {
        var p = new CssStyleParams
        {
            PageMargin = [PageMarginTop, PageMarginRight, PageMarginBottom, PageMarginLeft],
            BodyMargin = [BodyMarginTop, BodyMarginRight, BodyMarginBottom, BodyMarginLeft],
            FontSize = FontSize,
            LineHeight = LineHeight,
            IsVertical = IsVertical,
            BoldUseGothic = BoldUseGothic,
            GothicUseBold = GothicUseBold,
        };
        return CssTemplateService.GenerateCss(p);
    }

    // ───── コマンド ─────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(IsDirty))]
    private void Save()
    {
        if (_cssFilePath == null) return;

        try
        {
            var css = IsAdvancedMode ? CssText : BuildCssFromForm();
            File.WriteAllText(_cssFilePath, css);
            _originalCssText = css;
            IsDirty = false;
            StatusMessage = "Saved";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(IsDirty))]
    private void Reset()
    {
        CssText = _originalCssText;
        LoadFormFromCss(_originalCssText);
        IsDirty = false;
        NotifyCssChanged(_originalCssText);
        StatusMessage = "Reset to original";
    }

    [RelayCommand]
    private void ToggleMode() => IsAdvancedMode = !IsAdvancedMode;

    [RelayCommand]
    private void ToggleVisibility() => IsVisible = !IsVisible;
}
