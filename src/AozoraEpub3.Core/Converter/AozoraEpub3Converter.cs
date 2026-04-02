using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using AozoraEpub3.Core.Info;
using AozoraEpub3.Core.Io;

namespace AozoraEpub3.Core.Converter;

/// <summary>青空文庫テキストをePub3形式のXHTMLに変換</summary>
public class AozoraEpub3Converter
{
    //---------------- Instance Properties ----------------//
    // unused fields (removed from ConverterSettings; to be deleted in Step 9 cleanup)
    bool _userAlterCharEscape = false;
    bool _autoAlpha2 = false;
    bool _autoAlphaNum2 = false;
    bool _removeHeadSpace = false;

    internal ConverterSettings _settings = new();

    public bool commentPrint
    {
        get => _settings.CommentPrint;
        set => _settings.CommentPrint = value;
    }
    public bool commentConvert
    {
        get => _settings.CommentConvert;
        set => _settings.CommentConvert = value;
    }

    internal ConverterState _state = new();

    //---------------- Chapter Infos ----------------//
    readonly char[] _chapterNumChar = {
        '0','1','2','3','4','5','6','7','8','9',
        '０','１','２','３','４','５','６','７','８','９',
        '〇','一','二','三','四','五','六','七','八','九','十','百',
        '壱','弐','参','肆','伍',
        'Ⅰ','Ⅱ','Ⅲ','Ⅳ','Ⅴ','Ⅵ','Ⅶ','Ⅷ','Ⅸ','Ⅹ','Ⅺ','Ⅻ'
    };
    readonly char[] _chapterSeparator = { ' ', '　', '-', '－', '「', '―', '『', '（' };
    readonly string[] _chapterName = {
        "プロローグ","エピローグ","モノローグ","序","序章","序　章",
        "終章","終　章","間章","間　章","転章","転　章","幕間","幕　間"
    };
    readonly string[] _chapterNumPrefix = { "第", "その", "" };
    readonly string[][] _chapterNumSuffix = {
        new[] { "話","章","篇","部","節","幕","編" },
        new[] { "" },
        new[] { "章" }
    };
    readonly string[] _chapterNumParenPrefix = { "（", "〈", "〔", "【" };
    readonly string[] _chapterNumParenSuffix = { "）", "〉", "〕", "】" };

    //---------------- Flags Variables ----------------//
    // (moved to ConverterState)

    //---------------- Patterns ----------------//
    internal static readonly Regex ChukiPattern = new(@"(［＃.+?］)|(<.+?>)", RegexOptions.Compiled);
    internal static readonly Regex GaijiChukiPattern = new(@"(※［＃.+?］)|(〔.+?〕)|(／″?＼)", RegexOptions.Compiled);
    internal static readonly Regex ChukiSufPattern = new(@"［＃「([^］]+)」([^」^］]+)］", RegexOptions.Compiled);
    internal static readonly Regex ChukiSufPattern2 = new(@"［＃「([^］]+)」([^」^］]*「[^」^］]+」[^」^］]*)］", RegexOptions.Compiled);
    static readonly Regex ChukiLeftPattern = new(@"^［＃(.+?)］", RegexOptions.Compiled);
    static readonly Regex FileNamePattern = new(@"\[(.+?)\]( |　)*(.+?)(\(|（|\.)", RegexOptions.Compiled);
    // ReplaceChukiSufTag の第1パス: ［＃ または ］ にマッチ
    internal static readonly Regex InnerTagPattern = new(@"(［＃|］)", RegexOptions.Compiled);

    //---------------- Static Conversion Tables ----------------//
    static bool _inited = false;
    static readonly object _initLock = new();

    internal static readonly Dictionary<string, string[]> _chukiMap = new();
    internal static readonly HashSet<string> _chukiFlagNoBr = new();
    internal static readonly HashSet<string> _chukiFlagNoRubyStart = new();
    internal static readonly HashSet<string> _chukiFlagNoRubyEnd = new();
    public static readonly HashSet<string> ChukiFlagPageBreak = new();
    internal static readonly HashSet<string> _chukiFlagMiddle = new();
    internal static readonly HashSet<string> _chukiFlagBottom = new();
    internal static readonly HashSet<string> _chukiKunten = new();
    internal static readonly Dictionary<string, string[]> _sufChukiMap = new();
    internal static readonly Dictionary<string, Regex> _chukiPatternMap = new();
    internal static Dictionary<char, string>? _replaceMap = null;
    internal static Dictionary<string, string>? _replace2Map = null;
    internal static Dictionary<int, string>? _utf16FontMap = null;
    internal static Dictionary<int, string>? _utf32FontMap = null;
    internal static Dictionary<string, string>? _ivs16FontMap = null;
    internal static Dictionary<string, string>? _ivs32FontMap = null;
    internal static LatinConverter _latinConverter = new();
    internal static AozoraGaijiConverter _gaijiConverter = new();

    //---------------- Instance ----------------//
    readonly IEpub3Writer _writer;
    readonly CharacterConversionService _charService;
    readonly TcyConversionService _tcyService;
    readonly RubyConversionService _rubyService;
    readonly GaijiChukiService _gaijiChukiService;
    readonly OutputBufferService _outputService;
    readonly ImageService _imageService;
    public bool vertical
    {
        get => _settings.Vertical;
        set => _settings.Vertical = value;
    }
    public int lineNum
    {
        get => _state.LineNum;
        set => _state.LineNum = value;
    }

    //---------------- Page Break Types ----------------//
    internal static readonly PageBreakType _pageBreakNormal = new(true, 0, PageBreakType.IMAGE_PAGE_NONE);
    internal static readonly PageBreakType _pageBreakMiddle = new(true, PageBreakType.PAGE_MIDDLE, PageBreakType.IMAGE_PAGE_NONE);
    internal static readonly PageBreakType _pageBreakBottom = new(true, PageBreakType.PAGE_BOTTOM, PageBreakType.IMAGE_PAGE_NONE);
    internal static readonly PageBreakType _pageBreakImageAuto = new(true, 0, PageBreakType.IMAGE_PAGE_AUTO);
    internal static readonly PageBreakType _pageBreakImageW = new(true, 0, PageBreakType.IMAGE_PAGE_W);
    internal static readonly PageBreakType _pageBreakImageH = new(true, 0, PageBreakType.IMAGE_PAGE_H);
    internal static readonly PageBreakType _pageBreakImageNoFit = new(true, 0, PageBreakType.IMAGE_PAGE_NOFIT);
    internal static readonly PageBreakType _pageBreakNoChapter = new(true, 0, PageBreakType.IMAGE_PAGE_NONE, true);

    //---------------- Public Accessors ----------------//
    public string[]? GetChukiValue(string key) =>
        _chukiMap.TryGetValue(key, out var v) ? v : null;

    //---------------- Constructor ----------------//
    /// <summary>
    /// コンストラクタ。
    /// 変換テーブルやクラスがstaticで初期化されていなければ初期化する。
    /// </summary>
    /// <param name="writer">EPUB出力クラス</param>
    /// <param name="resourcePath">リソースファイルのディレクトリパス（nullなら埋め込みリソースを使用）</param>
    public AozoraEpub3Converter(IEpub3Writer writer, string? resourcePath = null)
    {
        _writer = writer;
        _charService = new CharacterConversionService(_settings, _state);
        _tcyService = new TcyConversionService(_settings, _state, _charService, writer);
        _rubyService = new RubyConversionService(_state, _tcyService, _charService);
        _gaijiChukiService = new GaijiChukiService(_settings, _state);
        _outputService = new OutputBufferService(_settings, _state, writer);
        _imageService = new ImageService(_settings, _state, writer, _outputService);

        lock (_initLock)
        {
            if (_inited) return;
            InitStaticTables(resourcePath ?? "");
            _inited = true;
        }
    }

    /// <summary>static変換テーブルの初期化（初回のみ実行）</summary>
    private static void InitStaticTables(string resourcePath)
    {
        // 拡張ラテン変換
        string latinFilePath = Path.Combine(resourcePath, "chuki_latin.txt");
        _latinConverter = File.Exists(latinFilePath)
            ? new LatinConverter(latinFilePath)
            : new LatinConverter();

        // 外字変換
        string ivsFilePath = Path.Combine(resourcePath, "chuki_ivs.txt");
        _gaijiConverter = File.Exists(ivsFilePath)
            ? new AozoraGaijiConverter(resourcePath)
            : new AozoraGaijiConverter();

        // 注記タグ変換テーブル（chuki_tag.txt）
        LoadChukiTagFile(resourcePath);

        // パターンマップ
        _chukiPatternMap["折り返し"] = new Regex(@"^［＃ここから([０-９]+)字下げ、折り返して([０-９]+)字下げ(.*)］", RegexOptions.Compiled);
        _chukiPatternMap["字下げ字詰め"] = new Regex(@"^［＃ここから([０-９]+)字下げ、([０-９]+)字詰め.*］", RegexOptions.Compiled);
        _chukiPatternMap["字下げ複合"] = new Regex(@"^［＃ここから([０-９]+)字下げ.*］", RegexOptions.Compiled);
        _chukiPatternMap["字下げ終わり複合"] = new Regex(@"^［＃ここで字下げ.*終わり", RegexOptions.Compiled);

        // 前方参照注記テーブル（chuki_tag_suf.txt）
        LoadChukiSufFile(resourcePath);

        // 単純文字置換（replace.txt、オプション）
        string replaceFilePath = Path.Combine(resourcePath, "replace.txt");
        if (File.Exists(replaceFilePath))
            LoadReplaceFile(replaceFilePath);
    }

