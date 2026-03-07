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
    bool _userAlterCharEscape = false;
    bool _autoYoko = true;
    bool _autoYokoNum1 = true;
    bool _autoYokoNum3 = true;
    bool _autoYokoEQ1 = true;
    bool _autoYokoEQ3 = true;
    bool _autoAlpha2 = false;
    bool _autoAlphaNum2 = false;
    bool _chukiRuby = false;
    bool _chukiKogaki = false;
    bool _noIllust = false;
    bool _imageFloatPage = false;
    bool _imageFloatBlock = false;
    bool _withMarkId = false;
    public bool commentPrint = false;
    public bool commentConvert = false;
    bool _chapterSection = true;
    int _dakutenType = 1;
    bool _printIvsBMP = false;
    bool _printIvsSSP = true;
    bool _separateColophon = true;
    int _spaceHyphenation = 0;
    int _removeEmptyLine = 0;
    int _maxEmptyLine = int.MaxValue;
    bool _forceIndent = false;
    bool _removeHeadSpace = false;
    bool _forcePageBreak = false;
    int _forcePageBreakSize = 0;
    int _forcePageBreakEmptyLine = 0;
    int _forcePageBreakEmptySize = 0;
    int _forcePageBreakChapterLevel = 0;
    int _forcePageBreakChapterSize = 0;

    //---------------- Chapter Properties ----------------//
    bool _autoChapterName = false;
    bool _autoChapterNumOnly = false;
    bool _autoChapterNumTitle = false;
    bool _autoChapterNumParen = false;
    bool _autoChapterNumParenTitle = false;
    bool _excludeSeqencialChapter = true;
    bool _useNextLineChapterName = true;
    int _maxChapterNameLength = 64;
    Regex? _chapterPattern;
    bool _canceled = false;

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
    Dictionary<string, int>? _chapterChukiMap = null;

    //---------------- Flags Variables ----------------//
    int _inJisage = -1;
    bool _inYoko = false;
    readonly HashSet<int> _noTcyStart = new();
    readonly HashSet<int> _noTcyEnd = new();

    //---------------- Patterns ----------------//
    static readonly Regex ChukiPattern = new(@"(［＃.+?］)|(<.+?>)", RegexOptions.Compiled);
    static readonly Regex GaijiChukiPattern = new(@"(※［＃.+?］)|(〔.+?〕)|(／″?＼)", RegexOptions.Compiled);
    static readonly Regex ChukiSufPattern = new(@"［＃「([^］]+)」([^」^］]+)］", RegexOptions.Compiled);
    static readonly Regex ChukiSufPattern2 = new(@"［＃「([^］]+)」([^」^］]*「[^」^］]+」[^」^］]*)］", RegexOptions.Compiled);
    static readonly Regex ChukiLeftPattern = new(@"^［＃(.+?)］", RegexOptions.Compiled);
    static readonly Regex FileNamePattern = new(@"\[(.+?)\]( |　)*(.+?)(\(|（|\.)", RegexOptions.Compiled);
    // ReplaceChukiSufTag の第1パス: ［＃ または ］ にマッチ
    static readonly Regex InnerTagPattern = new(@"(［＃|］)", RegexOptions.Compiled);

    //---------------- Static Conversion Tables ----------------//
    static bool _inited = false;
    static readonly object _initLock = new();

    static readonly Dictionary<string, string[]> _chukiMap = new();
    static readonly HashSet<string> _chukiFlagNoBr = new();
    static readonly HashSet<string> _chukiFlagNoRubyStart = new();
    static readonly HashSet<string> _chukiFlagNoRubyEnd = new();
    public static readonly HashSet<string> ChukiFlagPageBreak = new();
    static readonly HashSet<string> _chukiFlagMiddle = new();
    static readonly HashSet<string> _chukiFlagBottom = new();
    static readonly HashSet<string> _chukiKunten = new();
    static readonly Dictionary<string, string[]> _sufChukiMap = new();
    static readonly Dictionary<string, Regex> _chukiPatternMap = new();
    static Dictionary<char, string>? _replaceMap = null;
    static Dictionary<string, string>? _replace2Map = null;
    static Dictionary<int, string>? _utf16FontMap = null;
    static Dictionary<int, string>? _utf32FontMap = null;
    static Dictionary<string, string>? _ivs16FontMap = null;
    static Dictionary<string, string>? _ivs32FontMap = null;
    static LatinConverter _latinConverter = new();
    static AozoraGaijiConverter _gaijiConverter = new();

    //---------------- Instance ----------------//
    readonly IEpub3Writer _writer;
    BookInfo? _bookInfo;
    public bool vertical;
    public int lineNum;
    int _pageByteSize;
    int _sectionCharLength;
    int _lineIdNum;
    int _tagLevel = 0;
    bool _nextLineIsCaption = false;
    bool _inImageTag = false;

    //---------------- Page Break Types ----------------//
    static readonly PageBreakType _pageBreakNormal = new(true, 0, PageBreakType.IMAGE_PAGE_NONE);
    static readonly PageBreakType _pageBreakMiddle = new(true, PageBreakType.PAGE_MIDDLE, PageBreakType.IMAGE_PAGE_NONE);
    static readonly PageBreakType _pageBreakBottom = new(true, PageBreakType.PAGE_BOTTOM, PageBreakType.IMAGE_PAGE_NONE);
    static readonly PageBreakType _pageBreakImageAuto = new(true, 0, PageBreakType.IMAGE_PAGE_AUTO);
    static readonly PageBreakType _pageBreakImageW = new(true, 0, PageBreakType.IMAGE_PAGE_W);
    static readonly PageBreakType _pageBreakImageH = new(true, 0, PageBreakType.IMAGE_PAGE_H);
    static readonly PageBreakType _pageBreakImageNoFit = new(true, 0, PageBreakType.IMAGE_PAGE_NOFIT);
    static readonly PageBreakType _pageBreakNoChapter = new(true, 0, PageBreakType.IMAGE_PAGE_NONE, true);

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

    public void SetNoIllust(bool noIllust) => _noIllust = noIllust;

    public void SetImageFloat(bool imageFloatPage, bool imageFloatBlock)
    {
        _imageFloatPage = imageFloatPage;
        _imageFloatBlock = imageFloatBlock;
    }

    public void SetWithMarkId(bool withIdSpan) => _withMarkId = withIdSpan;

    public void SetAutoYoko(bool autoYoko, bool autoYokoNum1, bool autoYokoNum3, bool autoYokoEQ1)
    {
        _autoYoko = autoYoko;
        _autoYokoNum1 = autoYokoNum1;
        _autoYokoNum3 = autoYokoNum3;
        _autoYokoEQ1 = autoYokoEQ1;
    }

    public void SetCharOutput(int dakutenType, bool printIvsBMP, bool printIvsSSP)
    {
        _dakutenType = dakutenType;
        _printIvsBMP = printIvsBMP;
        _printIvsSSP = printIvsSSP;
    }

    public void SetCommentPrint(bool commentPrint, bool commentConvert)
    {
        this.commentPrint = commentPrint;
        this.commentConvert = commentConvert;
    }

    public void SetRemoveEmptyLine(int removeEmptyLine, int maxEmptyLine)
    {
        _removeEmptyLine = removeEmptyLine;
        _maxEmptyLine = maxEmptyLine == 0 ? int.MaxValue : maxEmptyLine;
    }

    public void SetForceIndent(bool forceIndent) => _forceIndent = forceIndent;

    public void SetChapterLevel(
        int maxLength, bool excludeSeqencialChapter, bool useNextLineChapterName,
        bool section, bool h, bool h1, bool h2, bool h3,
        bool userSameLineChapter,
        bool chapterName, bool autoChapterNumOnly, bool autoChapterNumTitle,
        bool autoChapterNumParen, bool autoChapterNumParenTitle,
        string chapterPattern)
    {
        _maxChapterNameLength = maxLength;
        _chapterSection = section;

        if (_chapterChukiMap == null) _chapterChukiMap = new Dictionary<string, int>();
        else _chapterChukiMap.Clear();

        if (h)
        {
            _chapterChukiMap["ここから見出し"] = ChapterLineInfo.TYPE_CHUKI_H;
            _chapterChukiMap["見出し"] = ChapterLineInfo.TYPE_CHUKI_H;
            if (userSameLineChapter) _chapterChukiMap["同行見出し"] = ChapterLineInfo.TYPE_CHUKI_H;
        }
        if (h1)
        {
            _chapterChukiMap["ここから大見出し"] = ChapterLineInfo.TYPE_CHUKI_H1;
            _chapterChukiMap["大見出し"] = ChapterLineInfo.TYPE_CHUKI_H1;
            if (userSameLineChapter) _chapterChukiMap["同行大見出し"] = ChapterLineInfo.TYPE_CHUKI_H1;
        }
        if (h2)
        {
            _chapterChukiMap["ここから中見出し"] = ChapterLineInfo.TYPE_CHUKI_H2;
            _chapterChukiMap["中見出し"] = ChapterLineInfo.TYPE_CHUKI_H2;
            if (userSameLineChapter) _chapterChukiMap["同行中見出し"] = ChapterLineInfo.TYPE_CHUKI_H2;
        }
        if (h3)
        {
            _chapterChukiMap["ここから小見出し"] = ChapterLineInfo.TYPE_CHUKI_H3;
            _chapterChukiMap["小見出し"] = ChapterLineInfo.TYPE_CHUKI_H3;
            if (userSameLineChapter) _chapterChukiMap["同行小見出し"] = ChapterLineInfo.TYPE_CHUKI_H3;
        }

        _useNextLineChapterName = useNextLineChapterName;
        _excludeSeqencialChapter = excludeSeqencialChapter;
        _autoChapterName = chapterName;
        _autoChapterNumOnly = autoChapterNumOnly;
        _autoChapterNumTitle = autoChapterNumTitle;
        _autoChapterNumParen = autoChapterNumParen;
        _autoChapterNumParenTitle = autoChapterNumParenTitle;

        _chapterPattern = null;
        if (!string.IsNullOrEmpty(chapterPattern))
        {
            try { _chapterPattern = new Regex(chapterPattern, RegexOptions.Compiled); }
            catch { LogAppender.Println("[WARN] 目次抽出のその他パターンが正しくありません: " + chapterPattern); }
        }
    }

    public int GetSpaceHyphenation() => _spaceHyphenation;
    public void SetSpaceHyphenation(int type) => _spaceHyphenation = type;

    public void SetChukiRuby(bool chukiRuby, bool chukiKogaki)
    {
        _chukiRuby = chukiRuby;
        _chukiKogaki = chukiKogaki;
    }

    public void SetForcePageBreak(int forcePageBreakSize, int emptyLine, int emptySize, int chapterLevel, int chapterSize)
    {
        _forcePageBreakSize = forcePageBreakSize;
        _forcePageBreakEmptyLine = emptyLine;
        _forcePageBreakEmptySize = emptySize;
        _forcePageBreakChapterLevel = chapterLevel;
        _forcePageBreakChapterSize = chapterSize;

        _forcePageBreak = forcePageBreakSize > 0
            || (emptyLine > 0 && emptySize > 0)
            || (chapterLevel > 0 && chapterSize > 0);

        if (emptyLine > 0) _forcePageBreakSize = Math.Max(_forcePageBreakSize, emptySize);
        if (chapterLevel > 0) _forcePageBreakSize = Math.Max(_forcePageBreakSize, chapterSize);
    }

    /// <summary>変換キャンセル</summary>
    public void Cancel() => _canceled = true;

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

            bool autoChapter = _autoChapterName || _autoChapterNumTitle || _autoChapterNumOnly ||
                               _autoChapterNumParen || _autoChapterNumParenTitle || _chapterPattern != null;
            bool addSectionChapter = true;
            bool addChapterName = false;
            int addNextChapterName = -1;
            ChapterLineInfo? preChapterLineInfo = null;

            while ((line = src.ReadLine()) != null)
            {
                lineNum++;

                // 見出し等の取得のため前方参照注記は変換し外字文字を置換
                line = CharUtils.RemoveSpace(ReplaceChukiSufTag(ConvertGaijiChuki(line, true, false)));
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
                if (!_noIllust) CheckImageOnly(bookInfo, preLines, noRubyLine, lineNum);

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
                            if (_useNextLineChapterName) addNextChapterName = lineNum + 1;
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
                    else if (_chapterChukiMap != null && _chapterChukiMap.TryGetValue(chukiName, out int chapterType))
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
                            if (_useNextLineChapterName) addNextChapterName = lineNum + 1;
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
                    if (_chapterPattern != null)
                    {
                        if (_chapterPattern.IsMatch(noChukiLine))
                        {
                            bookInfo.AddChapterLineInfo(new ChapterLineInfo(lineNum, ChapterLineInfo.TYPE_PATTERN,
                                addSectionChapter, ChapterLineInfo.GetLevel(ChapterLineInfo.TYPE_PATTERN),
                                lastEmptyLine == lineNum - 1, GetChapterName(noRubyLine)));
                            if (_useNextLineChapterName) addNextChapterName = lineNum + 1;
                            addSectionChapter = false;
                        }
                    }

                    if (_autoChapterName)
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
                            if (_useNextLineChapterName) addNextChapterName = lineNum + 1;
                            addChapterName = false;
                            addSectionChapter = false;
                        }
                    }

                    if (_autoChapterNumOnly || _autoChapterNumTitle)
                    {
                        int idx = 0;
                        while (noChukiLineLength > idx && IsChapterNum(noChukiLine[idx])) idx++;
                        if (idx > 0)
                        {
                            if ((_autoChapterNumOnly && noChukiLine.Length == idx) ||
                                (_autoChapterNumTitle && noChukiLine.Length > idx && IsChapterSeparator(noChukiLine[idx])))
                            {
                                bookInfo.AddChapterLineInfo(new ChapterLineInfo(lineNum, ChapterLineInfo.TYPE_CHAPTER_NUM,
                                    addSectionChapter, ChapterLineInfo.GetLevel(ChapterLineInfo.TYPE_CHAPTER_NUM),
                                    lastEmptyLine == lineNum - 1, GetChapterName(noRubyLine)));
                                if (_useNextLineChapterName) addNextChapterName = lineNum + 1;
                                addChapterName = false;
                                addSectionChapter = false;
                            }
                        }
                    }

                    if (_autoChapterNumParen || _autoChapterNumParenTitle)
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
                                    if ((_autoChapterNumParen && noChukiLine.Length == idx) ||
                                        (_autoChapterNumParenTitle && noChukiLine.Length > idx && IsChapterSeparator(noChukiLine[idx])))
                                    {
                                        bookInfo.AddChapterLineInfo(new ChapterLineInfo(lineNum, ChapterLineInfo.TYPE_CHAPTER_NUM,
                                            addSectionChapter, 13, lastEmptyLine == lineNum - 1,
                                            GetChapterName(noRubyLine)));
                                        if (_useNextLineChapterName) addNextChapterName = lineNum + 1;
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
                if (_chapterSection && addSectionChapter)
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
                            if (_useNextLineChapterName) addNextChapterName = lineNum + 1;
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
            if (_excludeSeqencialChapter) bookInfo.ExcludeTocChapter();

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
        CharUtils.GetChapterName(line, _maxChapterNameLength);

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

    /// <summary>改ページ処理があった場合、画像のみページの画像行をbookInfoに追加</summary>
    private void CheckImageOnly(BookInfo bookInfo, string?[] preLines, string line, int lineNum)
    {
        if (preLines[0] == null) return;
        int bracketIdx = line.IndexOf('］');
        if (bracketIdx <= 3) return;

        string curChuki = line[2..bracketIdx];
        if (!ChukiFlagPageBreak.Contains(curChuki)) return;

        // 2行前の行末が改ページまたは現在行が先頭から2行目
        bool prevIsPageBreak = preLines[1] == null;
        if (!prevIsPageBreak)
        {
            int prev1BracketIdx = preLines[1]!.IndexOf('］');
            if (prev1BracketIdx > 3)
            {
                int sharpIdx = preLines[1]!.LastIndexOf('＃') + 1;
                if (sharpIdx > 0 && sharpIdx < preLines[1]!.Length - 1)
                {
                    string prev1Chuki = preLines[1]![sharpIdx..(preLines[1]!.Length - 1)];
                    prevIsPageBreak = ChukiFlagPageBreak.Contains(prev1Chuki);
                }
            }
        }
        if (!prevIsPageBreak) return;

        // 1行前が画像のみの行か確認（preLines[0] は先頭でnullチェック済み）
        string prev0 = preLines[0]!;
        string prev0Lower = prev0.ToLower();
        bool isImageLine =
            (prev0.StartsWith("［＃") &&
             Regex.IsMatch(prev0, @"^［＃.*（.+\..+") &&
             prev0.IndexOf('］') == prev0.Length - 1) ||
            (prev0Lower.StartsWith("<img") &&
             prev0.IndexOf('>') == prev0.Length - 1);

        if (!isImageLine) return;

        string? fileName;
        if (prev0Lower.StartsWith("<img"))
            fileName = GetTagAttr(prev0, "src");
        else
            fileName = GetImageChukiFileName(prev0, prev0.IndexOf('（'));

        bookInfo.AddImageSectionLine(lineNum - 1, fileName ?? "");
    }

    /// <summary>タグからattr属性値を取得</summary>
    public string? GetTagAttr(string tag, string attr)
    {
        string lowerTag = tag.ToLower();
        int srcIdx = lowerTag.IndexOf(" " + attr + "=");
        if (srcIdx == -1) return null;
        int start = srcIdx + attr.Length + 2;
        if (start >= lowerTag.Length) return null;
        int end = -1;
        if (lowerTag[start] == '"') end = lowerTag.IndexOf('"', start + 1);
        else if (lowerTag[start] == '\'') end = lowerTag.IndexOf('\'', start + 1);
        if (end == -1) { end = lowerTag.IndexOf('>', start); start--; }
        if (end == -1) { end = lowerTag.IndexOf(' ', start); start--; }
        if (end != -1 && start + 1 <= end) return tag[(start + 1)..end].Trim();
        return null;
    }

    /// <summary>画像注記にキャプション付きの指定がある場合true</summary>
    public bool HasImageCaption(string chukiTag) =>
        chukiTag.IndexOf("キャプション付き") > 0;

    /// <summary>画像注記からファイル名取得</summary>
    /// <param name="chukiTag">注記全体</param>
    /// <param name="startIdx">画像注記の'（'の位置</param>
    public string? GetImageChukiFileName(string chukiTag, int startIdx)
    {
        if (startIdx < 0 || startIdx >= chukiTag.Length) return null;
        int endIdx = chukiTag.IndexOf('、', startIdx + 1);
        int closeIdx = chukiTag.IndexOf('）', startIdx + 1);
        if (closeIdx == -1)
        {
            // '）' なし: '、' のみ使用
        }
        else if (endIdx == -1)
        {
            endIdx = closeIdx;
        }
        else
        {
            endIdx = Math.Min(endIdx, closeIdx);
        }
        if (endIdx > startIdx && endIdx >= 0) return chukiTag[(startIdx + 1)..endIdx];
        return null;
    }

    //============================================================
    // Phase 6d: ConvertGaijiChuki / HasInnerChuki /
    //           ReplaceChukiSufTag / GetTargetStart
    //============================================================

    /// <summary>
    /// 外字注記を UTF-8 文字または代替文字列に変換する。
    /// <para>Java: convertGaijiChuki(line, escape, logged)</para>
    /// </summary>
    /// <param name="escape">
    ///   true の場合、変換後に ※《》｜＃ 等の特殊文字が現れたら前に ※ を付加する。
    /// </param>
    /// <param name="logged">
    ///   true の場合、変換できない外字注記を警告ログに出力する。
    /// </param>
    private string ConvertGaijiChuki(string line, bool escape, bool logged = true)
    {
        Match match = GaijiChukiPattern.Match(line);
        if (!match.Success) return line;

        var buf = new StringBuilder();
        int begin = 0;

        do
        {
            string chuki = match.Value;
            int chukiStart = match.Index;

            buf.Append(line, begin, chukiStart - begin);

            if (chuki[0] == '※')
            {
                // 外字注記: ※［＃「...」, ...］
                // chuki[0]='※' chuki[1]='［' chuki[2]='＃' ... chuki[^1]='］'
                string chukiInner = chuki[3..^1];
                string? gaiji = null;

                // U+のコードのみの注記
                if (chukiInner.StartsWith("U+", StringComparison.OrdinalIgnoreCase))
                {
                    gaiji = _gaijiConverter.CodeToCharString(chukiInner);
                    if (gaiji != null)
                    {
                        buf.Append(gaiji);
                        begin = chukiStart + chuki.Length;
                        match = match.NextMatch();
                        continue;
                    }
                }

                // 、の後ろにコードがある場合
                string[] chukiValues = chukiInner.Split('、');

                // 注記文字グリフ or 代替文字変換
                gaiji = _gaijiConverter.ToAlterString(chukiValues[0]);

                // 注記内なら注記タグは除外する
                if (gaiji != null && HasInnerChuki(line, match.Index))
                    gaiji = ChukiPattern.Replace(gaiji, "");

                // コード変換 (chukiValues[3])
                if (gaiji == null && chukiValues.Length > 3)
                    gaiji = _gaijiConverter.CodeToCharString(chukiValues[3]);
                // コード変換 (chukiValues[2])
                if (gaiji == null && chukiValues.Length > 2)
                    gaiji = _gaijiConverter.CodeToCharString(chukiValues[2]);
                // コード変換 (chukiValues[1])
                if (gaiji == null && chukiValues.Length > 1)
                    gaiji = _gaijiConverter.CodeToCharString(chukiValues[1]);
                // 注記名称で変換
                if (gaiji == null)
                    gaiji = _gaijiConverter.ToUtf(chukiValues[0]);

                if (gaiji != null)
                {
                    // 特殊文字は前に ※ を付けて文字出力時に例外処理
                    if (gaiji.Length == 1 && escape)
                    {
                        switch (gaiji[0])
                        {
                            case '※': buf.Append('※'); break;
                            case '》': buf.Append('※'); break;
                            case '《': buf.Append('※'); break;
                            case '｜': buf.Append('※'); break;
                            case '＃': buf.Append('※'); break;
                        }
                    }
                    buf.Append(gaiji);
                    begin = chukiStart + chuki.Length;
                    match = match.NextMatch();
                    continue;
                }

                // 変換不可
                if (HasInnerChuki(line, match.Index))
                {
                    gaiji = "〓";
                    LogAppender.Warn(lineNum, "外字注記内に注記があります", chuki);
                }
                else
                {
                    // 画像指定付き外字なら画像注記に変更
                    int imageStartIdx = chuki.IndexOf('（', 2);
                    if (imageStartIdx > -1 && chuki.IndexOf('.', 2) != -1)
                    {
                        // ※を消して内部処理用画像注記に変更: ［＃（ファイル名）#GAIJI#］
                        gaiji = chuki[1..^1] + "#GAIJI#］";
                    }
                    else
                    {
                        if (logged) LogAppender.Warn(lineNum, "外字未変換", chuki);
                        gaiji = "〓［＃行右小書き］（" + chukiValues[0] + "）［＃行右小書き終わり］";
                    }
                }
                buf.Append(gaiji);
            }
            else if (chuki[0] == '〔')
            {
                // 拡張ラテン文字変換: 〔e'tiquette〕
                string inner = chuki[1..^1];
                // 〔の次が半角でなければ〔の中を再度外字変換
                if (!CharUtils.IsHalfSpace(inner.ToCharArray()))
                    buf.Append('〔').Append(ConvertGaijiChuki(inner, true)).Append('〕');
                else
                    buf.Append(_latinConverter.ToLatinString(inner));
            }
            else if (chuki[0] == '／')
            {
                // くの字点: ／＼ → 〳〵、 ／″＼ → 〴〵
                buf.Append(chuki[1] == '″' ? '〴' : '〳');
                buf.Append('〵');
            }

            begin = chukiStart + chuki.Length;
            match = match.NextMatch();
        }
        while (match.Success);

        buf.Append(line, begin, line.Length - begin);
        return buf.ToString();
    }

    /// <summary>
    /// gaijiStart の位置より前で注記 ［＃ が閉じられていないかをチェック。
    /// 閉じていなければ true（= 外字が注記内にある）。
    /// </summary>
    private bool HasInnerChuki(string line, int gaijiStart)
    {
        int chukiStartCount = 0;
        int end = gaijiStart;
        while (end > 0)
        {
            end = line.LastIndexOf("［＃", end - 1);
            if (end == -1) break;
            chukiStartCount++;
        }

        int chukiEndCount = 0;
        end = gaijiStart;
        while (end > 0)
        {
            end = line.LastIndexOf('］', end - 1);
            if (end == -1) break;
            chukiEndCount++;
        }

        return chukiStartCount > chukiEndCount;
    }

    /// <summary>
    /// 前方参照注記（「○○」の××）をインライン注記（前後タグ）に変換する。
    /// <para>Java: replaceChukiSufTag(line)</para>
    /// </summary>
    private string ReplaceChukiSufTag(string line)
    {
        if (line.IndexOf("［＃「") == -1) return line;

        // --- 第1パス: 注記内注記を除外 ---
        var buf = new StringBuilder();
        int mTagEnd = 0;
        int innerTagLevel = 0;
        int innerTagStart = 0;

        foreach (Match mTag in InnerTagPattern.Matches(line))
        {
            if (innerTagLevel <= 1) buf.Append(line, mTagEnd, mTag.Index - mTagEnd);
            mTagEnd = mTag.Index + mTag.Length;
            string tag = mTag.Value;

            if (tag == "］")
            {
                if (innerTagLevel <= 1) buf.Append(tag);
                else if (innerTagLevel == 2)
                    LogAppender.Warn(lineNum, "注記内に注記があります", line[innerTagStart..mTagEnd]);
                innerTagLevel--;
            }
            else
            {
                innerTagLevel++;
                if (innerTagLevel <= 1) buf.Append(tag);
                else if (innerTagLevel == 2) innerTagStart = mTag.Index;
            }
        }
        buf.Append(line, mTagEnd, line.Length - mTagEnd);
        line = buf.ToString();

        // --- 第2パス: ［＃「target」chuki］ → 前後タグ ---
        Match m = ChukiSufPattern.Match(line);
        if (!m.Success) return line;

        int chOffset = 0;
        buf = new StringBuilder(line);
        do
        {
            string target = m.Groups[1].Value;
            string chuki = m.Groups[2].Value;
            string[]? tags = _sufChukiMap.TryGetValue(chuki, out var t) ? t : null;
            int chukiTagStart = m.Index;
            int chukiTagEnd = m.Index + m.Length;

            // 後ろにルビがあったら前に移動して位置を調整
            int bufChukiEnd = chukiTagEnd + chOffset;
            if (chukiTagEnd < line.Length && bufChukiEnd < buf.Length && buf[bufChukiEnd] == '《')
            {
                int rubyEnd = buf.ToString().IndexOf('》', bufChukiEnd + 2);
                if (rubyEnd != -1)
                {
                    string ruby = buf.ToString(bufChukiEnd, rubyEnd + 1 - bufChukiEnd);
                    buf.Remove(bufChukiEnd, ruby.Length);
                    buf.Insert(chukiTagStart + chOffset, ruby);
                    chukiTagStart += ruby.Length;
                    chukiTagEnd += ruby.Length;
                    LogAppender.Warn(lineNum, "ルビが注記の後ろにあります", ruby);
                }
            }

            if (chuki.EndsWith("の注記付き終わり"))
            {
                // ［＃注記付き］○○［＃「××」の注記付き終わり］ の例外処理
                buf.Remove(chukiTagStart + chOffset, chukiTagEnd - chukiTagStart);
                buf.Insert(chukiTagStart + chOffset, "《" + target + "》");
                // 前にある ［＃注記付き］ を ｜ に置換
                int start = buf.ToString().LastIndexOf("［＃注記付き］", chukiTagStart + chOffset);
                if (start != -1)
                {
                    buf.Remove(start + 1, 6); // ［＃注記付き］(7chars) → ｜(1char): delete [+1..+7)
                    buf[start] = '｜';
                    chOffset -= 6;
                }
                chOffset += target.Length + 2 - (chukiTagEnd - chukiTagStart);
            }
            else if (tags != null)
            {
                // 前後タグに展開
                int targetStart = GetTargetStart(buf, chukiTagStart, chOffset, CharUtils.RemoveRuby(target).Length);
                buf.Remove(chukiTagStart + chOffset, chukiTagEnd - chukiTagStart);
                buf.Insert(chukiTagStart + chOffset, "［＃" + tags[1] + "］");
                buf.Insert(targetStart, "［＃" + tags[0] + "］");
                chOffset += tags[0].Length + tags[1].Length + 6 - (chukiTagEnd - chukiTagStart);
            }

            m = m.NextMatch();
        }
        while (m.Success);

        // --- 第3パス: ［＃「target」に「rt」のルビ/注記］ ---
        line = buf.ToString();
        m = ChukiSufPattern2.Match(line);
        if (!m.Success) return line;

        chOffset = 0;
        do
        {
            string target = m.Groups[1].Value;
            string chuki = m.Groups[2].Value;
            string[]? tags = _sufChukiMap.TryGetValue(chuki, out var t2) ? t2 : null;
            int targetLength = target.Length;
            int chukiTagStart = m.Index;
            int chukiTagEnd = m.Index + m.Length;

            if (tags == null)
            {
                if (chuki.EndsWith("のルビ") || (_chukiRuby && chuki.EndsWith("の注記")))
                {
                    // ルビに変換（ママは除外）
                    if (chuki.StartsWith("に「") && !chuki.StartsWith("に「ママ"))
                    {
                        // ［＃「青空文庫」に「あおぞらぶんこ」のルビ］
                        int targetStart = GetTargetStart(buf, chukiTagStart, chOffset, targetLength);
                        buf.Remove(chukiTagStart + chOffset, chukiTagEnd - chukiTagStart);
                        string rt = chuki[(chuki.IndexOf('「') + 1)..chuki.IndexOf('」')];
                        buf.Insert(chukiTagStart + chOffset, "《" + rt + "》");
                        buf.Insert(targetStart, "｜");
                        chOffset += rt.Length + 3 - (chukiTagEnd - chukiTagStart);
                    }
                }
                else if (_chukiKogaki && chuki.EndsWith("の注記"))
                {
                    // 後ろに小書き表示（ママは除外）
                    if (chuki.StartsWith("に「") && !chuki.StartsWith("に「ママ"))
                    {
                        // ［＃「青空文庫」に「あおぞらぶんこ」の注記］
                        buf.Remove(chukiTagStart + chOffset, chukiTagEnd - chukiTagStart);
                        string kogaki = "［＃小書き］" +
                            chuki[(chuki.IndexOf('「') + 1)..chuki.IndexOf('」')] +
                            "［＃小書き終わり］";
                        buf.Insert(chukiTagStart + chOffset, kogaki);
                        chOffset += kogaki.Length - (chukiTagEnd - chukiTagStart);
                    }
                }
            }

            m = m.NextMatch();
        }
        while (m.Success);

        return buf.ToString();
    }

    /// <summary>
    /// 前方参照注記の前タグ挿入位置を取得する。
    /// ルビ・注記タグをスキップしながら targetLength 文字分を後ろから数え、挿入インデックスを返す。
    /// <para>Java: getTargetStart(buf, chukiTagStart, chOffset, targetLength)</para>
    /// </summary>
    private int GetTargetStart(StringBuilder buf, int chukiTagStart, int chOffset, int targetLength)
    {
        int idx = chukiTagStart - 1 + chOffset;
        bool hasRuby = false;
        int length = 0;

        while (targetLength > length && idx >= 0)
        {
            switch (buf[idx])
            {
                case '》':
                    idx--;
                    // エスケープ文字なら1文字としてカウント
                    if (idx >= 0 && CharUtils.IsEscapedChar(buf, idx))
                    {
                        length++;
                        break;
                    }
                    // ルビの《...》をスキップ
                    while (idx >= 0 && buf[idx] != '《' && !CharUtils.IsEscapedChar(buf, idx))
                        idx--;
                    hasRuby = true;
                    break;

                case '］':
                    idx--;
                    // エスケープ文字なら1文字としてカウント
                    if (idx >= 0 && CharUtils.IsEscapedChar(buf, idx))
                    {
                        length++;
                        break;
                    }
                    // 注記タグの ［...］ をスキップ
                    while (idx >= 0 && buf[idx] != '［' && !CharUtils.IsEscapedChar(buf, idx))
                        idx--;
                    break;

                case '｜':
                    // エスケープされた ｜ のみカウント（ルビ区切りの ｜ はスキップ）
                    if (CharUtils.IsEscapedChar(buf, idx))
                        length++;
                    break;

                default:
                    length++;
                    break;
            }
            idx--;
        }

        // ルビがあれば先頭の ｜ を含める
        if (hasRuby && idx >= 0 && buf[idx] == '｜') return idx;
        return idx + 1;
    }

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

        _canceled = false;

        // BookInfo の参照を保持
        _bookInfo = bookInfo;

        string? line;

        // 変換開始時のメンバ変数の初期化
        _pageByteSize = 0;
        _sectionCharLength = 0;
        lineNum = -1;
        _lineIdNum = 1;
        _tagLevel = 0;
        _inJisage = -1;
        // 最初のページの改ページフラグを設定
        SetPageBreakTrigger(_pageBreakNormal);

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
                            _pageByteSize = 0;
                            _sectionCharLength = 0;
                            lineNum = 0;
                            _lineIdNum = 1;
                            _tagLevel = 0;
                            _inJisage = -1;
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
                        if (_tagLevel > 0)
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
                if (bookInfo.IsPageBreakLine(lineNum) && _sectionCharLength > 0)
                {
                    if (_tagLevel == 0) SetPageBreakTrigger(_pageBreakNormal);
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
                            PrintLineBuffer(output, buf, lineNum, false);
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
                    PrintLineBuffer(output, new StringBuilder(chukiMap["表題前"][0]), -1, true);
                    ConvertTextLineToEpub3(output, line, lineNum, false, noImage);
                    PrintLineBuffer(output, new StringBuilder(chukiMap["表題後"][0]), -1, true);
                }
                else if (lineNum == bookInfo.OrgTitleLine)
                {
                    PrintLineBuffer(output, new StringBuilder(chukiMap["原題前"][0]), -1, true);
                    ConvertTextLineToEpub3(output, line, lineNum, false, noImage);
                    PrintLineBuffer(output, new StringBuilder(chukiMap["原題後"][0]), -1, true);
                }
                else if (lineNum == bookInfo.SubTitleLine)
                {
                    PrintLineBuffer(output, new StringBuilder(chukiMap["副題前"][0]), -1, true);
                    ConvertTextLineToEpub3(output, line, lineNum, false, noImage);
                    PrintLineBuffer(output, new StringBuilder(chukiMap["副題後"][0]), -1, true);
                }
                else if (lineNum == bookInfo.SubOrgTitleLine)
                {
                    PrintLineBuffer(output, new StringBuilder(chukiMap["副原題前"][0]), -1, true);
                    ConvertTextLineToEpub3(output, line, lineNum, false, noImage);
                    PrintLineBuffer(output, new StringBuilder(chukiMap["副原題後"][0]), -1, true);
                }
                else if (lineNum == bookInfo.CreatorLine)
                {
                    PrintLineBuffer(output, new StringBuilder(chukiMap["著者前"][0]), -1, true);
                    ConvertTextLineToEpub3(output, line, lineNum, false, noImage);
                    PrintLineBuffer(output, new StringBuilder(chukiMap["著者後"][0]), -1, true);
                }
                else if (lineNum == bookInfo.SubCreatorLine)
                {
                    PrintLineBuffer(output, new StringBuilder(chukiMap["副著者前"][0]), -1, true);
                    ConvertTextLineToEpub3(output, line, lineNum, false, noImage);
                    PrintLineBuffer(output, new StringBuilder(chukiMap["副著者後"][0]), -1, true);
                }
                else
                {
                    ConvertTextLineToEpub3(output, line, lineNum, false, noImage);
                }

                if (_canceled) return;
                // GUI 進捗バーは CLI 版では不要のためスキップ

                if (_tagLevel == 0) lastZeroTagLevelLineNum = lineNum;

            } while ((line = src.ReadLine()) != null);
        }
        catch (Exception e)
        {
            LogAppender.Error(lineNum, e.Message);
            throw;
        }
    }

    //============================================================
    // Phase 6c 補助: SetPageBreakTrigger フィールド + スタブ
    // （実際の処理は Phase 6f PrintLineBuffer と連動）
    //============================================================

    PageBreakType? _pageBreakTrigger = null;
    bool _skipMiddleEmpty = false;
    int _printEmptyLines = 0;
    int _lastChapterLine = -1;

    void SetPageBreakTrigger(PageBreakType? trigger)
    {
        _printEmptyLines = 0;
        _pageBreakTrigger = trigger;
        if (_pageBreakTrigger != null && _pageBreakTrigger.PageType != PageBreakType.PAGE_NONE)
            _skipMiddleEmpty = true;
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
                ConvertEscapedText(buf, ch, charStart, chukiStart);

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
            ConvertEscapedText(buf, ch, charStart, ch.Length);

        return ConvertRubyText(buf.ToString()).ToString();
    }

    /// <summary>1行をXHTMLに変換して output に書き出す。Java: convertTextLineToEpub3</summary>
    void ConvertTextLineToEpub3(TextWriter? output, string line, int lineNum, bool noBr, bool noImage)
    {
        var buf = new StringBuilder();
        line = ReplaceChukiSufTag(ConvertGaijiChuki(line, true));

        // キャプション指定の画像チェック
        if (_nextLineIsCaption)
        {
            if (!line.StartsWith("［＃キャプション］") && !line.StartsWith("［＃ここからキャプション］"))
            {
                LogAppender.Warn(lineNum, "画像の次の行にキャプションがありません");
                buf.Append(_chukiMap["画像終わり"][0]);
                buf.Append("\n");
                _inImageTag = false;
            }
        }
        _nextLineIsCaption = false;

        char[] ch = line.ToCharArray();
        int charStart = 0;

        // 行頭インデント
        if (_forceIndent && ch.Length > charStart + 1)
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

        _noTcyStart.Clear();
        _noTcyEnd.Clear();
        if (_inYoko) _noTcyStart.Add(0);

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
                    ConvertEscapedText(buf, ch, charStart, wrcBrPos);
                    buf.Append(_chukiMap["改行"][0]);
                    ConvertEscapedText(buf, ch, wrcBrPos, chukiStart - (wrcEndChar == '\0' ? 0 : 1));
                    wrcBrPos = -1;
                }
                else
                    ConvertEscapedText(buf, ch, charStart, chukiStart - (wrcEndChar == '\0' ? 0 : 1));

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
                    ConvertEscapedText(buf, ch, charStart, wrcBrPos);
                    buf.Append(_chukiMap["改行"][0]);
                    ConvertEscapedText(buf, ch, wrcBrPos, chukiStart);
                    wrcBrPos = -1;
                }
                else
                    ConvertEscapedText(buf, ch, charStart, chukiStart);
            }

            // 縦中横抑止
            if (chukiName.EndsWith("横組み")) { _inYoko = true; _noTcyStart.Add(buf.Length); }
            else if (_inYoko && chukiName.EndsWith("横組み終わり")) { _inYoko = false; _noTcyEnd.Add(buf.Length); }
            if (!_inYoko)
            {
                if (chukiName.StartsWith("縦中横"))
                {
                    if (chukiName.EndsWith("終わり")) _noTcyEnd.Add(buf.Length);
                    else _noTcyStart.Add(buf.Length);
                }
            }

            if (tags != null)
            {
                bool noTagAppend = false;

                // 改ページ注記
                if (ChukiFlagPageBreak.Contains(chukiName) && !_bookInfo!.IsNoPageBreakLine(lineNum))
                {
                    if (_inJisage >= 0)
                    {
                        LogAppender.Warn(_inJisage, "字下げ注記省略");
                        buf.Append(_chukiMap["字下げ省略"][0]);
                        _inJisage = -1;
                    }
                    if (buf.Length > 0)
                    {
                        PrintLineBuffer(output, ConvertRubyText(buf.ToString()), lineNum, true);
                        buf.Length = 0;
                    }
                    noBr = true;
                    if (ch.Length > charStart + chukiTag.Length) noBr = false;

                    if (_chukiFlagMiddle.Contains(chukiName))
                        SetPageBreakTrigger(_pageBreakMiddle);
                    else if (_chukiFlagBottom.Contains(chukiName))
                        SetPageBreakTrigger(_pageBreakBottom);
                    else if (_bookInfo!.IsImageSectionLine(lineNum + 1))
                    {
                        if (_writer.GetImageIndex() == _bookInfo.CoverImageIndex && _bookInfo.InsertCoverPage)
                            SetPageBreakTrigger(null);
                        else
                        {
                            SetPageBreakTrigger(_pageBreakImageAuto);
                            _pageBreakImageAuto.SrcFileName = _bookInfo.GetImageSectionFileName(lineNum + 1);
                            _pageBreakImageAuto.ImagePageType = _writer.GetImagePageType(
                                _pageBreakTrigger?.SrcFileName, _tagLevel, lineNum, HasImageCaption(chukiTag));
                        }
                    }
                    else
                        SetPageBreakTrigger(_pageBreakNormal);
                }
                // 字下げ
                else if (chukiName.EndsWith("字下げ"))
                {
                    if (_inJisage >= 0)
                    {
                        LogAppender.Warn(_inJisage, "字下げ注記省略");
                        buf.Append(_chukiMap["字下げ省略"][0]);
                        _inJisage = -1;
                    }
                    if (tags.Length > 1) _inJisage = -1;
                    else _inJisage = lineNum;
                }
                else if (chukiName.EndsWith("字下げ終わり"))
                {
                    if (_inJisage == -1)
                    {
                        LogAppender.Warn(lineNum, "字下げ終わり重複");
                        noTagAppend = true;
                    }
                    _inJisage = -1;
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
                    if (_inImageTag)
                    {
                        buf.Append(_chukiMap["画像終わり"][0]);
                        buf.Append("\n");
                        _inImageTag = false;
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
                                    if (_noIllust && !_writer.IsCoverImage())
                                        LogAppender.Warn(lineNum, "挿絵除外", chukiTag);
                                    else
                                    {
                                        string? dstFileName = _writer.GetImageFilePath(srcFilePath, lineNum);
                                        if (dstFileName != null)
                                        {
                                            if (_bookInfo!.IsImageSectionLine(lineNum)) noBr = true;
                                            if (PrintImageChuki(output, buf, srcFilePath, dstFileName,
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
                    if (_noIllust && !_writer.IsCoverImage())
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
                                if (_bookInfo!.IsImageSectionLine(lineNum)) noBr = true;
                                if (PrintImageChuki(output, buf, srcFilePath, dstFileName,
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
                        if (_inJisage >= 0) { LogAppender.Warn(_inJisage, "字下げ注記省略"); buf.Append(_chukiMap["字下げ省略"][0]); }
                        _inJisage = lineNum;
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
                            if (_inJisage >= 0) { LogAppender.Warn(_inJisage, "字下げ注記省略"); buf.Append(_chukiMap["字下げ省略"][0]); }
                            _inJisage = lineNum;
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
                            if (_inJisage >= 0) { LogAppender.Warn(_inJisage, "字下げ注記省略"); buf.Append(_chukiMap["字下げ省略"][0]); }
                            _inJisage = lineNum;
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
                            if (_inJisage == -1) LogAppender.Error(lineNum, "字下げ注記エラー");
                            else buf.Append(_chukiMap["ここで字下げ終わり"][0]);
                            _inJisage = -1;
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
            ConvertEscapedText(buf, ch, charStart, ch.Length);

        // 行末タグ追加
        if (bufSuf.Length > 0) buf.Append(bufSuf);

        // 底本：チェック → 改ページ
        if (_separateColophon && _sectionCharLength > 0 && buf.Length > 2 &&
            buf[0] == '底' && buf[1] == '本' && buf[2] == '：')
        {
            if (_inJisage >= 0) LogAppender.Error(_inJisage, "字下げ注記エラー");
            else SetPageBreakTrigger(_pageBreakNoChapter);
        }

        PrintLineBuffer(output, ConvertRubyText(buf.ToString()), lineNum, noBr || _inImageTag);
    }

    //============================================================
    // Phase 6f: PrintLineBuffer, ConvertEscapedText, ConvertRubyText,
    //           ConvertTcyText, ConvertReplacedChar, helpers
    //============================================================

    /// <summary>注記で分割された文字列単位でエスケープ処理して buf に出力。
    /// Java: convertEscapedText</summary>
    private void ConvertEscapedText(StringBuilder buf, char[] ch, int begin, int end)
    {
        if (begin >= end) return;

        // 先頭 IVS 除去
        if (begin < ch.Length)
        {
            if (ch[begin] == 0xDB40) { begin += 2; LogAppender.Warn(lineNum, "先頭にあるIVSを除去します"); }
            else if (ch[begin] >= 0xFE00 && ch[begin] <= 0xFE0F) { begin++; LogAppender.Warn(lineNum, "先頭にあるIVSを除去します"); }
        }
        if (begin >= end) return;

        // << >> ＜＜ ＞＞ → ※《 ※》
        for (int i = begin + 1; i < end; i++)
        {
            switch (ch[i])
            {
                case '<':
                    if (ch[i - 1] == '<' && (i == begin + 1 || ch[i - 2] != '<') && (end - 1 == i || ch[i + 1] != '<'))
                    { ch[i - 1] = '※'; ch[i] = '《'; }
                    break;
                case '>':
                    if (ch[i - 1] == '>' && (i == begin + 1 || ch[i - 2] != '>') && (end - 1 == i || ch[i + 1] != '>'))
                    { ch[i - 1] = '※'; ch[i] = '》'; }
                    break;
                case '＜':
                    if (ch[i - 1] == '＜' && (i == begin + 1 || ch[i - 2] != '＜') && (end - 1 == i || ch[i + 1] != '＜'))
                    { ch[i - 1] = '※'; ch[i] = '《'; }
                    break;
                case '＞':
                    if (ch[i - 1] == '＞' && (i == begin + 1 || ch[i - 2] != '＞') && (end - 1 == i || ch[i + 1] != '＞'))
                    { ch[i - 1] = '※'; ch[i] = '》'; }
                    break;
                case '゙': ch[i] = '゛'; break;
                case '゚': ch[i] = '゜'; break;
            }
        }

        // NULL 除去 + &<> エスケープ
        for (int idx = begin; idx < end; idx++)
        {
            switch (ch[idx])
            {
                case '\0': break;
                case '&': buf.Append("&amp;"); break;
                case '<': buf.Append("&lt;"); break;
                case '>': buf.Append("&gt;"); break;
                default: buf.Append(ch[idx]); break;
            }
        }
    }

    private enum RubyCharType { Null, Alpha, FullAlpha, Kanji, Hiragana, Katakana }

    /// <summary>ルビタグに変換して出力。Java: convertRubyText</summary>
    private StringBuilder ConvertRubyText(string line)
    {
        var buf = new StringBuilder();
        char[] ch = line.ToCharArray();
        int begin = 0, end = ch.Length;

        int rubyStart = -1;
        int rubyTopStart = -1;
        bool inRuby = false;
        RubyCharType rubyCharType = RubyCharType.Null;

        string rubyStartChuki = _chukiMap.TryGetValue("ルビ開始", out var rsv) ? rsv[0] : "<ruby>";
        string rubyEndChuki = _chukiMap.TryGetValue("ルビ終了", out var rev) ? rev[0] : "</ruby>";

        bool noTcy = false;
        for (int i = begin; i < end; i++)
        {
            if (!noTcy && _noTcyStart.Contains(i)) noTcy = true;
            else if (noTcy && _noTcyEnd.Contains(i)) noTcy = false;

            switch (ch[i])
            {
                case '｜':
                    if (!CharUtils.IsEscapedChar(ch, i))
                    {
                        if (rubyStart != -1) ConvertTcyText(buf, ch, rubyStart, i, noTcy);
                        rubyStart = i + 1;
                        inRuby = true;
                    }
                    break;
                case '《':
                    if (!CharUtils.IsEscapedChar(ch, i))
                    {
                        inRuby = true;
                        rubyTopStart = i;
                    }
                    break;
            }

            if (inRuby)
            {
                if (ch[i] == '》' && !CharUtils.IsEscapedChar(ch, i))
                {
                    if (rubyStart != -1 && rubyTopStart != -1)
                    {
                        // 同じ長さで同じ文字なら1文字ずつルビ
                        if (rubyTopStart - rubyStart == i - rubyTopStart - 1 &&
                            CharUtils.IsSameChars(ch, rubyTopStart + 1, i))
                        {
                            if (!rubyEndChuki.Equals(buf.Length >= rubyEndChuki.Length
                                ? buf.ToString(buf.Length - rubyEndChuki.Length, rubyEndChuki.Length) : ""))
                                buf.Append(rubyStartChuki);
                            else
                                buf.Length -= rubyEndChuki.Length;
                            for (int j = 0; j < rubyTopStart - rubyStart; j++)
                            {
                                ConvertReplacedChar(buf, ch, rubyStart + j, noTcy);
                                buf.Append(_chukiMap.TryGetValue("ルビ前", out var rbf) ? rbf[0] : "<rt>");
                                ConvertReplacedChar(buf, ch, rubyTopStart + 1 + j, true);
                                buf.Append(_chukiMap.TryGetValue("ルビ後", out var rba) ? rba[0] : "</rt>");
                            }
                            buf.Append(rubyEndChuki);
                        }
                        else
                        {
                            if (!rubyEndChuki.Equals(buf.Length >= rubyEndChuki.Length
                                ? buf.ToString(buf.Length - rubyEndChuki.Length, rubyEndChuki.Length) : ""))
                                buf.Append(rubyStartChuki);
                            else
                                buf.Length -= rubyEndChuki.Length;
                            ConvertTcyText(buf, ch, rubyStart, rubyTopStart, noTcy);
                            buf.Append(_chukiMap.TryGetValue("ルビ前", out var rbf2) ? rbf2[0] : "<rt>");
                            ConvertTcyText(buf, ch, rubyTopStart + 1, i, true);
                            buf.Append(_chukiMap.TryGetValue("ルビ後", out var rba2) ? rba2[0] : "</rt>");
                            buf.Append(rubyEndChuki);
                        }
                    }
                    if (rubyStart == -1)
                        LogAppender.Warn(lineNum, "ルビ開始文字無し");
                    inRuby = false;
                    rubyStart = -1;
                    rubyTopStart = -1;
                }
            }
            else
            {
                // ルビ開始位置チェック
                if (rubyStart != -1)
                {
                    bool charTypeChanged = rubyCharType switch
                    {
                        RubyCharType.Alpha => !CharUtils.IsHalfSpace(ch[i]) || ch[i] == '>',
                        RubyCharType.FullAlpha => !(CharUtils.IsFullAlpha(ch[i]) || CharUtils.IsFullNum(ch[i])),
                        RubyCharType.Kanji => !CharUtils.IsKanji(ch, i),
                        RubyCharType.Hiragana => !CharUtils.IsHiragana(ch[i]),
                        RubyCharType.Katakana => !CharUtils.IsKatakana(ch[i]),
                        _ => false
                    };
                    if (charTypeChanged)
                    {
                        ConvertTcyText(buf, ch, rubyStart, i, noTcy);
                        rubyStart = -1; rubyCharType = RubyCharType.Null;
                    }
                }
                if (rubyStart == -1)
                {
                    if (CharUtils.IsKanji(ch, i)) { rubyStart = i; rubyCharType = RubyCharType.Kanji; }
                    else if (CharUtils.IsHiragana(ch[i])) { rubyStart = i; rubyCharType = RubyCharType.Hiragana; }
                    else if (CharUtils.IsKatakana(ch[i])) { rubyStart = i; rubyCharType = RubyCharType.Katakana; }
                    else if (CharUtils.IsHalfSpace(ch[i]) && ch[i] != '>') { rubyStart = i; rubyCharType = RubyCharType.Alpha; }
                    else if (CharUtils.IsFullAlpha(ch[i]) || CharUtils.IsFullNum(ch[i])) { rubyStart = i; rubyCharType = RubyCharType.FullAlpha; }
                    else { ConvertReplacedChar(buf, ch, i, noTcy); rubyCharType = RubyCharType.Null; }
                }
            }
        }
        if (rubyStart != -1)
            ConvertTcyText(buf, ch, rubyStart, end, noTcy);

        return buf;
    }

    /// <summary>ルビ変換 外部呼び出し用。Java: convertTcyText(String)</summary>
    public string ConvertTcyText(string text)
    {
        var buf = new StringBuilder();
        ConvertTcyText(buf, text.ToCharArray(), 0, text.Length, false);
        return buf.ToString();
    }

    /// <summary>1文字外字フォントタグを出力。Java: printGlyphFontTag</summary>
    private bool PrintGlyphFontTag(StringBuilder buf, string gaijiFileName, string className, char baseChar)
    {
        string fullPath = Path.Combine(_writer.GetGaijiFontPath(), gaijiFileName);
        if (!File.Exists(fullPath)) return false;
        _writer.AddGaijiFont(className, fullPath);
        buf.Append("<span class=\"glyph ").Append(className).Append("\">").Append(baseChar).Append("</span>");
        return true;
    }

    /// <summary>縦中横変換して buf に出力。Java: convertTcyText(buf,ch,begin,end,noTcy)</summary>
    private void ConvertTcyText(StringBuilder buf, char[] ch, int begin, int end, bool noTcy)
    {
        for (int i = begin; i < end; i++)
        {
            string? gaijiFileName = null;

            // 4バイト文字（サロゲートペア）
            if (i < end - 1 && char.IsHighSurrogate(ch[i]))
            {
                int code = char.ConvertToUtf32(ch[i], ch[i + 1]);

                // 4バイト文字 + IVS(U+E0100～)
                if (i < end - 3 && ch[i + 2] == 0xDB40)
                {
                    string ivsCode = char.ConvertToUtf32(ch[i + 2], ch[i + 3]).ToString("x");
                    if (_ivs32FontMap != null)
                    {
                        string className = "u" + code.ToString("x") + "-u" + ivsCode;
                        gaijiFileName = _ivs32FontMap.GetValueOrDefault(className);
                        if (gaijiFileName != null && PrintGlyphFontTag(buf, gaijiFileName, className, '〓'))
                        {
                            LogAppender.Warn(lineNum, "外字フォント利用(IVS含む)", "" + ch[i] + ch[i + 1] + ch[i + 2] + ch[i + 3] + "(" + gaijiFileName + ")");
                            i += 3; continue;
                        }
                    }
                    if (_utf32FontMap != null)
                    {
                        gaijiFileName = _utf32FontMap.GetValueOrDefault(code);
                        if (gaijiFileName != null && PrintGlyphFontTag(buf, gaijiFileName, "u" + code.ToString("x"), '〓'))
                        {
                            LogAppender.Warn(lineNum, "外字フォント利用(IVS除外)", "" + ch[i] + ch[i + 1] + "(" + gaijiFileName + ") -" + ivsCode);
                            i += 3; continue;
                        }
                    }
                    if (_printIvsSSP)
                    {
                        if (vertical) buf.Append(_chukiMap["正立"][0]);
                        buf.Append(ch[i]); buf.Append(ch[i + 1]); buf.Append(ch[i + 2]); buf.Append(ch[i + 3]);
                        if (vertical) buf.Append(_chukiMap["正立終わり"][0]);
                        LogAppender.Warn(lineNum, "拡張漢字＋IVSを出力します", "" + ch[i] + ch[i + 1] + ch[i + 2] + ch[i + 3] + "(u+" + code.ToString("x") + "+" + ivsCode + ")");
                    }
                    else
                    {
                        buf.Append(ch[i]); buf.Append(ch[i + 1]);
                        LogAppender.Warn(lineNum, "拡張漢字出力(IVS除外)", "" + ch[i] + ch[i + 1] + "(u+" + code.ToString("x") + ") -" + ivsCode);
                    }
                    i += 3; continue;
                }

                // 4バイト文字 + IVS(U+FE00～)
                if (i < end - 2 && ch[i + 2] >= 0xFE00 && ch[i + 2] <= 0xFE0F)
                {
                    if (_ivs32FontMap != null)
                    {
                        string className = "u" + code.ToString("x") + "-u" + ((int)ch[i + 2]).ToString("x");
                        gaijiFileName = _ivs32FontMap.GetValueOrDefault(className);
                        if (gaijiFileName != null && PrintGlyphFontTag(buf, gaijiFileName, className, '〓'))
                        {
                            LogAppender.Warn(lineNum, "外字フォント利用(IVS含む)", "" + ch[i] + ch[i + 1] + ch[i + 2] + "(" + gaijiFileName + ")");
                            i += 2; continue;
                        }
                    }
                    if (_printIvsBMP)
                    {
                        if (vertical) buf.Append(_chukiMap["正立"][0]);
                        buf.Append(ch[i]); buf.Append(ch[i + 1]); buf.Append(ch[i + 2]);
                        if (vertical) buf.Append(_chukiMap["正立終わり"][0]);
                        LogAppender.Warn(lineNum, "拡張漢字＋IVSを出力します", "" + ch[i] + ch[i + 1] + ch[i + 2] + "(u+" + code.ToString("x") + "+" + ((int)ch[i + 2]).ToString("x") + ")");
                    }
                    else
                    {
                        buf.Append(ch[i]); buf.Append(ch[i + 1]);
                        LogAppender.Warn(lineNum, "拡張漢字出力(IVS除外)", "" + ch[i] + ch[i + 1] + "(u+" + code.ToString("x") + ") -" + ((int)ch[i + 2]).ToString("x") + ")");
                    }
                    i += 2; continue;
                }

                // IVS無し1文字フォントあり
                if (_utf32FontMap != null)
                {
                    gaijiFileName = _utf32FontMap.GetValueOrDefault(code);
                    if (gaijiFileName != null && PrintGlyphFontTag(buf, gaijiFileName, "u" + code.ToString("x"), '〓'))
                    {
                        LogAppender.Warn(lineNum, "外字フォント利用", "" + ch[i] + ch[i + 1] + "(" + gaijiFileName + ")");
                        i++; continue;
                    }
                }

                // 通常4バイト文字
                buf.Append(ch[i]); buf.Append(ch[i + 1]);
                LogAppender.Warn(lineNum, "拡張漢字出力", "" + ch[i] + ch[i + 1] + "(u+" + code.ToString("x") + ")");
                i++; continue;
            }

            // 2バイト文字(U+FFFF以下)

            // 2バイト文字 + IVS(U+E0100～)
            if (i < end - 2 && ch[i + 1] == 0xDB40)
            {
                string ivsCode = char.ConvertToUtf32(ch[i + 1], ch[i + 2]).ToString("x");
                if (_ivs16FontMap != null)
                {
                    string className = "u" + ((int)ch[i]).ToString("x") + "-u" + ivsCode;
                    gaijiFileName = _ivs16FontMap.GetValueOrDefault(className);
                    if (gaijiFileName != null && PrintGlyphFontTag(buf, gaijiFileName, className, '〓'))
                    {
                        LogAppender.Warn(lineNum, "外字フォント利用(IVS含む)", "" + ch[i] + ch[i + 1] + ch[i + 2] + "(" + gaijiFileName + ")");
                        i += 2; continue;
                    }
                }
                if (_utf16FontMap != null && _utf16FontMap.ContainsKey((int)ch[i]))
                {
                    gaijiFileName = _utf16FontMap[(int)ch[i]];
                    if (gaijiFileName != null && PrintGlyphFontTag(buf, gaijiFileName, "u" + ((int)ch[i]).ToString("x"), '〓'))
                    {
                        LogAppender.Warn(lineNum, "外字フォント利用(IVS除外)", "" + ch[i] + "(" + gaijiFileName + ") -" + ivsCode);
                        i += 2; continue;
                    }
                }
                if (_printIvsSSP)
                {
                    if (vertical) buf.Append(_chukiMap["正立"][0]);
                    buf.Append(ch[i]); buf.Append(ch[i + 1]); buf.Append(ch[i + 2]);
                    if (vertical) buf.Append(_chukiMap["正立終わり"][0]);
                    LogAppender.Warn(lineNum, "IVSを出力します", "" + ch[i] + ch[i + 1] + ch[i + 2] + "(u+" + ((int)ch[i]).ToString("x") + "+" + ivsCode + ")");
                }
                else
                {
                    buf.Append(ch[i]);
                    LogAppender.Warn(lineNum, "IVS除外", ch[i] + "(u+" + ((int)ch[i]).ToString("x") + ") -" + ivsCode);
                }
                i += 2; continue;
            }

            // 2バイト文字 + IVS(U+FE00～)
            if (i < end - 1 && ch[i + 1] >= 0xFE00 && ch[i + 1] <= 0xFE0F)
            {
                if (_ivs32FontMap != null)
                {
                    string className = "u" + ((int)ch[i]).ToString("x") + "-u" + ((int)ch[i + 1]).ToString("x");
                    gaijiFileName = _ivs32FontMap.GetValueOrDefault(className);
                    if (gaijiFileName != null && PrintGlyphFontTag(buf, gaijiFileName, className, '〓'))
                    {
                        LogAppender.Warn(lineNum, "外字フォント利用(IVS含む)", "" + ch[i] + "(" + gaijiFileName + ")");
                        i++; continue;
                    }
                }
                if (_utf16FontMap != null && _utf16FontMap.ContainsKey((int)ch[i]))
                {
                    gaijiFileName = _utf16FontMap[(int)ch[i]];
                    if (gaijiFileName != null && PrintGlyphFontTag(buf, gaijiFileName, "u" + ((int)ch[i]).ToString("x"), '〓'))
                    {
                        LogAppender.Warn(lineNum, "外字フォント利用(IVS除外)", "" + ch[i] + "(" + gaijiFileName + ") -" + ((int)ch[i + 1]).ToString("x"));
                        i++; continue;
                    }
                }
                if (_printIvsBMP)
                {
                    buf.Append(ch[i]); buf.Append(ch[i + 1]);
                    LogAppender.Warn(lineNum, "IVSを出力します", "" + ch[i] + ch[i + 1] + "(u+" + ((int)ch[i]).ToString("x") + "+" + ((int)ch[i + 1]).ToString("x") + ")");
                }
                else
                {
                    buf.Append(ch[i]);
                    LogAppender.Warn(lineNum, "IVS除外", ch[i] + "(u+" + ((int)ch[i]).ToString("x") + ") -" + ((int)ch[i + 1]).ToString("x"));
                }
                i++; continue;
            }

            // IVS無し1文字フォント
            if (_utf16FontMap != null && _utf16FontMap.ContainsKey((int)ch[i]))
            {
                gaijiFileName = _utf16FontMap[(int)ch[i]];
                if (gaijiFileName != null && PrintGlyphFontTag(buf, gaijiFileName, "u" + ((int)ch[i]).ToString("x"), '〓'))
                {
                    LogAppender.Warn(lineNum, "外字フォント利用", "" + ch[i] + "(" + gaijiFileName + ")");
                    continue;
                }
            }

            // 自動縦中横
            if (vertical && !(_inYoko || noTcy))
            {
                switch (ch[i])
                {
                    case '0': case '1': case '2': case '3': case '4':
                    case '5': case '6': case '7': case '8': case '9':
                        if (_autoYoko)
                        {
                            if (_autoYokoNum3 && i + 2 < end && CharUtils.IsNum(ch[i + 1]) && CharUtils.IsNum(ch[i + 2]))
                            {
                                if (!CheckTcyPrev(ch, i - 1)) break;
                                if (!CheckTcyNext(ch, i + 3)) break;
                                buf.Append(_chukiMap["縦中横"][0]); buf.Append(ch[i]); buf.Append(ch[i + 1]); buf.Append(ch[i + 2]);
                                buf.Append(_chukiMap["縦中横終わり"][0]); i += 2; continue;
                            }
                            else if (i + 1 < end && CharUtils.IsNum(ch[i + 1]))
                            {
                                if (!CheckTcyPrev(ch, i - 1)) break;
                                if (!CheckTcyNext(ch, i + 2)) break;
                                buf.Append(_chukiMap["縦中横"][0]); buf.Append(ch[i]); buf.Append(ch[i + 1]);
                                buf.Append(_chukiMap["縦中横終わり"][0]); i++; continue;
                            }
                            else if (_autoYokoNum1 && (i == 0 || !CharUtils.IsNum(ch[i - 1])) && (i + 1 == end || !CharUtils.IsNum(ch[i + 1])))
                            {
                                if (!CheckTcyPrev(ch, i - 1)) break;
                                if (!CheckTcyNext(ch, i + 1)) break;
                                buf.Append(_chukiMap["縦中横"][0]); buf.Append(ch[i]); buf.Append(_chukiMap["縦中横終わり"][0]); continue;
                            }
                            // 1月1日 パターン
                            if (i + 3 < ch.Length && ch[i + 1] == '月' && '0' <= ch[i + 2] && ch[i + 2] <= '9' &&
                                (ch[i + 3] == '日' || (i + 4 < ch.Length && '0' <= ch[i + 3] && ch[i + 3] <= '9' && ch[i + 4] == '日')))
                            {
                                buf.Append(_chukiMap["縦中横"][0]); buf.Append(ch[i]); buf.Append(_chukiMap["縦中横終わり"][0]); continue;
                            }
                            if (i > 1 && i + 1 < ch.Length &&
                                (ch[i - 1] == '年' && ch[i + 1] == '月' || ch[i - 1] == '月' && ch[i + 1] == '日' ||
                                 ch[i - 1] == '第' && (ch[i + 1] == '刷' || ch[i + 1] == '版' || ch[i + 1] == '巻')))
                            {
                                buf.Append(_chukiMap["縦中横"][0]); buf.Append(ch[i]); buf.Append(_chukiMap["縦中横終わり"][0]); continue;
                            }
                            if (i > 2 && (ch[i - 2] == '明' && ch[i - 1] == '治' || ch[i - 2] == '大' && ch[i - 1] == '正' ||
                                          ch[i - 2] == '昭' && ch[i - 1] == '和' || ch[i - 2] == '平' && ch[i - 1] == '成'))
                            {
                                buf.Append(_chukiMap["縦中横"][0]); buf.Append(ch[i]); buf.Append(_chukiMap["縦中横終わり"][0]); continue;
                            }
                        }
                        break;

                    case '!': case '?':
                        if (_autoYoko)
                        {
                            if (_autoYokoEQ3 && i + 2 < end && (ch[i + 1] == '!' || ch[i + 1] == '?') && (ch[i + 2] == '!' || ch[i + 2] == '?'))
                            {
                                if (!CheckTcyPrev(ch, i - 1)) break;
                                if (!CheckTcyNext(ch, i + 3)) break;
                                buf.Append(_chukiMap["縦中横"][0]); buf.Append(ch[i]); buf.Append(ch[i + 1]); buf.Append(ch[i + 2]);
                                buf.Append(_chukiMap["縦中横終わり"][0]); i += 2; continue;
                            }
                            else if (i + 1 < end && (ch[i + 1] == '!' || ch[i + 1] == '?'))
                            {
                                if (!CheckTcyPrev(ch, i - 1)) break;
                                if (!CheckTcyNext(ch, i + 2)) break;
                                buf.Append(_chukiMap["縦中横"][0]); buf.Append(ch[i]); buf.Append(ch[i + 1]);
                                buf.Append(_chukiMap["縦中横終わり"][0]); i++; continue;
                            }
                            else if (_autoYokoEQ1 && (i == 0 || !CharUtils.IsNum(ch[i - 1])) && (i + 1 == end || !CharUtils.IsNum(ch[i + 1])))
                            {
                                if (!CheckTcyPrev(ch, i - 1)) break;
                                if (!CheckTcyNext(ch, i + 1)) break;
                                buf.Append(_chukiMap["縦中横"][0]); buf.Append(ch[i]); buf.Append(_chukiMap["縦中横終わり"][0]); continue;
                            }
                        }
                        break;
                }
            }

            // ひらがな/カタカナ + 濁点/半濁点（縦書き時）
            if (vertical && i + 1 < end && (ch[i + 1] == '゛' || ch[i + 1] == '゜'))
            {
                if (CharUtils.IsHiragana(ch[i]) || CharUtils.IsKatakana(ch[i]) || ch[i] == '〻')
                {
                    if (ch[i + 1] == '゛')
                    {
                        if ('ッ' != ch[i] && (('か' <= ch[i] && ch[i] <= 'と') || ('カ' <= ch[i] && ch[i] <= 'ト')))
                        { ch[i] = (char)(ch[i] + 1); buf.Append(ch[i]); i++; continue; }
                        if (ch[i] == 'ウ') { buf.Append('ヴ'); i++; continue; }
                        if (ch[i] == 'ワ') { buf.Append('ヷ'); i++; continue; }
                        if (ch[i] == 'ヲ') { buf.Append('ヺ'); i++; continue; }
                        if (ch[i] == 'う') { buf.Append('ゔ'); i++; continue; }
                        if (ch[i] == 'ゝ') { buf.Append('ゞ'); i++; continue; }
                        if (ch[i] == 'ヽ') { buf.Append('ヾ'); i++; continue; }
                    }
                    if (('は' <= ch[i] && ch[i] <= 'ほ') || ('ハ' <= ch[i] && ch[i] <= 'ホ'))
                    {
                        buf.Append(ch[i + 1] == '゛' ? (char)(ch[i] + 1) : (char)(ch[i] + 2));
                        i++; continue;
                    }
                    if (_dakutenType == 1 && !(_inYoko || noTcy))
                    {
                        buf.Append("<span class=\"dakuten\">").Append(ch[i]).Append("<span>");
                        buf.Append(ch[i + 1] == '゛' ? "゛" : "゜");
                        buf.Append("</span></span>");
                        i++; continue;
                    }
                    else if (_dakutenType == 2)
                    {
                        string cname = "u" + ((int)ch[i]).ToString("x");
                        if (ch[i + 1] == '゛') cname += "-u3099"; else cname += "-u309a";
                        if (PrintGlyphFontTag(buf, "dakuten/" + cname + ".ttf", cname, ch[i]))
                        {
                            LogAppender.Warn(lineNum, "濁点フォント利用", "" + ch[i] + ch[i + 1]);
                            i++; continue;
                        }
                    }
                }
            }

            ConvertReplacedChar(buf, ch, i, noTcy);
        }
    }

    /// <summary>自動縦中横の前の半角チェック。Java: checkTcyPrev</summary>
    private bool CheckTcyPrev(char[] ch, int i)
    {
        while (i >= 0)
        {
            if (ch[i] == '>') { do { i--; } while (i >= 0 && ch[i] != '<'); i--; continue; }
            if (ch[i] == ' ') { i--; continue; }
            if (CharUtils.IsHalf(ch[i])) return false;
            return true;
        }
        return true;
    }

    /// <summary>自動縦中横の後ろ半角チェック。Java: checkTcyNext</summary>
    private bool CheckTcyNext(char[] ch, int i)
    {
        while (i < ch.Length)
        {
            if (ch[i] == '<') { do { i++; } while (i < ch.Length && ch[i] != '>'); i++; continue; }
            if (ch[i] == ' ') { i++; continue; }
            if (CharUtils.IsHalf(ch[i])) return false;
            return true;
        }
        return true;
    }

    /// <summary>1文字をbufに出力。エスケープ・置換処理あり。Java: convertReplacedChar</summary>
    private void ConvertReplacedChar(StringBuilder buf, char[] ch, int idx, bool noTcy)
    {
        if (ch[idx] == '\0') return;

        int length = buf.Length;

        // エスケープ文字処理
        bool escaped = false;
        if (idx > 0)
        {
            switch (ch[idx])
            {
                case '》': case '《': case '｜': case '＃': case '※':
                    if (ch[idx - 1] == '※')
                    {
                        buf.Length = length - 1;
                        escaped = true;
                    }
                    break;
            }
        }

        if (_replaceMap != null && _replaceMap.TryGetValue(ch[idx], out string? replaced1))
        {
            buf.Append(replaced1); return;
        }

        if (idx > 1 || (idx == 1 && !escaped))
        {
            if (_replace2Map != null)
            {
                string key = "" + ch[idx - (escaped ? 2 : 1)] + ch[idx];
                if (_replace2Map.TryGetValue(key, out string? replaced2))
                {
                    buf.Length = length - 1;
                    buf.Append(replaced2); return;
                }
            }
        }

        if (escaped) { buf.Append(ch[idx]); ch[idx] = '　'; return; }

        // 全角スペース禁則
        if (!(_inYoko || noTcy))
        {
            switch (_spaceHyphenation)
            {
                case 1:
                    if (idx > 20 && ch[idx] == '　' && buf.Length > 0 && buf[buf.Length - 1] != '　' &&
                        (idx - 1 == ch.Length || idx + 1 < ch.Length && ch[idx + 1] != '　'))
                    { buf.Append("<span class=\"fullsp\"> </span>"); return; }
                    break;
                case 2:
                    if (idx > 20 && ch[idx] == '　' && buf.Length > 0 && buf[buf.Length - 1] != '　' &&
                        (idx - 1 == ch.Length || idx + 1 < ch.Length && ch[idx + 1] != '　'))
                    { buf.Append((char)0x2000).Append((char)0x2000); return; }
                    break;
            }
        }

        // 縦書き固有の文字変換
        if (vertical && !_inYoko)
        {
            switch (ch[idx])
            {
                case '≪': buf.Append("《"); return;
                case '≫': buf.Append("》"); return;
                case '\u201C': buf.Append("〝"); return;   // "
                case '\u201D': buf.Append("〟"); return;   // "
                case '―': buf.Append("─"); return;
                case '÷': case '±': case '∞': case '∴': case '∵':
                case 'Ⅰ': case 'Ⅱ': case 'Ⅲ': case 'Ⅳ': case 'Ⅴ': case 'Ⅵ':
                case 'Ⅶ': case 'Ⅷ': case 'Ⅸ': case 'Ⅹ': case 'Ⅺ': case 'Ⅻ':
                case 'ⅰ': case 'ⅱ': case 'ⅲ': case 'ⅳ': case 'ⅴ': case 'ⅵ':
                case 'ⅶ': case 'ⅷ': case 'ⅸ': case 'ⅹ': case 'ⅺ': case 'ⅻ':
                case '⓪': case '①': case '②': case '③': case '④': case '⑤':
                case '⑥': case '⑦': case '⑧': case '⑨': case '⑩':
                case '⑪': case '⑫': case '⑬': case '⑭': case '⑮': case '⑯':
                case '⑰': case '⑱': case '⑲': case '⑳':
                case '㉑': case '㉒': case '㉓': case '㉔': case '㉕': case '㉖':
                case '㉗': case '㉘': case '㉙': case '㉚': case '㉛': case '㉜':
                case '㉝': case '㉞': case '㉟': case '㊱': case '㊲': case '㊳':
                case '㊴': case '㊵': case '㊶': case '㊷': case '㊸': case '㊹':
                case '㊺': case '㊻': case '㊼': case '㊽': case '㊾': case '㊿':
                case '△': case '▽': case '▲': case '▼': case '☆': case '★':
                case '♂': case '♀': case '♪': case '♭': case '§': case '†': case '‡':
                case '‼': case '⁇': case '⁉': case '⁈':
                case '©': case '®': case '⁑': case '⁂':
                case '◐': case '◑': case '◒': case '◓': case '▷': case '▶': case '◁': case '◀':
                case '♤': case '♠': case '♢': case '♦': case '♡': case '♥': case '♧': case '♣': case '❤':
                case '☖': case '☗': case '☎': case '☁': case '☂': case '☃': case '♨': case '▱': case '⊿': case '✿':
                case '☹': case '☺': case '☻':
                case '✓': case '✔': case '␣': case '⏎': case '♩': case '♮': case '♫': case '♬':
                case 'ℓ': case '№': case '℡': case 'ℵ': case 'ℏ': case '℧':
                    if (!noTcy) { buf.Append(_chukiMap["正立"][0]); buf.Append(ch[idx]); buf.Append(_chukiMap["正立終わり"][0]); }
                    else buf.Append(ch[idx]);
                    return;
                default: buf.Append(ch[idx]); return;
            }
        }
        else
        {
            switch (ch[idx])
            {
                case '≪': buf.Append("《"); return;
                case '≫': buf.Append("》"); return;
                case '―': buf.Append("─"); return;
                default: buf.Append(ch[idx]); return;
            }
        }
    }

    /// <summary>画像タグを出力。単ページ出力なら true を返す。Java: printImageChuki</summary>
    private bool PrintImageChuki(TextWriter? output, StringBuilder buf, string srcFileName,
        string dstFileName, bool hasCaption, int lineNum)
    {
        int imagePageType = _writer.GetImagePageType(srcFileName, _tagLevel, lineNum, hasCaption);
        double ratio = _writer.GetImageWidthRatio(srcFileName, hasCaption);

        if (imagePageType == PageBreakType.IMAGE_INLINE_W)
        {
            if (ratio <= 0) buf.AppendFormat(_chukiMap["画像横"][0], dstFileName);
            else buf.AppendFormat(_chukiMap["画像幅"][0], ratio, dstFileName);
        }
        else if (imagePageType == PageBreakType.IMAGE_INLINE_H)
        {
            if (ratio <= 0) buf.AppendFormat(_chukiMap["画像縦"][0], dstFileName);
            else buf.AppendFormat(_chukiMap["画像幅"][0], ratio, dstFileName);
        }
        else if (imagePageType == PageBreakType.IMAGE_INLINE_TOP_W)
        {
            if (ratio <= 0) buf.AppendFormat(_chukiMap["画像上横"][0], dstFileName);
            else buf.AppendFormat(_chukiMap["画像幅上"][0], ratio, dstFileName);
        }
        else if (imagePageType == PageBreakType.IMAGE_INLINE_BOTTOM_W)
        {
            if (ratio <= 0) buf.AppendFormat(_chukiMap["画像下横"][0], dstFileName);
            else buf.AppendFormat(_chukiMap["画像幅下"][0], ratio, dstFileName);
        }
        else if (imagePageType == PageBreakType.IMAGE_INLINE_TOP)
        {
            if (ratio <= 0) buf.AppendFormat(_chukiMap["画像上"][0], dstFileName);
            else buf.AppendFormat(_chukiMap["画像幅上"][0], ratio, dstFileName);
        }
        else if (imagePageType == PageBreakType.IMAGE_INLINE_BOTTOM)
        {
            if (ratio <= 0) buf.AppendFormat(_chukiMap["画像下"][0], dstFileName);
            else buf.AppendFormat(_chukiMap["画像幅下"][0], ratio, dstFileName);
        }
        else if (imagePageType != PageBreakType.IMAGE_PAGE_NONE)
        {
            // 単ページ
            if (ratio != -1 && _imageFloatPage)
            {
                if (imagePageType == PageBreakType.IMAGE_PAGE_W)
                    buf.AppendFormat(_chukiMap["画像単横浮"][0], dstFileName);
                else if (imagePageType == PageBreakType.IMAGE_PAGE_H)
                    buf.AppendFormat(_chukiMap["画像単縦浮"][0], dstFileName);
                else if (ratio <= 0) buf.AppendFormat(_chukiMap["画像単浮"][0], dstFileName);
                else buf.AppendFormat(_chukiMap["画像単幅浮"][0], ratio, dstFileName);
            }
            else
            {
                if (buf.Length > 0) PrintLineBuffer(output, buf, lineNum, true);
                buf.AppendFormat(_chukiMap["画像"][0], dstFileName);
                buf.Append(_chukiMap["画像終わり"][0]);
                PrintImagePage(output, buf, lineNum, srcFileName, dstFileName, imagePageType);
                return true;
            }
        }
        else
        {
            // 画像通常表示
            if (ratio != -1 && _imageFloatBlock)
            {
                if (ratio <= 0) buf.AppendFormat(_chukiMap["画像浮"][0], dstFileName);
                else buf.AppendFormat(_chukiMap["画像幅浮"][0], ratio, dstFileName);
            }
            else
            {
                if (ratio <= 0) buf.AppendFormat(_chukiMap["画像"][0], dstFileName);
                else buf.AppendFormat(_chukiMap["画像幅"][0], ratio, dstFileName);
            }
        }

        if (hasCaption) { _inImageTag = true; _nextLineIsCaption = true; }
        else buf.Append(_chukiMap["画像終わり"][0]);
        return false;
    }

    /// <summary>前後に改ページを入れて画像を出力。Java: printImagePage</summary>
    private void PrintImagePage(TextWriter? output, StringBuilder buf, int lineNum,
        string srcFileName, string dstFileName, int imagePageType)
    {
        bool hasPageBreakTrigger = _pageBreakTrigger != null && !_pageBreakTrigger.NoChapter;

        switch (imagePageType)
        {
            case PageBreakType.IMAGE_PAGE_W:
                SetPageBreakTrigger(_pageBreakImageW);
                _pageBreakImageW.SrcFileName = srcFileName;
                break;
            case PageBreakType.IMAGE_PAGE_H:
                SetPageBreakTrigger(_pageBreakImageH);
                _pageBreakImageH.SrcFileName = srcFileName;
                break;
            case PageBreakType.IMAGE_PAGE_NOFIT:
                SetPageBreakTrigger(_pageBreakImageNoFit);
                _pageBreakImageNoFit.SrcFileName = srcFileName;
                break;
            default:
                SetPageBreakTrigger(_pageBreakImageAuto);
                _pageBreakImageAuto.SrcFileName = srcFileName;
                break;
        }
        PrintLineBuffer(output, buf, lineNum, true);

        if (hasPageBreakTrigger) SetPageBreakTrigger(_pageBreakNormal);
        else SetPageBreakTrigger(_pageBreakNoChapter);
    }

    /// <summary>行バッファを出力。改ページフラグがあれば改ページ処理。Java: printLineBuffer</summary>
    private void PrintLineBuffer(TextWriter? output, StringBuilder buf, int lineNum, bool noBr)
    {
        string line = buf.ToString();
        int length = buf.Length;
        if (CharUtils.IsSpace(line)) { line = ""; length = 0; }

        if (_removeEmptyLine > 0 && length > 0 && CharUtils.IsSpace(line)) { line = ""; length = 0; }

        if (length == 0)
        {
            if (!_skipMiddleEmpty && !noBr) _printEmptyLines++;
            buf.Length = 0;
            return;
        }

        // タグ階層カウント
        int tagStart = 0, tagEnd = 0;
        bool inTag = false;
        for (int i = 0; i < length; i++)
        {
            if (inTag)
            {
                if (line[i] == '/' && i + 1 < length && line[i + 1] == '>') tagEnd++;
                if (line[i] == '>') inTag = false;
            }
            else
            {
                if (line[i] == '<')
                {
                    if (i < length - 1 && line[i + 1] == '/') tagEnd++;
                    else tagStart++;
                    inTag = true;
                }
            }
        }

        if (output != null)
        {
            // 強制改ページ
            if (_forcePageBreak && _pageBreakTrigger == null && _tagLevel == 0)
            {
                if (_pageByteSize > _forcePageBreakSize)
                    SetPageBreakTrigger(_pageBreakNoChapter);
                else if (_forcePageBreakEmptyLine > 0 && _printEmptyLines >= _forcePageBreakEmptyLine &&
                         _pageByteSize > _forcePageBreakEmptySize)
                    SetPageBreakTrigger(_pageBreakNoChapter);
                else if (_forcePageBreakChapterLevel > 0 && _pageByteSize > _forcePageBreakChapterSize)
                {
                    var cli = _bookInfo?.GetChapterLineInfo(lineNum);
                    if (cli != null) SetPageBreakTrigger(_pageBreakNoChapter);
                    else if (tagStart - tagEnd > 0 && (_bookInfo?.GetChapterLevel(lineNum + 1) ?? 0) > 0)
                        SetPageBreakTrigger(_pageBreakNoChapter);
                }
            }

            // 改ページ処理
            if (_pageBreakTrigger != null)
            {
                if (_pageBreakTrigger.PageType != PageBreakType.PAGE_NONE)
                    _writer.NextSection(output, lineNum, _pageBreakTrigger.PageType, PageBreakType.IMAGE_PAGE_NONE, null);
                else
                    _writer.NextSection(output, lineNum, PageBreakType.PAGE_NONE, _pageBreakTrigger.ImagePageType, _pageBreakTrigger.SrcFileName);

                _pageByteSize = 0;
                _sectionCharLength = 0;
                if (_tagLevel > 0) LogAppender.Error(lineNum, "タグが閉じていません");
                _tagLevel = 0;
                _lineIdNum = 0;
                _pageBreakTrigger = null;
            }

            _skipMiddleEmpty = false;

            // 空行出力
            if (_printEmptyLines > 0)
            {
                string br = _chukiMap["改行"][0];
                int lines = Math.Min(_maxEmptyLine, _printEmptyLines - _removeEmptyLine);
                if (_lastChapterLine >= lineNum - _printEmptyLines - 2)
                    lines = Math.Max(1, lines);
                for (int i = lines - 1; i >= 0; i--)
                {
                    output.Write("<p>");
                    output.Write(br);
                    output.Write("</p>\n");
                }
                _pageByteSize += (br.Length + 8) * lines;
                _printEmptyLines = 0;
            }

            _lineIdNum++;
            var chapterLineInfo = _bookInfo?.GetChapterLineInfo(lineNum);
            string? chapterId = null;

            if (noBr)
            {
                if (chapterLineInfo != null)
                {
                    chapterId = "kobo." + _lineIdNum + ".1";
                    if (line.StartsWith("<"))
                        line = new Regex(@"(<[\d\w]+)").Replace(line, "$1 id=\"" + chapterId + "\"", 1);
                    else
                    {
                        output.Write("<span id=\"" + chapterId + "\">" + line[0] + "</span>");
                        _pageByteSize += chapterId.Length + 20;
                        line = line[1..];
                    }
                }
            }
            else
            {
                if (_withMarkId || (chapterLineInfo != null && !chapterLineInfo.PageBreakChapter))
                {
                    chapterId = "kobo." + _lineIdNum + ".1";
                    output.Write("<p id=\"" + chapterId + "\">");
                    _pageByteSize += chapterId.Length + 14;
                }
                else
                {
                    output.Write("<p>");
                    _pageByteSize += 7;
                }
            }

            output.Write(line);
            if (_forcePageBreak) _pageByteSize += System.Text.Encoding.UTF8.GetByteCount(line);

            if (!noBr) output.Write("</p>\n");

            // 章追加
            if (chapterLineInfo != null && _lastChapterLine != lineNum)
            {
                string? name = chapterLineInfo.ChapterName;
                if (name != null && name.Length > 0)
                {
                    if (chapterLineInfo.PageBreakChapter) _writer.AddChapter(null, name, chapterLineInfo.Level % 10);
                    else _writer.AddChapter(chapterId, name, chapterLineInfo.Level % 10);
                    _lastChapterLine = lineNum;
                }
            }

            _sectionCharLength += length;
        }

        _tagLevel += tagStart - tagEnd;
        buf.Length = 0;
    }
}
