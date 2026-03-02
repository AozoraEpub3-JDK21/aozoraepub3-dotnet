using CommunityToolkit.Mvvm.ComponentModel;
using AozoraEpub3.Core.Converter;
using AozoraEpub3.Core.Info;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>
/// ローカル変換の詳細設定 60 項目を保持する ViewModel（詳細設定ドロワー用）。
/// 各プロパティは設定項目辞典の英語名に対応する。
/// </summary>
public sealed partial class LocalConvertSettingsViewModel : ViewModelBase
{
    // ══════════════════════════════════════════════════════════════════════════
    // カテゴリ 1: 入力設定
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>入力エンコード ("MS932" / "UTF-8")</summary>
    [ObservableProperty] private string _inputEncoding = "MS932";

    /// <summary>ファイル名から表題・著者を使用</summary>
    [ObservableProperty] private bool _useFileName = false;

    // ══════════════════════════════════════════════════════════════════════════
    // カテゴリ 2: 書誌情報設定
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>表題種別 (0–4)</summary>
    [ObservableProperty] private int _titleType = 0;

    /// <summary>表紙画像種別 (-1=なし / 0=先頭の挿絵 / 1=同名 / 2=ファイル指定)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCoverFileEnabled))]
    private int _coverImageType = -1;

    /// <summary>表紙ファイルパス入力が有効か</summary>
    public bool IsCoverFileEnabled => CoverImageType == 2;

    /// <summary>表紙画像ファイルパス（CoverImageType==2 の時のみ有効）</summary>
    [ObservableProperty] private string _coverImagePath = "";

    /// <summary>表紙ページ挿入</summary>
    [ObservableProperty] private bool _insertCoverPage = false;

    /// <summary>表紙ページを目次に含める</summary>
    [ObservableProperty] private bool _insertCoverPageToc = false;

    /// <summary>表紙最大行数 (0=無制限)</summary>
    [ObservableProperty] private int _maxCoverLine = 0;

    /// <summary>表題ページ出力</summary>
    [ObservableProperty] private bool _titlePageWrite = false;

    /// <summary>表題ページ種別 (0=通常 / 1=中央 / 2=横書き)</summary>
    [ObservableProperty] private int _titlePageType = 0;

    // ══════════════════════════════════════════════════════════════════════════
    // カテゴリ 3: 出力設定
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>出力拡張子 (".epub" / ".kepub.epub")</summary>
    [ObservableProperty] private string _outputExtension = ".epub";

    /// <summary>出力ファイル名を入力ファイル名に合わせる</summary>
    [ObservableProperty] private bool _useInputFileName = false;

    /// <summary>対象端末 (false=汎用 / true=Kindle)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ForKindleIndex))]
    private bool _forKindle = false;