    /// <summary>chuki_tag.txt を読み込んで変換テーブルを構築</summary>
    private static void LoadChukiTagFile(string resourcePath)
    {
        Stream? stream = OpenResource(resourcePath, "chuki_tag.txt");
        if (stream == null) throw new IOException("chuki_tag.txt not found");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        int lineNum = 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNum++;
            if (line.Length == 0 || line[0] == '#') continue;
            try
            {
                string[] values = line.Split('\t');
                string[] tags;
                if (values.Length == 1) tags = new[] { "" };
                else if (values.Length > 2 && values[2].Length > 0)
                    tags = new[] { ConvertJavaFormat(values[1]), ConvertJavaFormat(values[2]) };
                else tags = new[] { ConvertJavaFormat(values[1]) };
                _chukiMap[values[0]] = tags;

                if (values.Length > 3 && values[3].Length > 0)
                {
                    switch (values[3][0])
                    {
                        case '1': _chukiFlagNoBr.Add(values[0]); break;
                        case '2': _chukiFlagNoRubyStart.Add(values[0]); break;
                        case '3': _chukiFlagNoRubyEnd.Add(values[0]); break;
                        case 'P': ChukiFlagPageBreak.Add(values[0]); break;
                        case 'M': ChukiFlagPageBreak.Add(values[0]); _chukiFlagMiddle.Add(values[0]); break;
                        case 'K': _chukiKunten.Add(values[0]); break;
                        case 'L': ChukiFlagPageBreak.Add(values[0]); _chukiFlagBottom.Add(values[0]); break;
                    }
                }
            }
            catch { LogAppender.Error(lineNum, "chuki_tag.txt", line); }
        }
    }

    /// <summary>Java String.format の %s/%% を C# string.Format の {0}/{1}/% に変換</summary>
    private static string ConvertJavaFormat(string s)
    {
        if (!s.Contains('%')) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        int argIdx = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '%' && i + 1 < s.Length)
            {
                if (s[i + 1] == 's') { sb.Append('{').Append(argIdx++).Append('}'); i++; }
                else if (s[i + 1] == '%') { sb.Append('%'); i++; }
                else sb.Append(s[i]);
            }
            else if (s[i] == '{') sb.Append("{{");
            else if (s[i] == '}') sb.Append("}}");
            else sb.Append(s[i]);
        }
        return sb.ToString();
    }

    /// <summary>chuki_tag_suf.txt を読み込んで前方参照注記テーブルを構築</summary>
    private static void LoadChukiSufFile(string resourcePath)
    {
        Stream? stream = OpenResource(resourcePath, "chuki_tag_suf.txt");
        if (stream == null) throw new IOException("chuki_tag_suf.txt not found");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        int lineNum = 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNum++;
            if (line.Length == 0 || line[0] == '#') continue;
            try
            {
                string[] values = line.Split('\t');
                string[] tags;
                if (values.Length > 2 && values[2].Length > 0) tags = new[] { values[1], values[2] };
                else tags = new[] { values[1] };
                _sufChukiMap[values[0]] = tags;
                // 別名
                if (values.Length > 3 && values[3].Length > 0)
                    _sufChukiMap[values[3] + values[0]] = tags;
            }
            catch { LogAppender.Error(lineNum, "chuki_tag_suf.txt", line); }
        }
    }

    /// <summary>replace.txt を読み込んで文字置換テーブルを構築（任意）</summary>
    private static void LoadReplaceFile(string filePath)
    {
        _replaceMap = new Dictionary<char, string>();
        _replace2Map = new Dictionary<string, string>();
        using var reader = new StreamReader(filePath, Encoding.UTF8);
        int lineNum = 0;
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            lineNum++;
            if (line.Length == 0 || line[0] == '#') continue;
            try
            {
                string[] values = line.Split('\t');
                if (values[0].Length == 1)
                    _replaceMap[values[0][0]] = values[1];
                else if (values[0].Length == 2)
                    _replace2Map[values[0]] = values[1];
                else
                    LogAppender.Error(lineNum, Path.GetFileName(filePath) + " too long", line);
            }
            catch { LogAppender.Error(lineNum, Path.GetFileName(filePath), line); }
        }
    }

    /// <summary>外字フォントマップをライターのフォントパスから構築</summary>
    public void InitGaijiFontMap()
    {
        string gaijiFontPath = _writer.GetGaijiFontPath();
        if (!Directory.Exists(gaijiFontPath)) return;

        var utf16 = new Dictionary<int, string>();
        var utf32 = new Dictionary<int, string>();
        var ivs16 = new Dictionary<string, string>();
        var ivs32 = new Dictionary<string, string>();

        foreach (string fontFile in Directory.GetFiles(gaijiFontPath))
        {
            string fileName = Path.GetFileName(fontFile).ToLowerInvariant();
            string ext = Path.GetExtension(fileName).TrimStart('.');
            if (ext != "ttf" && ext != "ttc" && ext != "otf") continue;
            if (!fileName.StartsWith('u')) continue;

            string baseName = Path.GetFileNameWithoutExtension(fileName);
            if (baseName.Contains("-u"))
            {
                // IVS合成フォント: u{code1}-u{code2}.ttf
                string className = baseName;
                string[] parts = className.Split(new[] { "-u" }, StringSplitOptions.None);
                if (parts.Length != 2)
                {
                    LogAppender.Warn(-1, "IVS以外の合成フォントは対応しません", Path.GetFileName(fontFile));
                    continue;
                }
                if (!int.TryParse(parts[0][1..], System.Globalization.NumberStyles.HexNumber, null, out int code1) ||
                    !int.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out int code2))
                    continue;
                if (0xe0100 <= code2 && code2 <= 0xe01ef)
                {
                    if (code1 <= 0xFFFF)
                        ivs16["u" + className] = Path.GetFileName(fontFile);
                    else
                        ivs32["u" + className] = Path.GetFileName(fontFile);
                }
                else
                {
                    LogAppender.Warn(-1, "IVS以外の合成フォントは対応しません", Path.GetFileName(fontFile));
                }
            }
            else
            {
                // 単体フォント: u{code}.ttf
                if (!int.TryParse(baseName[1..], System.Globalization.NumberStyles.HexNumber, null, out int code))
                    continue;
                if (code <= 0xFFFF)
                    utf16[code] = Path.GetFileName(fontFile);
                else
                    utf32[code] = Path.GetFileName(fontFile);
            }
        }

        lock (_initLock)
        {
            _utf16FontMap = utf16.Count > 0 ? utf16 : null;
            _utf32FontMap = utf32.Count > 0 ? utf32 : null;
            _ivs16FontMap = ivs16.Count > 0 ? ivs16 : null;
            _ivs32FontMap = ivs32.Count > 0 ? ivs32 : null;
        }
    }

    /// <summary>ファイルシステムまたは埋め込みリソースからストリームを開く</summary>
    private static Stream? OpenResource(string resourcePath, string fileName)
    {
        if (!string.IsNullOrEmpty(resourcePath))
        {
            string filePath = Path.Combine(resourcePath, fileName);
            if (File.Exists(filePath))
                return new FileStream(filePath, FileMode.Open, FileAccess.Read);
        }
        // 埋め込みリソースにフォールバック
        var asm = Assembly.GetExecutingAssembly();
        string? resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        return resourceName != null ? asm.GetManifestResourceStream(resourceName) : null;
    }

    //---------------- Setters ----------------//

    public void SetNoIllust(bool noIllust) => _settings.NoIllust = noIllust;

    public void SetImageFloat(bool imageFloatPage, bool imageFloatBlock)
    {
        _settings.ImageFloatPage = imageFloatPage;
        _settings.ImageFloatBlock = imageFloatBlock;
    }

    public void SetWithMarkId(bool withIdSpan) => _settings.WithMarkId = withIdSpan;

    public void SetAutoYoko(bool autoYoko, bool autoYokoNum1, bool autoYokoNum3, bool autoYokoEQ1)
    {
        _settings.AutoYoko = autoYoko;
        _settings.AutoYokoNum1 = autoYokoNum1;
        _settings.AutoYokoNum3 = autoYokoNum3;
        _settings.AutoYokoEQ1 = autoYokoEQ1;
    }

    public void SetCharOutput(int dakutenType, bool printIvsBMP, bool printIvsSSP)
    {
        _settings.DakutenType = dakutenType;
        _settings.PrintIvsBMP = printIvsBMP;
        _settings.PrintIvsSSP = printIvsSSP;
    }

    public void SetCommentPrint(bool commentPrint, bool commentConvert)
    {
        this.commentPrint = commentPrint;
        this.commentConvert = commentConvert;
    }

    public void SetRemoveEmptyLine(int removeEmptyLine, int maxEmptyLine)
    {
        _settings.RemoveEmptyLine = removeEmptyLine;
        _settings.MaxEmptyLine = maxEmptyLine == 0 ? int.MaxValue : maxEmptyLine;
    }

    public void SetForceIndent(bool forceIndent) => _settings.ForceIndent = forceIndent;

    public void SetChapterLevel(
        int maxLength, bool excludeSeqencialChapter, bool useNextLineChapterName,
        bool section, bool h, bool h1, bool h2, bool h3,
        bool userSameLineChapter,
        bool chapterName, bool autoChapterNumOnly, bool autoChapterNumTitle,
        bool autoChapterNumParen, bool autoChapterNumParenTitle,
        string chapterPattern)
    {
        _settings.MaxChapterNameLength = maxLength;
        _settings.ChapterSection = section;

        if (_settings.ChapterChukiMap == null) _settings.ChapterChukiMap = new Dictionary<string, int>();
        else _settings.ChapterChukiMap.Clear();

        if (h)
        {
            _settings.ChapterChukiMap["ここから見出し"] = ChapterLineInfo.TYPE_CHUKI_H;
            _settings.ChapterChukiMap["見出し"] = ChapterLineInfo.TYPE_CHUKI_H;
            if (userSameLineChapter) _settings.ChapterChukiMap["同行見出し"] = ChapterLineInfo.TYPE_CHUKI_H;
        }
        if (h1)
        {
            _settings.ChapterChukiMap["ここから大見出し"] = ChapterLineInfo.TYPE_CHUKI_H1;
            _settings.ChapterChukiMap["大見出し"] = ChapterLineInfo.TYPE_CHUKI_H1;
            if (userSameLineChapter) _settings.ChapterChukiMap["同行大見出し"] = ChapterLineInfo.TYPE_CHUKI_H1;
        }
        if (h2)
        {
            _settings.ChapterChukiMap["ここから中見出し"] = ChapterLineInfo.TYPE_CHUKI_H2;
            _settings.ChapterChukiMap["中見出し"] = ChapterLineInfo.TYPE_CHUKI_H2;
            if (userSameLineChapter) _settings.ChapterChukiMap["同行中見出し"] = ChapterLineInfo.TYPE_CHUKI_H2;
        }
        if (h3)
        {
            _settings.ChapterChukiMap["ここから小見出し"] = ChapterLineInfo.TYPE_CHUKI_H3;
            _settings.ChapterChukiMap["小見出し"] = ChapterLineInfo.TYPE_CHUKI_H3;
            if (userSameLineChapter) _settings.ChapterChukiMap["同行小見出し"] = ChapterLineInfo.TYPE_CHUKI_H3;
        }

        _settings.UseNextLineChapterName = useNextLineChapterName;
        _settings.ExcludeSequentialChapter = excludeSeqencialChapter;
        _settings.AutoChapterName = chapterName;
        _settings.AutoChapterNumOnly = autoChapterNumOnly;
        _settings.AutoChapterNumTitle = autoChapterNumTitle;
        _settings.AutoChapterNumParen = autoChapterNumParen;
        _settings.AutoChapterNumParenTitle = autoChapterNumParenTitle;

        _settings.ChapterPattern = null;
        if (!string.IsNullOrEmpty(chapterPattern))
        {
            try { _settings.ChapterPattern = new Regex(chapterPattern, RegexOptions.Compiled); }
            catch { LogAppender.Println("[WARN] 目次抽出のその他パターンが正しくありません: " + chapterPattern); }
        }
    }

    public int GetSpaceHyphenation() => _settings.SpaceHyphenation;
    public void SetSpaceHyphenation(int type) => _settings.SpaceHyphenation = type;

    public void SetChukiRuby(bool chukiRuby, bool chukiKogaki)
    {
        _settings.ChukiRuby = chukiRuby;
        _settings.ChukiKogaki = chukiKogaki;
    }

    public void SetForcePageBreak(int forcePageBreakSize, int emptyLine, int emptySize, int chapterLevel, int chapterSize)
    {
        _settings.ForcePageBreakSize = forcePageBreakSize;
        _settings.ForcePageBreakEmptyLine = emptyLine;
        _settings.ForcePageBreakEmptySize = emptySize;
        _settings.ForcePageBreakChapterLevel = chapterLevel;
        _settings.ForcePageBreakChapterSize = chapterSize;

        _settings.ForcePageBreak = forcePageBreakSize > 0
            || (emptyLine > 0 && emptySize > 0)
            || (chapterLevel > 0 && chapterSize > 0);

        if (emptyLine > 0) _settings.ForcePageBreakSize = Math.Max(_settings.ForcePageBreakSize, emptySize);
        if (chapterLevel > 0) _settings.ForcePageBreakSize = Math.Max(_settings.ForcePageBreakSize, chapterSize);
    }

    /// <summary>変換キャンセル</summary>
    public void Cancel() => _state.Canceled = true;

    //============================================================
    // Phase 6b: GetBookInfo + Chapter helpers
    //============================================================

    /// <summary>
    /// テキストを先読みして書誌情報（表題・著者・章情報等）を収集する。
    /// </summary>
    public BookInfo GetBookInfo(string srcFilePath, TextReader src, ImageInfoReader imageInfoReader,
        BookInfo.TitleType titleType, bool pubFirst)
    {
        try
        {
            BookInfo bookInfo = new BookInfo(srcFilePath);

            string? line;
            lineNum = -1;
            string?[] preLines = new string?[] { null, null };

            bool inComment = false;
            bool firstCommentStarted = false;
            int firstCommentLineNum = -1;

            string?[] firstLines = new string?[10];
            int firstLineStart = -1;

            int commentLineNum = 0;
            int commentLineStart = -1;

            int lastEmptyLine = -1;

            bool autoChapter = _settings.AutoChapterName || _settings.AutoChapterNumTitle || _settings.AutoChapterNumOnly ||
                               _settings.AutoChapterNumParen || _settings.AutoChapterNumParenTitle || _settings.ChapterPattern != null;
            bool addSectionChapter = true;
            bool addChapterName = false;
            int addNextChapterName = -1;
            ChapterLineInfo? preChapterLineInfo = null;

            while ((line = src.ReadLine()) != null)
            {
                lineNum++;

                // 見出し等の取得のため前方参照注記は変換し外字文字を置換
                line = CharUtils.RemoveSpace(_gaijiChukiService.ReplaceChukiSufTag(_gaijiChukiService.ConvertGaijiChuki(line, true, false)));
                // 注記と画像のチェックなので先にルビ除去
                string noRubyLine = CharUtils.RemoveRuby(line);

                // コメント除外 50文字以上をコメントにする
                if (noRubyLine.StartsWith("--------------------------------"))
                {
                    if (!noRubyLine.StartsWith("--------------------------------------------------"))
                    {
                        LogAppender.Warn(lineNum, "コメント行の文字数が足りません");
                    }
                    else
                    {
                        if (firstCommentLineNum == -1) firstCommentLineNum = lineNum;
                        firstCommentStarted = true;
                        if (inComment)
                        {
                            if (commentLineNum > 20)
                                LogAppender.Warn(lineNum, $"コメントが {commentLineNum} 行 ({commentLineStart + 1}) -");
                            commentLineNum = 0;
                            inComment = false;
                            continue;
                        }
                        else
                        {
                            if (lineNum > 10 && !(commentPrint && commentConvert))
                                LogAppender.Warn(lineNum, "コメント開始行が10行目以降にあります");
                            commentLineStart = lineNum;
                            inComment = true;
                            continue;
                        }
                    }
                    if (inComment) commentLineNum++;
                }

                // 空行チェック
                if (noRubyLine == "" || noRubyLine == " " || noRubyLine == "　")
                {
                    lastEmptyLine = lineNum;
                    continue;
                }

                if (inComment && !commentPrint) continue;

                // 2行前が改ページと画像の行かをチェックして行番号をbookInfoに保存
                if (!_settings.NoIllust) _imageService.CheckImageOnly(bookInfo, preLines, noRubyLine, lineNum);

                // 見出しのChapter追加
                if (addChapterName)
                {
                    if (preChapterLineInfo == null)
                    {
                        addChapterName = false;
                    }
                    else
                    {
                        string cname = GetChapterName(noRubyLine);
                        // 字下げ注記等は飛ばして次の行を見る
                        if (cname.Length > 0)
                        {
                            preChapterLineInfo.ChapterName = cname;
                            preChapterLineInfo.LineNum = lineNum;
                            addChapterName = false;
                            if (_settings.UseNextLineChapterName) addNextChapterName = lineNum + 1;
                            addSectionChapter = false;
                        }
                        // 必ず文字が入る
                        preChapterLineInfo = null;
                    }
                }

                // 画像のファイル名の順にimageInfoReaderにファイル名を追加
                var chukiMatches = ChukiPattern.Matches(noRubyLine);
                foreach (Match m in chukiMatches)
                {
                    string chukiTag = m.Value;
                    // ［＃...］ 形式のとき chukiName を取得（<tag> 形式は空になるが問題なし）
                    string chukiName = chukiTag.Length > 3 ? chukiTag[2..(chukiTag.Length - 1)] : "";

                    if (ChukiFlagPageBreak.Contains(chukiName))
                    {
                        addSectionChapter = true;
                    }
                    else if (_settings.ChapterChukiMap != null && _settings.ChapterChukiMap.TryGetValue(chukiName, out int chapterType))
                    {
                        // 見出し注記: 注記の後に文字がなければブロックなので次の行
                        int matchEnd = m.Index + m.Length;
                        if (noRubyLine.Length == matchEnd)
                        {
                            preChapterLineInfo = new ChapterLineInfo(lineNum + 1, chapterType, addSectionChapter,
                                ChapterLineInfo.GetLevel(chapterType), lastEmptyLine == lineNum - 1);
                            bookInfo.AddChapterLineInfo(preChapterLineInfo);
                            addChapterName = true;
                            addNextChapterName = -1;
                        }
                        else
                        {
                            bookInfo.AddChapterLineInfo(new ChapterLineInfo(lineNum, chapterType, addSectionChapter,
                                ChapterLineInfo.GetLevel(chapterType), lastEmptyLine == lineNum - 1,
                                GetChapterName(noRubyLine[matchEnd..])));
                            if (_settings.UseNextLineChapterName) addNextChapterName = lineNum + 1;
                            addChapterName = false;
                        }
                        addSectionChapter = false;
                    }

                    string lowerChukiTag = chukiTag.ToLower();
                    int imageStartIdx = chukiTag.LastIndexOf('（');
                    if (imageStartIdx > -1)
                    {
                        int imageEndIdx = chukiTag.IndexOf('）', imageStartIdx);
                        int imageDotIdx = chukiTag.IndexOf('.', imageStartIdx);
                        // 訓点送り仮名チェック: ＃の次が（で.を含まない場合は画像でない
                        if (imageDotIdx > -1 && imageDotIdx < imageEndIdx)
                        {
                            string? imageFileName = GetImageChukiFileName(chukiTag, imageStartIdx);
                            if (imageFileName != null)
                            {
                                imageInfoReader.AddImageFileName(imageFileName);
                                if (bookInfo.FirstImageLineNum == -1)
                                {
                                    // 小さい画像は無視
                                    string? corrected = imageInfoReader.CorrectExt(imageFileName);
                                    ImageInfo? imageInfo = corrected != null ? imageInfoReader.GetImageInfo(corrected) : null;
                                    if (imageInfo != null && imageInfo.Width > 64 && imageInfo.Height > 64)
                                    {
                                        bookInfo.FirstImageLineNum = lineNum;
                                        bookInfo.FirstImageIdx = imageInfoReader.CountImageFileNames() - 1;
                                    }
                                }
                            }
                        }
                    }
                    else if (lowerChukiTag.StartsWith("<img"))
                    {
                        string? imageFileName = GetTagAttr(chukiTag, "src");
                        if (imageFileName != null)
                        {
                            imageInfoReader.AddImageFileName(imageFileName);
                            if (bookInfo.FirstImageLineNum == -1)
                            {
                                string? corrected = imageInfoReader.CorrectExt(imageFileName);
                                ImageInfo? imageInfo = corrected != null ? imageInfoReader.GetImageInfo(corrected) : null;
                                if (imageInfo != null && imageInfo.Width > 64 && imageInfo.Height > 64)
                                {
                                    bookInfo.FirstImageLineNum = lineNum;
                                    bookInfo.FirstImageIdx = imageInfoReader.CountImageFileNames() - 1;
                                }
                            }
                        }
                    }
                }

                // 見出し行パターン抽出（パターン抽出時はレベル+10）
                if (autoChapter && bookInfo.GetChapterLevel(lineNum) == 0)
                {
                    string noChukiLine = CharUtils.RemoveSpace(CharUtils.RemoveTag(noRubyLine));
                    int noChukiLineLength = noChukiLine.Length;

                    // その他パターン
                    if (_settings.ChapterPattern != null)
                    {
                        if (_settings.ChapterPattern.IsMatch(noChukiLine))
                        {
                            bookInfo.AddChapterLineInfo(new ChapterLineInfo(lineNum, ChapterLineInfo.TYPE_PATTERN,
                                addSectionChapter, ChapterLineInfo.GetLevel(ChapterLineInfo.TYPE_PATTERN),
                                lastEmptyLine == lineNum - 1, GetChapterName(noRubyLine)));
                            if (_settings.UseNextLineChapterName) addNextChapterName = lineNum + 1;
                            addSectionChapter = false;
                        }
                    }

                    if (_settings.AutoChapterName)
                    {
                        bool isChapter = false;
                        // 数字を含まない章名
                        for (int i = 0; i < _chapterName.Length; i++)
                        {
                            string prefix = _chapterName[i];
                            if (noChukiLine.StartsWith(prefix))
                            {
                                if (noChukiLine.Length == prefix.Length) { isChapter = true; break; }
                                else if (IsChapterSeparator(noChukiLine[prefix.Length])) { isChapter = true; break; }
                            }
                        }
                        // 数字を含む章名
                        if (!isChapter)
                        {
                            for (int i = 0; i < _chapterNumPrefix.Length && !isChapter; i++)
                            {
                                string prefix = _chapterNumPrefix[i];
                                if (noChukiLine.StartsWith(prefix))
                                {
                                    int idx = prefix.Length;
                                    while (noChukiLineLength > idx && IsChapterNum(noChukiLine[idx])) idx++;
                                    if (idx <= prefix.Length) break;
                                    foreach (string suffix in _chapterNumSuffix[i])
                                    {
                                        if (suffix != "")
                                        {
                                            if (noChukiLine[idx..].StartsWith(suffix))
                                            {
                                                idx += suffix.Length;
                                                if (noChukiLine.Length == idx) { isChapter = true; break; }
                                                else if (IsChapterSeparator(noChukiLine[idx])) { isChapter = true; break; }
                                            }
                                        }
                                        else
                                        {
                                            if (noChukiLine.Length == idx) { isChapter = true; break; }
                                            else if (IsChapterSeparator(noChukiLine[idx])) { isChapter = true; break; }
                                        }
                                    }
                                }
                            }
                        }
                        if (isChapter)
                        {
                            bookInfo.AddChapterLineInfo(new ChapterLineInfo(lineNum, ChapterLineInfo.TYPE_CHAPTER_NAME,
                                addSectionChapter, ChapterLineInfo.GetLevel(ChapterLineInfo.TYPE_CHAPTER_NAME),
                                lastEmptyLine == lineNum - 1, GetChapterName(noRubyLine)));
                            if (_settings.UseNextLineChapterName) addNextChapterName = lineNum + 1;
                            addChapterName = false;
                            addSectionChapter = false;
                        }
                    }

                    if (_settings.AutoChapterNumOnly || _settings.AutoChapterNumTitle)
                    {
                        int idx = 0;
                        while (noChukiLineLength > idx && IsChapterNum(noChukiLine[idx])) idx++;
                        if (idx > 0)
                        {
                            if ((_settings.AutoChapterNumOnly && noChukiLine.Length == idx) ||
                                (_settings.AutoChapterNumTitle && noChukiLine.Length > idx && IsChapterSeparator(noChukiLine[idx])))
                            {
                                bookInfo.AddChapterLineInfo(new ChapterLineInfo(lineNum, ChapterLineInfo.TYPE_CHAPTER_NUM,
                                    addSectionChapter, ChapterLineInfo.GetLevel(ChapterLineInfo.TYPE_CHAPTER_NUM),
                                    lastEmptyLine == lineNum - 1, GetChapterName(noRubyLine)));
                                if (_settings.UseNextLineChapterName) addNextChapterName = lineNum + 1;
                                addChapterName = false;
                                addSectionChapter = false;
                            }
                        }
                    }

                    if (_settings.AutoChapterNumParen || _settings.AutoChapterNumParenTitle)
                    {
                        for (int i = 0; i < _chapterNumParenPrefix.Length; i++)
                        {
                            string prefix = _chapterNumParenPrefix[i];
                            if (noChukiLine.StartsWith(prefix))
                            {
                                int idx = prefix.Length;
                                while (noChukiLineLength > idx && IsChapterNum(noChukiLine[idx])) idx++;
                                if (idx <= prefix.Length) break;
                                string suffix = _chapterNumParenSuffix[i];
                                if (noChukiLine[idx..].StartsWith(suffix))
                                {
                                    idx += suffix.Length;
                                    if ((_settings.AutoChapterNumParen && noChukiLine.Length == idx) ||
                                        (_settings.AutoChapterNumParenTitle && noChukiLine.Length > idx && IsChapterSeparator(noChukiLine[idx])))
                                    {
                                        bookInfo.AddChapterLineInfo(new ChapterLineInfo(lineNum, ChapterLineInfo.TYPE_CHAPTER_NUM,
                                            addSectionChapter, 13, lastEmptyLine == lineNum - 1,
                                            GetChapterName(noRubyLine)));
                                        if (_settings.UseNextLineChapterName) addNextChapterName = lineNum + 1;
                                        addChapterName = false;
                                        addSectionChapter = false;
                                    }
                                }
                                break; // prefix matched, stop looking for other prefixes
                            }
                        }
                    }
                }

                // 改ページ後の注記以外の本文を追加
                if (_settings.ChapterSection && addSectionChapter)
                {
                    // 底本：は目次に出さない
                    if (noRubyLine.Length > 2 && noRubyLine[0] == '底' && noRubyLine[1] == '本' && noRubyLine[2] == '：')
                    {
                        addSectionChapter = false;
                    }
                    else
                    {
                        string name = GetChapterName(noRubyLine);
                        // 記号のみの行は無視
                        if (Regex.Replace(name, @"◇|◆|□|■|▽|▼|☆|★|＊|＋|×|†|　", "").Length > 0)
                        {
                            bookInfo.AddChapterLineInfo(new ChapterLineInfo(lineNum, ChapterLineInfo.TYPE_PAGEBREAK,
                                true, 1, lastEmptyLine == lineNum - 1, name));
                            if (_settings.UseNextLineChapterName) addNextChapterName = lineNum + 1;
                            addSectionChapter = false;
                        }
                    }
                }

                // 見出しの次の行かつ見出しでない場合は章名に連結
                if (addNextChapterName == lineNum && bookInfo.GetChapterLineInfo(lineNum) == null)
                {
                    string name = GetChapterName(noRubyLine);
                    if (name.Length > 0)
                    {
                        ChapterLineInfo? info = bookInfo.GetChapterLineInfo(lineNum - 1);
                        if (info != null) info.JoinChapterName(name);
                    }
                    addNextChapterName = -1;
                }

                // コメント行の後はタイトル取得はしない
                if (!firstCommentStarted)
                {
                    string replaced = CharUtils.GetChapterName(noRubyLine, 0);
                    if (firstLineStart == -1)
                    {
                        if (replaced.Length > 0)
                        {
                            firstLineStart = lineNum;
                            firstLines[0] = line;
                        }
                    }
                    else
                    {
                        if (IsPageBreakLine(noRubyLine)) firstCommentStarted = true;
                        if (lineNum - firstLineStart > firstLines.Length - 1)
                        {
                            firstCommentStarted = true;
                        }
                        else if (replaced.Length > 0)
                        {
                            firstLines[lineNum - firstLineStart] = line;
                        }
                    }
                }

                // 前の2行を保存
                preLines[1] = preLines[0];
                preLines[0] = noRubyLine;
            }

            // 行数設定
            bookInfo.TotalLineNum = lineNum;

            if (inComment)
                LogAppender.Error(commentLineStart, "コメントが閉じていません");

            // 表題と著者を先頭行から設定
            bookInfo.SetMetaInfo(titleType, pubFirst, firstLines!, firstLineStart, firstCommentLineNum);

            // タイトルのChapter追加
            if (bookInfo.TitleLine > -1)
            {
                string name = GetChapterName(bookInfo.Title ?? "");
                ChapterLineInfo? titleChapterInfo = bookInfo.GetChapterLineInfo(bookInfo.TitleLine);
                if (titleChapterInfo == null)
                    bookInfo.AddChapterLineInfo(new ChapterLineInfo(bookInfo.TitleLine, ChapterLineInfo.TYPE_TITLE,
                        true, 0, false, name));
                else { titleChapterInfo.Type = ChapterLineInfo.TYPE_TITLE; titleChapterInfo.Level = 0; }
                // 1行目がタイトルでなければ除外
                if (bookInfo.TitleLine > 0)
                {
                    for (int i = bookInfo.TitleLine - 1; i >= 0; i--)
                        bookInfo.RemoveChapterLineInfo(i);
                }
            }

            if (bookInfo.OrgTitleLine > 0) bookInfo.RemoveChapterLineInfo(bookInfo.OrgTitleLine);
            if (bookInfo.SubTitleLine > 0) bookInfo.RemoveChapterLineInfo(bookInfo.SubTitleLine);
            if (bookInfo.SubOrgTitleLine > 0) bookInfo.RemoveChapterLineInfo(bookInfo.SubOrgTitleLine);

            // 目次ページの見出しを除外
            if (_settings.ExcludeSequentialChapter) bookInfo.ExcludeTocChapter();

            return bookInfo;
        }
        catch (Exception e)
        {
            LogAppender.Error(lineNum, "");
            throw;
        }
    }

    /// <summary>目次やタイトル用の文字列を取得</summary>
    private string GetChapterName(string line) =>
        CharUtils.GetChapterName(line, _settings.MaxChapterNameLength);

    /// <summary>文字が章の数字ならtrue</summary>
    private bool IsChapterNum(char c)
    {
        foreach (char num in _chapterNumChar)
            if (c == num) return true;
        return false;
    }

    /// <summary>文字が章の後の区切り文字ならtrue</summary>
    private bool IsChapterSeparator(char c)
    {
        foreach (char sep in _chapterSeparator)
            if (c == sep) return true;
        return false;
    }

    /// <summary>改ページのある行か判別</summary>
    private bool IsPageBreakLine(string line)
    {
        var m = ChukiLeftPattern.Match(line);
        if (m.Success)
            return ChukiFlagPageBreak.Contains(m.Groups[1].Value);
        return false;
    }

    /// <summary>タグからattr属性値を取得</summary>
    public string? GetTagAttr(string tag, string attr) => _imageService.GetTagAttr(tag, attr);

    /// <summary>画像注記にキャプション付きの指定がある場合true</summary>
    public bool HasImageCaption(string chukiTag) => _imageService.HasImageCaption(chukiTag);

    /// <summary>画像注記からファイル名取得</summary>
    /// <param name="chukiTag">注記全体</param>
    /// <param name="startIdx">画像注記の'（'の位置</param>
    public string? GetImageChukiFileName(string chukiTag, int startIdx) => _imageService.GetImageChukiFileName(chukiTag, startIdx);


    //============================================================
    // Phase 6c: ConvertTextToEpub3 - メイン変換ループ
    //============================================================

    /// <summary>
    /// テキストを読み取り EPUB3 XHTML に変換して output に書き出す。
    /// Java: convertTextToEpub3(BufferedWriter out, BufferedReader src, BookInfo bookInfo)
    /// </summary>
    public void ConvertTextToEpub3(TextWriter output, TextReader src, BookInfo bookInfo)
    {
        // ダミー切り替え用バックアップ
        TextWriter orgOut = output;

        _state.Canceled = false;

        // BookInfo の参照を保持
        _state.BookInfo = bookInfo;

        string? line;

        // 変換開始時のメンバ変数の初期化
        _state.PageByteSize = 0;
        _state.SectionCharLength = 0;
        lineNum = -1;
        _state.LineIdNum = 1;
        _state.TagLevel = 0;
        _state.InJisage = -1;
        // 最初のページの改ページフラグを設定
        _outputService.SetPageBreakTrigger(_pageBreakNormal);

        // 直前の tagLevel=0 の行番号
        int lastZeroTagLevelLineNum = -1;

        // タイトル目の画像等をバッファ
        List<string>? preTitleBuf = null;

        // コメントブロック内フラグ
        bool inComment = false;

        // タイトルを出力しない
        bool skipTitle = false;
        // バッファ中は画像は処理しない
        bool noImage = false;

        // 表題をバッファ処理するケース
        if (bookInfo.TitlePageType == BookInfo.TITLE_NONE
            || bookInfo.TitlePageType == BookInfo.TITLE_MIDDLE
            || bookInfo.TitlePageType == BookInfo.TITLE_HORIZONTAL)
        {
            bookInfo.InsertTitlePage = true;
            skipTitle = true;
            output = null!;           // null 出力で抑制（後で orgOut に戻す）
            preTitleBuf = new List<string>();
            noImage = true;
        }

        // 先頭行取得
        line = src.ReadLine();
        if (line == null) return;

        // BOM 除去
        line = CharUtils.RemoveBOM(line);

        try
        {
            do
            {
                lineNum++;

                if (skipTitle)
                {
                    // タイトル文字行前までバッファ
                    if (bookInfo.MetaLineStart > lineNum)
                        preTitleBuf!.Add(line);

                    // タイトル文字行: バッファがあれば出力
                    if (bookInfo.MetaLineStart == lineNum && preTitleBuf!.Count > 0)
                    {
                        noImage = false;
                        if (lastZeroTagLevelLineNum >= 0)
                        {
                            // lastZeroTagLevelLineNum 以前を orgOut へ、以降は null へ
                            int lineNumBak = lineNum;
                            _state.PageByteSize = 0;
                            _state.SectionCharLength = 0;
                            lineNum = 0;
                            _state.LineIdNum = 1;
                            _state.TagLevel = 0;
                            _state.InJisage = -1;
                            int i = 0;
                            while (lineNum < lineNumBak)
                            {
                                if (bookInfo.IsIgnoreLine(lineNum)) { lineNum++; i++; continue; }
                                TextWriter? dest = lineNum <= lastZeroTagLevelLineNum ? orgOut : null;
                                ConvertTextLineToEpub3(dest, preTitleBuf[i++], lineNum, false, false);
                                lineNum++;
                            }
                        }
                        preTitleBuf!.Clear();
                    }

                    // タイトルページの改ページ
                    if (bookInfo.TitleEndLine + 1 == lineNum)
                    {
                        if (_state.TagLevel > 0)
                            bookInfo.TitleEndLine++;
                        else
                        {
                            skipTitle = false;
                            output = orgOut;       // ダミーから戻す
                            noImage = false;
                            bookInfo.AddPageBreakLine(bookInfo.TitleEndLine + 1);
                        }
                    }
                }

                // 改ページ指定行なら改ページフラグ設定（タグ内なら次の行へ）
                if (bookInfo.IsPageBreakLine(lineNum) && _state.SectionCharLength > 0)
                {
                    if (_state.TagLevel == 0) _outputService.SetPageBreakTrigger(_pageBreakNormal);
                    else bookInfo.AddPageBreakLine(lineNum + 1);
                }

                // コメントブロック処理（"-----..." で囲まれた範囲）
                if (line.StartsWith("--------------------------------------------------", StringComparison.Ordinal))
                {
                    if (commentPrint)
                    {
                        inComment = !inComment;
                    }
                    else
                    {
                        if (inComment) { inComment = false; }
                        else { inComment = true; }
                        continue;
                    }
                }

                if (inComment)
                {
                    if (commentPrint)
                    {
                        if (!commentConvert)
                        {
                            // コメント内容をそのまま HTML エスケープして出力
                            var buf = new StringBuilder();
                            foreach (char c in line)
                            {
                                buf.Append(c switch
                                {
                                    '&' => "&amp;",
                                    '<' => "&lt;",
                                    '>' => "&gt;",
                                    _ => c.ToString()
                                });
                            }
                            _outputService.PrintLineBuffer(output, buf, lineNum, false);
                            continue;
                        }
                    }
                    else
                    {
                        continue;
                    }
                }

                // 出力しない行を飛ばす
                if (bookInfo.IsIgnoreLine(lineNum)) continue;

                // 特殊行（タイトル・著者等）の出力
                var chukiMap = GetChukiMap();
                if (lineNum == bookInfo.TitleLine)
                {
                    _outputService.PrintLineBuffer(output, new StringBuilder(chukiMap["表題前"][0]), -1, true);
                    ConvertTextLineToEpub3(output, line, lineNum, false, noImage);
                    _outputService.PrintLineBuffer(output, new StringBuilder(chukiMap["表題後"][0]), -1, true);
                }
                else if (lineNum == bookInfo.OrgTitleLine)
                {
                    _outputService.PrintLineBuffer(output, new StringBuilder(chukiMap["原題前"][0]), -1, true);
                    ConvertTextLineToEpub3(output, line, lineNum, false, noImage);
                    _outputService.PrintLineBuffer(output, new StringBuilder(chukiMap["原題後"][0]), -1, true);
                }
                else if (lineNum == bookInfo.SubTitleLine)
                {
                    _outputService.PrintLineBuffer(output, new StringBuilder(chukiMap["副題前"][0]), -1, true);
                    ConvertTextLineToEpub3(output, line, lineNum, false, noImage);
                    _outputService.PrintLineBuffer(output, new StringBuilder(chukiMap["副題後"][0]), -1, true);
                }
                else if (lineNum == bookInfo.SubOrgTitleLine)
                {
                    _outputService.PrintLineBuffer(output, new StringBuilder(chukiMap["副原題前"][0]), -1, true);
                    ConvertTextLineToEpub3(output, line, lineNum, false, noImage);
                    _outputService.PrintLineBuffer(output, new StringBuilder(chukiMap["副原題後"][0]), -1, true);
                }
                else if (lineNum == bookInfo.CreatorLine)
                {
                    _outputService.PrintLineBuffer(output, new StringBuilder(chukiMap["著者前"][0]), -1, true);
                    ConvertTextLineToEpub3(output, line, lineNum, false, noImage);
                    _outputService.PrintLineBuffer(output, new StringBuilder(chukiMap["著者後"][0]), -1, true);
                }
                else if (lineNum == bookInfo.SubCreatorLine)
                {
                    _outputService.PrintLineBuffer(output, new StringBuilder(chukiMap["副著者前"][0]), -1, true);
                    ConvertTextLineToEpub3(output, line, lineNum, false, noImage);
                    _outputService.PrintLineBuffer(output, new StringBuilder(chukiMap["副著者後"][0]), -1, true);
                }
                else
                {
                    ConvertTextLineToEpub3(output, line, lineNum, false, noImage);
                }

                if (_state.Canceled) return;
                // GUI 進捗バーは CLI 版では不要のためスキップ

                if (_state.TagLevel == 0) lastZeroTagLevelLineNum = lineNum;

            } while ((line = src.ReadLine()) != null);
        }
        catch (Exception e)
        {
            LogAppender.Error(lineNum, e.Message);
            throw;
        }
    }

    /// <summary>chukiMap のアクセサ（static フィールドのラッパー）</summary>
    Dictionary<string, string[]> GetChukiMap() => _chukiMap;

    //============================================================
    // Phase 6e: ConvertTitleLineToEpub3, ConvertTextLineToEpub3
    //============================================================

    /// <summary>表題行をXHTMLに変換して返す。Java: convertTitleLineToEpub3</summary>
    public string ConvertTitleLineToEpub3(string line)
    {
        var buf = new StringBuilder();
        char[] ch = line.ToCharArray();
        int charStart = 0;

        Match m = ChukiPattern.Match(line);
        while (m.Success)
        {
            string chukiTag = m.Value;
            string lowerChukiTag = chukiTag.ToLowerInvariant();
            int chukiStart = m.Index;

            if (chukiTag[0] == '＃') { m = m.NextMatch(); continue; }
            if (chukiTag[0] == '<' &&
                !(lowerChukiTag.StartsWith("<img ") || lowerChukiTag.StartsWith("</img>") ||
                  lowerChukiTag.StartsWith("<a ") || lowerChukiTag.StartsWith("</a>")))
            { m = m.NextMatch(); continue; }

            string chukiName = chukiTag[2..^1];
            if (charStart < chukiStart)
                _charService.ConvertEscapedText(buf, ch, charStart, chukiStart);

            if (_chukiKunten.Contains(chukiName) || (vertical && chukiName.StartsWith("縦中横")))
            {
                if (_chukiMap.TryGetValue(chukiName, out var kuntenTags))
                    buf.Append(kuntenTags[0]);
            }
            else
            {
                int imageStartIdx = chukiTag.LastIndexOf('（');
                if (imageStartIdx > -1)
                {
                    if (imageStartIdx == 2 && chukiTag.EndsWith("）］") && chukiTag.IndexOf('.', 2) == -1)
                    {
                        buf.Append(_chukiMap["行右小書き"][0]);
                        buf.Append(chukiTag[3..^2]);
                        buf.Append(_chukiMap["行右小書き終わり"][0]);
                    }
                    else if (chukiTag.IndexOf('.', 2) != -1)
                    {
                        string? srcFilePath = GetImageChukiFileName(chukiTag, imageStartIdx);
                        if (srcFilePath != null && chukiTag.EndsWith("#GAIJI#］"))
                        {
                            string? fileName = _writer.GetImageFilePath(srcFilePath.Trim(), -1);
                            if (fileName != null)
                                buf.AppendFormat(_chukiMap["外字画像"][0], fileName);
                        }
                    }
                }
            }

            charStart = chukiStart + chukiTag.Length;
            m = m.NextMatch();
        }

        if (charStart < ch.Length)
            _charService.ConvertEscapedText(buf, ch, charStart, ch.Length);

        return _rubyService.ConvertRubyText(buf.ToString()).ToString();
    }

    /// <summary>1行をXHTMLに変換して output に書き出す。Java: convertTextLineToEpub3</summary>
    void ConvertTextLineToEpub3(TextWriter? output, string line, int lineNum, bool noBr, bool noImage)
    {
        var buf = new StringBuilder();
        line = _gaijiChukiService.ReplaceChukiSufTag(_gaijiChukiService.ConvertGaijiChuki(line, true));

        // キャプション指定の画像チェック
        if (_state.NextLineIsCaption)
        {
            if (!line.StartsWith("［＃キャプション］") && !line.StartsWith("［＃ここからキャプション］"))
            {
                LogAppender.Warn(lineNum, "画像の次の行にキャプションがありません");
                buf.Append(_chukiMap["画像終わり"][0]);
                buf.Append("\n");
                _state.InImageTag = false;
            }
        }
        _state.NextLineIsCaption = false;

        char[] ch = line.ToCharArray();
        int charStart = 0;

        // 行頭インデント
        if (_settings.ForceIndent && ch.Length > charStart + 1)
        {
            switch (ch[charStart])
            {
                case '　': case '「': case '『': case '（': case '\u201C': case '〈':
                case '【': case '〔': case '［': case '※':
                    break;
                case ' ':
                case '\u00A0':
                    char c1 = ch[charStart + 1];
                    if (c1 == ' ' || c1 == '\u00A0' || c1 == '　') charStart++;
                    ch[charStart] = '　';
                    break;
                default:
                    buf.Append('　');
                    break;
            }
        }

        // 割り注状態
        int wrcStart = 0;
        int wrcBrPos = -1;
        char wrcStartChar = '\0';
        bool inMado = false;
        bool linkStarted = false;

        var bufSuf = new StringBuilder();
        Match m = ChukiPattern.Match(line);
        int chukiStart = 0;

        _state.NoTcyStart.Clear();
        _state.NoTcyEnd.Clear();
        if (_state.InYoko) _state.NoTcyStart.Add(0);

        while (m.Success)
        {
            string chukiTag = m.Value;
            string lowerChukiTag = chukiTag.ToLowerInvariant();
            chukiStart = m.Index;

            if (chukiTag[0] == '＃') { m = m.NextMatch(); continue; }
            if (chukiTag[0] == '<' &&
                !(lowerChukiTag.StartsWith("<img ") || lowerChukiTag.Equals("</img>") ||
                  lowerChukiTag.StartsWith("<a ") || lowerChukiTag.Equals("</a>")))
            { m = m.NextMatch(); continue; }

            string chukiName = chukiTag[2..^1];
            _chukiMap.TryGetValue(chukiName, out string[]? tags);

            // 割り注終わり処理
            if (wrcStart > 0 && chukiName.EndsWith("割り注終わり"))
            {
                char wrcEndChar = '\0';
                if (wrcStartChar != '\0' && chukiStart > 0)
                    wrcEndChar = ch[chukiStart - 1];

                if (charStart <= wrcBrPos && wrcBrPos <= chukiStart)
                {
                    _charService.ConvertEscapedText(buf, ch, charStart, wrcBrPos);
                    buf.Append(_chukiMap["改行"][0]);
                    _charService.ConvertEscapedText(buf, ch, wrcBrPos, chukiStart - (wrcEndChar == '\0' ? 0 : 1));
                    wrcBrPos = -1;
                }
                else
                    _charService.ConvertEscapedText(buf, ch, charStart, chukiStart - (wrcEndChar == '\0' ? 0 : 1));

                if (tags != null) buf.Append(tags[0]);
                if (wrcEndChar != '\0') buf.Append(wrcEndChar);
                if (wrcStart != -1 && chukiStart - wrcStart > 60)
                    LogAppender.Warn(lineNum, "割り注が長すぎます");
                wrcStart = -1;
                wrcStartChar = '\0';
                wrcBrPos = -1;
                charStart = chukiStart + chukiTag.Length;
                m = m.NextMatch();
                continue;
            }

            // 注記の前まで本文出力
            if (charStart < chukiStart)
            {
                if (charStart <= wrcBrPos && wrcBrPos <= chukiStart)
                {
                    _charService.ConvertEscapedText(buf, ch, charStart, wrcBrPos);
                    buf.Append(_chukiMap["改行"][0]);
                    _charService.ConvertEscapedText(buf, ch, wrcBrPos, chukiStart);
                    wrcBrPos = -1;
                }
                else
                    _charService.ConvertEscapedText(buf, ch, charStart, chukiStart);
            }

            // 縦中横抑止
            if (chukiName.EndsWith("横組み")) { _state.InYoko = true; _state.NoTcyStart.Add(buf.Length); }
            else if (_state.InYoko && chukiName.EndsWith("横組み終わり")) { _state.InYoko = false; _state.NoTcyEnd.Add(buf.Length); }
            if (!_state.InYoko)
            {
                if (chukiName.StartsWith("縦中横"))
                {
                    if (chukiName.EndsWith("終わり")) _state.NoTcyEnd.Add(buf.Length);
                    else _state.NoTcyStart.Add(buf.Length);
                }
            }

            if (tags != null)
            {
                bool noTagAppend = false;

                // 改ページ注記
                if (ChukiFlagPageBreak.Contains(chukiName) && !_state.BookInfo!.IsNoPageBreakLine(lineNum))
                {
                    if (_state.InJisage >= 0)
                    {
                        LogAppender.Warn(_state.InJisage, "字下げ注記省略");
                        buf.Append(_chukiMap["字下げ省略"][0]);
                        _state.InJisage = -1;
                    }
                    if (buf.Length > 0)
                    {
                        _outputService.PrintLineBuffer(output, _rubyService.ConvertRubyText(buf.ToString()), lineNum, true);
                        buf.Length = 0;
                    }
                    noBr = true;
                    if (ch.Length > charStart + chukiTag.Length) noBr = false;

                    if (_chukiFlagMiddle.Contains(chukiName))
                        _outputService.SetPageBreakTrigger(_pageBreakMiddle);
                    else if (_chukiFlagBottom.Contains(chukiName))
                        _outputService.SetPageBreakTrigger(_pageBreakBottom);
                    else if (_state.BookInfo!.IsImageSectionLine(lineNum + 1))
                    {
                        if (_writer.GetImageIndex() == _state.BookInfo.CoverImageIndex && _state.BookInfo.InsertCoverPage)
                            _outputService.SetPageBreakTrigger(null);
                        else
                        {
                            _outputService.SetPageBreakTrigger(_pageBreakImageAuto);
                            _pageBreakImageAuto.SrcFileName = _state.BookInfo.GetImageSectionFileName(lineNum + 1);
                            _pageBreakImageAuto.ImagePageType = _writer.GetImagePageType(
                                _state.PageBreakTrigger?.SrcFileName, _state.TagLevel, lineNum, HasImageCaption(chukiTag));
                        }
                    }
                    else
                        _outputService.SetPageBreakTrigger(_pageBreakNormal);
                }
                // 字下げ
                else if (chukiName.EndsWith("字下げ"))
                {
                    if (_state.InJisage >= 0)
                    {
                        LogAppender.Warn(_state.InJisage, "字下げ注記省略");
                        buf.Append(_chukiMap["字下げ省略"][0]);
                        _state.InJisage = -1;
                    }
                    if (tags.Length > 1) _state.InJisage = -1;
                    else _state.InJisage = lineNum;
                }
                else if (chukiName.EndsWith("字下げ終わり"))
                {
                    if (_state.InJisage == -1)
                    {
                        LogAppender.Warn(lineNum, "字下げ終わり重複");
                        noTagAppend = true;
                    }
                    _state.InJisage = -1;
                }
                // 窓見出し（行頭のみ）
                else if (chukiName.StartsWith("窓"))
                {
                    if (!inMado && Regex.Replace(line[..chukiStart], @"［＃[^］]+］", "")
                                       .Replace(" ", "").Replace("　", "").Length > 0)
                    {
                        LogAppender.Warn(lineNum, "行頭のみ対応: " + chukiName);
                        noTagAppend = true;
                    }
                    else
                    {
                        if (inMado && chukiName.EndsWith("終わり")) inMado = false;
                        else inMado = true;
                    }
                }
                // 割り注開始
                else if (chukiName.EndsWith("割り注"))
                {
                    wrcStart = chukiStart + chukiTag.Length;
                    wrcStartChar = '\0';
                    wrcBrPos = -1;

                    int wrcEnd = line.IndexOf("［＃割り注終わり］", wrcStart);
                    if (wrcEnd == -1) wrcEnd = line.IndexOf("［＃ここで割り注終わり］", wrcStart);
                    if (wrcEnd == -1)
                    {
                        LogAppender.Error(lineNum, "割り注終わりなし");
                        wrcStart = -1;
                    }
                    else
                    {
                        if (chukiStart == 0 || (ch[chukiStart - 1] != '〔' && ch[chukiStart - 1] != '（'))
                        {
                            if ((wrcStart < ch.Length && ch[wrcStart] == '〔' && line.IndexOf('〕', wrcStart) == wrcEnd - 1) ||
                                (wrcStart < ch.Length && ch[wrcStart] == '（' && line.IndexOf('）', wrcStart) == wrcEnd - 1))
                            {
                                wrcStartChar = ch[wrcStart];
                                buf.Append(ch[wrcStart]);
                                chukiStart++;
                            }
                        }

                        int wStart = wrcStart + (wrcStartChar == '\0' ? 0 : 1);
                        int wEnd = wrcEnd - (wrcStartChar == '\0' ? 0 : 1);
                        if (wEnd < ch.Length && ch[wEnd] == '。') wEnd--;

                        int blength = 0;
                        bool hasBr = false;
                        bool inChuki = false, inRuby = false;
                        for (int i = wStart; i < wEnd; i++)
                        {
                            if (inChuki) { if (ch[i] == '］') inChuki = false; }
                            else if (i < wEnd - 1 && ch[i] == '［' && ch[i + 1] == '＃')
                            {
                                if (i < wEnd - 4 && ch[i + 2] == '改' && ch[i + 3] == '行' && ch[i + 4] == '］') { hasBr = true; break; }
                                inChuki = true; i++;
                            }
                            else if (inRuby) { if (ch[i] == '》') inRuby = false; }
                            else if (i < wEnd - 1 && ch[i] == '《') inRuby = true;
                            else blength += CharUtils.IsHalf(ch[i]) ? 1 : 2;
                        }

                        if (!hasBr)
                        {
                            int half = (int)Math.Ceiling(blength / 2.0);
                            blength = 0; inChuki = false; inRuby = false;
                            for (int i = wStart; i < wEnd; i++)
                            {
                                if (inChuki) { if (ch[i] == '］') inChuki = false; }
                                else if (i < wEnd - 1 && ch[i] == '［' && ch[i + 1] == '＃') { inChuki = true; i++; }
                                else if (inRuby) { if (ch[i] == '》') inRuby = false; }
                                else if (i < wEnd - 1 && ch[i] == '《') inRuby = true;
                                else
                                {
                                    if (blength >= half) { wrcBrPos = i; break; }
                                    blength += CharUtils.IsHalf(ch[i]) ? 1 : 2;
                                }
                            }
                            if (wrcBrPos > 0 && wrcBrPos < ch.Length && (ch[wrcBrPos] == '、' || ch[wrcBrPos] == '。'))
                                wrcBrPos++;
                        }
                    }
                }
                // キャプション終わり
                else if (chukiName.EndsWith("キャプション終わり"))
                {
                    if (_state.InImageTag)
                    {
                        buf.Append(_chukiMap["画像終わり"][0]);
                        buf.Append("\n");
                        _state.InImageTag = false;
                        noBr = true;
                    }
                }

                // タグ出力
                if (!noTagAppend)
                {
                    buf.Append(tags[0]);
                    if (tags.Length > 1) bufSuf.Insert(0, tags[1]);
                }
                if (_chukiFlagNoBr.Contains(chukiName)) noBr = true;
            }
            else
            {
                // 画像注記
                int imageStartIdx = chukiTag.LastIndexOf('（');
                if (imageStartIdx > -1)
                {
                    if (imageStartIdx == 2 && chukiTag.EndsWith("）］") && chukiTag.IndexOf('.', 2) == -1)
                    {
                        buf.Append(_chukiMap["行右小書き"][0]);
                        buf.Append(chukiTag[3..^2]);
                        buf.Append(_chukiMap["行右小書き終わり"][0]);
                    }
                    else if (chukiTag.IndexOf('.', 2) == -1)
                    {
                        LogAppender.Warn(lineNum, "注記未変換", chukiTag);
                    }
                    else
                    {
                        if (!noImage)
                        {
                            string? srcFilePath = GetImageChukiFileName(chukiTag, imageStartIdx);
                            if (srcFilePath == null)
                                LogAppender.Error(lineNum, "注記エラー", chukiTag);
                            else
                            {
                                srcFilePath = srcFilePath.Trim();
                                // 外字のすぐ後ろがルビならルビ開始文字チェック
                                if (ch.Length - 1 > chukiStart + chukiTag.Length &&
                                    ch[chukiStart + chukiTag.Length] == '《')
                                {
                                    bool hasRubyStart = false;
                                    for (int i = chukiStart - 1; i >= 0; i--)
                                    {
                                        if (ch[i] == '｜') { hasRubyStart = true; break; }
                                        if (ch[i] == '》') break;
                                    }
                                    if (!hasRubyStart)
                                    {
                                        if (!chukiTag.EndsWith("#GAIJI#］")) LogAppender.Warn(lineNum, "画像にルビ", srcFilePath);
                                        buf.Append('｜');
                                    }
                                }
                                if (chukiTag.EndsWith("#GAIJI#］"))
                                {
                                    string? imgFileName = _writer.GetImageFilePath(srcFilePath, lineNum);
                                    if (imgFileName != null)
                                    {
                                        buf.AppendFormat(_chukiMap["外字画像"][0], imgFileName);
                                        LogAppender.Warn(lineNum, "外字画像利用", srcFilePath);
                                    }
                                }
                                else
                                {
                                    if (_settings.NoIllust && !_writer.IsCoverImage())
                                        LogAppender.Warn(lineNum, "挿絵除外", chukiTag);
                                    else
                                    {
                                        string? dstFileName = _writer.GetImageFilePath(srcFilePath, lineNum);
                                        if (dstFileName != null)
                                        {
                                            if (_state.BookInfo!.IsImageSectionLine(lineNum)) noBr = true;
                                            if (_imageService.PrintImageChuki(output, buf, srcFilePath, dstFileName,
                                                HasImageCaption(chukiTag), lineNum)) noBr = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else if (lowerChukiTag.StartsWith("<img"))
                {
                    if (_settings.NoIllust && !_writer.IsCoverImage())
                        LogAppender.Warn(lineNum, "挿絵除外", chukiTag);
                    else if (!noImage)
                    {
                        string? srcFilePath = GetTagAttr(chukiTag, "src");
                        if (srcFilePath == null)
                            LogAppender.Error(lineNum, "画像注記エラー", chukiTag);
                        else
                        {
                            string? dstFileName = _writer.GetImageFilePath(srcFilePath.Trim(), lineNum);
                            if (dstFileName != null)
                            {
                                if (_state.BookInfo!.IsImageSectionLine(lineNum)) noBr = true;
                                if (_imageService.PrintImageChuki(output, buf, srcFilePath, dstFileName,
                                    HasImageCaption(chukiTag), lineNum)) noBr = true;
                            }
                        }
                    }
                }
                else if (lowerChukiTag.StartsWith("<a"))
                {
                    if (linkStarted) buf.Append("</a>");
                    string? href = GetTagAttr(chukiTag, "href");
                    if (href != null && href.StartsWith("http"))
                    {
                        buf.Append(chukiTag.Replace("&", "&amp;"));
                        linkStarted = true;
                    }
                    else linkStarted = false;
                }
                else if (lowerChukiTag.Equals("</a>"))
                {
                    if (linkStarted) buf.Append(chukiTag);
                }
                else
                {
                    // インデント字下げパターン
                    bool patternMatched = false;
                    Match m2 = _chukiPatternMap["折り返し"].Match(chukiTag);
                    if (m2.Success)
                    {
                        if (_state.InJisage >= 0) { LogAppender.Warn(_state.InJisage, "字下げ注記省略"); buf.Append(_chukiMap["字下げ省略"][0]); }
                        _state.InJisage = lineNum;
                        int arg0 = int.Parse(CharUtils.FullToHalf(m2.Groups[1].Value));
                        int arg1 = int.Parse(CharUtils.FullToHalf(m2.Groups[2].Value));
                        buf.Append(_chukiMap["折り返し1"][0] + arg1);
                        buf.Append(_chukiMap["折り返し2"][0] + (arg0 - arg1));
                        buf.Append(_chukiMap["折り返し3"][0]);
                        noBr = true;
                        patternMatched = true;
                    }
                    if (!patternMatched)
                    {
                        m2 = _chukiPatternMap["字下げ字詰め"].Match(chukiTag);
                        if (m2.Success)
                        {
                            if (_state.InJisage >= 0) { LogAppender.Warn(_state.InJisage, "字下げ注記省略"); buf.Append(_chukiMap["字下げ省略"][0]); }
                            _state.InJisage = lineNum;
                            int arg0 = int.Parse(CharUtils.FullToHalf(m2.Groups[1].Value));
                            int arg1 = int.Parse(CharUtils.FullToHalf(m2.Groups[2].Value));
                            buf.Append(_chukiMap["字下げ字詰め1"][0] + arg0);
                            buf.Append(_chukiMap["字下げ字詰め2"][0] + arg1);
                            buf.Append(_chukiMap["字下げ字詰め3"][0]);
                            noBr = true;
                            patternMatched = true;
                        }
                    }
                    if (!patternMatched)
                    {
                        m2 = _chukiPatternMap["字下げ複合"].Match(chukiTag);
                        if (m2.Success)
                        {
                            if (_state.InJisage >= 0) { LogAppender.Warn(_state.InJisage, "字下げ注記省略"); buf.Append(_chukiMap["字下げ省略"][0]); }
                            _state.InJisage = lineNum;
                            int arg0 = int.Parse(CharUtils.FullToHalf(m2.Groups[1].Value));
                            buf.Append(_chukiMap["字下げ複合1"][0] + arg0);
                            if (chukiTag.Contains("破線罫囲み")) buf.Append(" ").Append(_chukiMap["字下げ破線罫囲み"][0]);
                            else if (chukiTag.Contains("罫囲み")) buf.Append(" ").Append(_chukiMap["字下げ罫囲み"][0]);
                            if (chukiTag.Contains("破線枠囲み")) buf.Append(" ").Append(_chukiMap["字下げ破線枠囲み"][0]);
                            else if (chukiTag.Contains("枠囲み")) buf.Append(" ").Append(_chukiMap["字下げ枠囲み"][0]);
                            if (chukiTag.Contains("中央揃え")) buf.Append(" ").Append(_chukiMap["字下げ中央揃え"][0]);
                            if (chukiTag.Contains("横書き")) buf.Append(" ").Append(_chukiMap["字下げ横書き"][0]);
                            buf.Append(_chukiMap["字下げ複合2"][0]);
                            noBr = true;
                            patternMatched = true;
                        }
                    }
                    if (!patternMatched)
                    {
                        m2 = _chukiPatternMap["字下げ終わり複合"].Match(chukiTag);
                        if (m2.Success)
                        {
                            if (_state.InJisage == -1) LogAppender.Error(lineNum, "字下げ注記エラー");
                            else buf.Append(_chukiMap["ここで字下げ終わり"][0]);
                            _state.InJisage = -1;
                            noBr = true;
                            patternMatched = true;
                        }
                    }
                    if (!patternMatched)
                    {
                        if (!chukiTag.Contains("底本では") && !chukiTag.Contains("に「ママ」") && !chukiTag.Contains("」はママ"))
                            LogAppender.Warn(lineNum, "注記未変換", chukiTag);
                    }
                }
            }
            charStart = chukiStart + chukiTag.Length;
            m = m.NextMatch();
        }

        // 残りの文字出力
        if (charStart < ch.Length)
            _charService.ConvertEscapedText(buf, ch, charStart, ch.Length);

        // 行末タグ追加
        if (bufSuf.Length > 0) buf.Append(bufSuf);

        // 底本：チェック → 改ページ
        if (_settings.SeparateColophon && _state.SectionCharLength > 0 && buf.Length > 2 &&
            buf[0] == '底' && buf[1] == '本' && buf[2] == '：')
        {
            if (_state.InJisage >= 0) LogAppender.Error(_state.InJisage, "字下げ注記エラー");
            else _outputService.SetPageBreakTrigger(_pageBreakNoChapter);
        }

        _outputService.PrintLineBuffer(output, _rubyService.ConvertRubyText(buf.ToString()), lineNum, noBr || _state.InImageTag);
    }

    //============================================================
    // Phase 6f: PrintLineBuffer, ConvertEscapedText, ConvertRubyText,
    //           ConvertTcyText, ConvertReplacedChar, helpers
    //============================================================

    /// <summary>ルビ変換 外部呼び出し用。Java: convertTcyText(String)</summary>
    public string ConvertTcyText(string text) => _tcyService.ConvertTcyText(text);


}