    /// <summary>ComboBox用: 0=汎用, 1=Kindle</summary>
    public int ForKindleIndex
    {
        get => ForKindle ? 1 : 0;
        set => ForKindle = value == 1;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // カテゴリ 4: ページ構成設定
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>縦書き (true) / 横書き (false)</summary>
    [ObservableProperty] private bool _vertical = true;

    /// <summary>目次ページ挿入</summary>
    [ObservableProperty] private bool _insertTocPage = false;

    /// <summary>目次縦書き</summary>
    [ObservableProperty] private bool _tocVertical = false;

    /// <summary>表題を目次に含める</summary>
    [ObservableProperty] private bool _insertTitleToc = true;

    // ══════════════════════════════════════════════════════════════════════════
    // カテゴリ 5: 文字・変換設定
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>挿絵なし</summary>
    [ObservableProperty] private bool _noIllust = false;

    /// <summary>マーク用ID付与</summary>
    [ObservableProperty] private bool _withMarkId = false;

    /// <summary>自動横組み（縦中横）</summary>
    [ObservableProperty] private bool _autoYoko = true;

    /// <summary>数字1桁 自動横組み</summary>
    [ObservableProperty] private bool _autoYokoNum1 = true;

    /// <summary>数字3桁 自動横組み</summary>
    [ObservableProperty] private bool _autoYokoNum3 = true;

    /// <summary>英数字1桁 自動横組み</summary>
    [ObservableProperty] private bool _autoYokoEQ1 = true;

    /// <summary>濁点種別 (0=なし / 1=CSSスタイル / 2=外字)</summary>
    [ObservableProperty] private int _dakutenType = 1;

    /// <summary>IVS BMP出力</summary>
    [ObservableProperty] private bool _ivsBMP = false;

    /// <summary>IVS SSP出力</summary>
    [ObservableProperty] private bool _ivsSSP = true;

    /// <summary>スペース禁則処理 (0=なし / 1=softspace / 2=en-space)</summary>
    [ObservableProperty] private int _spaceHyphenation = 0;

    /// <summary>コメント出力</summary>
    [ObservableProperty] private bool _commentPrint = false;

    /// <summary>コメント変換</summary>
    [ObservableProperty] private bool _commentConvert = false;

    /// <summary>空行削除 (0=しない / 1=1行に集約 / 2=すべて削除)</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMaxEmptyLineEnabled))]
    private int _removeEmptyLine = 0;

    /// <summary>最大空行数入力が有効か</summary>
    public bool IsMaxEmptyLineEnabled => RemoveEmptyLine > 0;

    /// <summary>最大空行数 (0=無制限)</summary>
    [ObservableProperty] private int _maxEmptyLine = 0;

    // ══════════════════════════════════════════════════════════════════════════
    // カテゴリ 6: 章認識設定
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>章名最大文字数</summary>
    [ObservableProperty] private int _chapterNameMaxLength = 64;

    /// <summary>連続章除外</summary>
    [ObservableProperty] private bool _excludeSequentialChapter = true;

    /// <summary>次行を章名に使用</summary>
    [ObservableProperty] private bool _useNextLineChapterName = true;

    /// <summary>節を章として認識</summary>
    [ObservableProperty] private bool _chapterSection = true;

    /// <summary>見出し注記を章として認識（親スイッチ）</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChapterHEnabled))]
    private bool _chapterH = false;

    public bool IsChapterHEnabled => ChapterH;

    /// <summary>大見出し (H1) 認識</summary>
    [ObservableProperty] private bool _chapterH1 = false;

    /// <summary>中見出し (H2) 認識</summary>
    [ObservableProperty] private bool _chapterH2 = false;

    /// <summary>小見出し (H3) 認識</summary>
    [ObservableProperty] private bool _chapterH3 = false;

    /// <summary>同行章認識</summary>
    [ObservableProperty] private bool _sameLineChapter = false;

    /// <summary>章名キーワード認識</summary>
    [ObservableProperty] private bool _chapterName = false;

    /// <summary>数字のみの章認識</summary>
    [ObservableProperty] private bool _chapterNumOnly = false;

    /// <summary>数字＋タイトル章認識</summary>
    [ObservableProperty] private bool _chapterNumTitle = false;

    /// <summary>括弧付き数字章認識</summary>
    [ObservableProperty] private bool _chapterNumParen = false;

    /// <summary>括弧付き数字＋タイトル章認識</summary>
    [ObservableProperty] private bool _chapterNumParenTitle = false;

    /// <summary>カスタム章パターン使用</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChapterPatternEnabled))]
    private bool _chapterPattern = false;

    public bool IsChapterPatternEnabled => ChapterPattern;

    /// <summary>カスタム章パターン（正規表現文字列）</summary>
    [ObservableProperty] private string _chapterPatternText = "";

    // ══════════════════════════════════════════════════════════════════════════
    // カテゴリ 7: 改ページ設定
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>強制改ページ有効（親スイッチ）</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPageBreakEnabled))]
    private bool _pageBreak = false;

    public bool IsPageBreakEnabled => PageBreak;

    /// <summary>強制改ページサイズ (KB)</summary>
    [ObservableProperty] private int _pageBreakSize = 0;

    /// <summary>空行での改ページ</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPageBreakEmptyEnabled))]
    private bool _pageBreakEmpty = false;

    public bool IsPageBreakEmptyEnabled => PageBreak && PageBreakEmpty;

    /// <summary>空行改ページ行数</summary>
    [ObservableProperty] private int _pageBreakEmptyLine = 0;

    /// <summary>空行改ページサイズ (KB)</summary>
    [ObservableProperty] private int _pageBreakEmptySize = 0;

    /// <summary>章での改ページ</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPageBreakChapterEnabled))]
    private bool _pageBreakChapter = false;

    public bool IsPageBreakChapterEnabled => PageBreak && PageBreakChapter;

    /// <summary>章改ページサイズ (KB)</summary>
    [ObservableProperty] private int _pageBreakChapterSize = 0;

    // ══════════════════════════════════════════════════════════════════════════
    // BookInfo への設定適用
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// この ViewModel の設定値を <see cref="BookInfo"/> と <see cref="AozoraEpub3Converter"/> に書き込む。
    /// converter.GetBookInfo() 呼び出し前に実行すること。
    /// </summary>
    public void ApplyTo(BookInfo info, AozoraEpub3Converter converter)
    {
        // ── BookInfo へ設定 ──────────────────────────────────────────
        info.Vertical          = Vertical;
        info.InsertTocPage     = InsertTocPage;
        info.TocVertical       = TocVertical;
        info.InsertTitleToc    = InsertTitleToc;
        info.TitlePageType     = TitlePageWrite ? TitlePageType : BookInfo.TITLE_NONE;
        info.InsertCoverPage   = InsertCoverPage;
        info.InsertCoverPageToc = InsertCoverPageToc;

        // ── AozoraEpub3Converter へ設定 ──────────────────────────────
        converter.vertical = Vertical;
        converter.SetNoIllust(NoIllust);
        converter.SetWithMarkId(WithMarkId);
        converter.SetAutoYoko(AutoYoko, AutoYokoNum1, AutoYokoNum3, AutoYokoEQ1);
        converter.SetCharOutput(DakutenType, IvsBMP, IvsSSP);
        converter.SetSpaceHyphenation(SpaceHyphenation);
        converter.SetCommentPrint(CommentPrint, CommentConvert);
        converter.SetRemoveEmptyLine(RemoveEmptyLine, MaxEmptyLine == 0 ? int.MaxValue : MaxEmptyLine);

        // 強制改ページ
        int pbSize = 0, pbEmpty = 0, pbEmptySize = 0, pbChapter = 0, pbChapterSize = 0;
        if (PageBreak)
        {
            pbSize = PageBreakSize * 1024;
            if (PageBreakEmpty) { pbEmpty = PageBreakEmptyLine; pbEmptySize = PageBreakEmptySize * 1024; }
            if (PageBreakChapter) { pbChapter = 1; pbChapterSize = PageBreakChapterSize * 1024; }
        }
        converter.SetForcePageBreak(pbSize, pbEmpty, pbEmptySize, pbChapter, pbChapterSize);

        // 章認識
        string chapterPatternText = ChapterPattern && !string.IsNullOrWhiteSpace(ChapterPatternText)
            ? ChapterPatternText : "";
        converter.SetChapterLevel(
            ChapterNameMaxLength,
            ExcludeSequentialChapter, UseNextLineChapterName,
            ChapterSection,
            ChapterH, ChapterH1, ChapterH2, ChapterH3,
            SameLineChapter,
            ChapterName,
            ChapterNumOnly, ChapterNumTitle,
            ChapterNumParen, ChapterNumParenTitle,
            chapterPatternText);
    }
}
