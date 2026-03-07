using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text;
using System.Text.RegularExpressions;
using AozoraEpub3.Core.Converter;
using AozoraEpub3.Core.Info;
using AozoraEpub3.Core.Io;
using AozoraEpub3.Core.Web;

// Shift-JIS など Windows コードページを有効化
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

const string Version = "1.0.0-dotnet";

// ── オプション定義 ─────────────────────────────────────────────
// System.CommandLine 3.0 preview API: new Option<T>(name, aliases[]) + property init
var iniOption = new Option<string>("--ini", ["-i"])
{
    DefaultValueFactory = _ => "AozoraEpub3.ini",
    Description = "設定ファイル (.ini) のパス"
};
var titleTypeOption = new Option<int>("-t")
{
    DefaultValueFactory = _ => 0,
    Description = "表題種別 [0:表題→著者名][1:著者名→表題][2:表題→著者名(副題優先)][3:表題のみ][4:なし]"
};
var useFileNameOption = new Option<bool>("-tf")
{
    Description = "入力ファイル名を表題に利用",
    Arity = ArgumentArity.Zero
};
var coverOption = new Option<string?>("--cover", ["-c"])
{
    Description = "表紙画像 [0:先頭の挿絵][1:同名画像][ファイル名]"
};
var extOption = new Option<string>("-ext")
{
    DefaultValueFactory = _ => ".epub",
    Description = "出力拡張子 [.epub][.kepub.epub]"
};
var ofOption = new Option<bool>("-of")
{
    Description = "出力ファイル名を入力ファイル名に合わせる",
    Arity = ArgumentArity.Zero
};
var dstOption = new Option<DirectoryInfo?>("--dst", ["-d"])
{
    Description = "出力先ディレクトリ"
};
var encOption = new Option<string>("-enc")
{
    DefaultValueFactory = _ => "MS932",
    Description = "入力エンコード [MS932][UTF-8]"
};
var horOption = new Option<bool>("-hor")
{
    Description = "横書き (省略時は縦書き)",
    Arity = ArgumentArity.Zero
};
var deviceOption = new Option<string?>("-device")
{
    Description = "端末種別 [kindle]"
};

var urlOption = new Option<string?>("--url", ["-u"])
{
    Description = "変換するURL (なろう等の小説サイト)"
};
var webConfigOption = new Option<string>("--web-config")
{
    DefaultValueFactory = _ => Path.Combine(AppContext.BaseDirectory, "web"),
    Description = "web設定ディレクトリ (extract.txt を格納するパス)"
};
var intervalOption = new Option<int>("--interval")
{
    DefaultValueFactory = _ => 1500,
    Description = "URL変換時のダウンロード間隔 (ミリ秒)"
};

var filesArgument = new Argument<string[]>("files")
{
    Arity = ArgumentArity.ZeroOrMore,
    Description = "変換するファイル (txt,zip,cbz,rar)"
};

// ── RootCommand ────────────────────────────────────────────────
var rootCommand = new RootCommand($"AozoraEpub3 [-options] input_files(txt,zip,cbz)  version: {Version}");
rootCommand.Add(iniOption);
rootCommand.Add(titleTypeOption);
rootCommand.Add(useFileNameOption);
rootCommand.Add(coverOption);
rootCommand.Add(extOption);
rootCommand.Add(ofOption);
rootCommand.Add(dstOption);
rootCommand.Add(encOption);
rootCommand.Add(horOption);
rootCommand.Add(deviceOption);
rootCommand.Add(urlOption);
rootCommand.Add(webConfigOption);
rootCommand.Add(intervalOption);
rootCommand.Add(filesArgument);

rootCommand.SetAction((Action<ParseResult>)(parseResult =>
{
    var iniPath      = parseResult.GetValue(iniOption) ?? "AozoraEpub3.ini";
    var titleIndex   = parseResult.GetValue(titleTypeOption);
    var useFileName  = parseResult.GetValue(useFileNameOption);
    var coverValue   = parseResult.GetValue(coverOption);
    var outExt       = parseResult.GetValue(extOption) ?? ".epub";
    var autoFileName = !parseResult.GetValue(ofOption);
    var dstDir       = parseResult.GetValue(dstOption);
    var encType      = parseResult.GetValue(encOption) ?? "MS932";
    var vertical     = !parseResult.GetValue(horOption);
    var targetDevice = parseResult.GetValue(deviceOption);
    var urlValue     = parseResult.GetValue(urlOption);
    var webConfig    = parseResult.GetValue(webConfigOption) ?? Path.Combine(AppContext.BaseDirectory, "web");
    var downloadInterval = parseResult.GetValue(intervalOption);
    var fileNames    = parseResult.GetValue(filesArgument) ?? [];

    // INI 読み込み
    var ini = LoadIni(iniPath);

    // テンプレートパス（実行ファイルの隣の template/ フォルダ）
    string templatePath = Path.Combine(AppContext.BaseDirectory, "template") + Path.DirectorySeparatorChar;

    // Writer・Converter 生成
    var epub3Writer = new Epub3Writer(templatePath);
    if (string.Equals(targetDevice, "kindle", StringComparison.OrdinalIgnoreCase))
        epub3Writer.SetIsKindle(true);

    var converter = new AozoraEpub3Converter(epub3Writer, templatePath);
    ApplyIniSettings(ini, converter);

    // INI 由来の BookInfo デフォルト値
    bool coverPage      = GetBool(ini, "CoverPage");
    int  titlePageType  = BookInfo.TITLE_NONE;
    if (GetBool(ini, "TitlePageWrite"))
        titlePageType = GetInt(ini, "TitlePage");
    bool tocPage        = GetBool(ini, "TocPage");
    bool tocVertical    = GetBool(ini, "TocVertical");
    bool coverPageToc   = GetBool(ini, "CoverPageToc");
    bool insertTitleToc = !ini.ContainsKey("InsertTitleToc") || GetBool(ini, "InsertTitleToc");
    int  maxCoverLine   = GetInt(ini, "MaxCoverLine");

    var titleType = (BookInfo.TitleType)Math.Clamp(titleIndex, 0, 5);

    // ── URL 変換 ─────────────────────────────────────────────
    if (!string.IsNullOrEmpty(urlValue))
    {
        LogAppender.Println("--------");
        LogAppender.Println($"URL変換開始: {urlValue}");

        var webSettings = new NarouFormatSettings();
        string settingsFile = Path.Combine(webConfig, "setting_narourb.ini");
        if (File.Exists(settingsFile)) webSettings.Load(settingsFile);
        NarouFormatSettings.GenerateDefaultIfMissing(settingsFile);

        ConvertUrlToEpub(urlValue, webConfig, webSettings, downloadInterval,
            ini, vertical, titleIndex, outExt ?? ".epub", dstDir?.FullName);
    }

    // ── 各ファイルを処理 ───────────────────────────────────────
    foreach (string fileName in fileNames)
    {
        LogAppender.Println("--------");
        string srcFilePath = Path.GetFullPath(fileName);
        if (!File.Exists(srcFilePath))
        {
            LogAppender.Println($"[ERROR] file not exist. {srcFilePath}");
            continue;
        }

        string ext = Path.GetExtension(srcFilePath).TrimStart('.').ToLowerInvariant();

        // 表紙設定の解析
        int     coverImageIndex = -1;
        string? coverFileName   = coverValue;
        if (coverFileName != null)
        {
            if (coverFileName == "0")      { coverImageIndex = 0; coverFileName = ""; }
            else if (coverFileName == "1") { coverFileName = GetSameCoverFileName(srcFilePath); }
        }

        // アーカイブ内テキスト数カウント
        int  txtCount  = 1;
        bool imageOnly = false;
        bool isFile    = ext == "txt";
        if (ext is "zip" or "txtz")
        {
            txtCount = ArchiveTextExtractor.CountZipText(srcFilePath);
            if (txtCount == 0) { txtCount = 1; imageOnly = true; }
        }
        else if (ext == "rar")
        {
            txtCount = ArchiveTextExtractor.CountRarText(srcFilePath);
            if (txtCount == 0) { txtCount = 1; imageOnly = true; }
        }
        else if (ext == "cbz") { imageOnly = true; }

        for (int txtIdx = 0; txtIdx < txtCount; txtIdx++)
        {
            var imageInfoReader = new ImageInfoReader(isFile, srcFilePath);

            if (imageOnly)
            {
                LogAppender.Println("[WARN] 画像のみ ePub 出力は未サポートです (cbz / 画像のみ zip)");
                continue;
            }

            // 第1パス: BookInfo 取得
            var bookInfo = GetBookInfo(srcFilePath, ext, txtIdx, imageInfoReader, converter, encType, titleType);
            if (bookInfo == null) { LogAppender.Println("[ERROR] BookInfo 取得失敗"); continue; }

            bookInfo.Vertical       = vertical;
            bookInfo.InsertTocPage  = tocPage;
            bookInfo.TocVertical    = tocVertical;
            bookInfo.InsertTitleToc = insertTitleToc;
            converter.vertical      = vertical;
            bookInfo.TitlePageType  = titlePageType;

            if (!bookInfo.InsertTitleToc && bookInfo.TitleLine >= 0)
                bookInfo.RemoveChapterLineInfo(bookInfo.TitleLine);

            // 表紙行数チェック
            if (coverFileName == "" && maxCoverLine > 0 && bookInfo.FirstImageLineNum >= maxCoverLine)
            {
                coverImageIndex = -1;
                coverFileName   = null;
            }

            // 画像情報ロード（アーカイブの場合）
            if (!isFile)
            {
                if (ext == "rar") imageInfoReader.LoadRarImageInfos(false);
                else              imageInfoReader.LoadZipImageInfos(false);
            }

            // 表紙設定反映
            bookInfo.InsertCoverPageToc = coverPageToc;
            bookInfo.InsertCoverPage    = coverPage;
            bookInfo.CoverImageIndex    = coverImageIndex;

            if (coverFileName != null && !coverFileName.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                if (!File.Exists(coverFileName))
                {
                    string alt = Path.Combine(Path.GetDirectoryName(srcFilePath)!, coverFileName);
                    if (File.Exists(alt))
                        coverFileName = alt;
                    else
                    {
                        LogAppender.Println($"[WARN] 表紙画像ファイルが見つかりません : {Path.GetFullPath(coverFileName)}");
                        coverFileName = null;
                    }
                }
            }
            bookInfo.CoverFileName = coverFileName;

            // ファイル名から表題・著者を補完
            var titleCreator = BookInfo.GetFileTitleCreator(Path.GetFileName(srcFilePath));
            if (titleCreator != null)
            {
                if (useFileName)
                {
                    if (!string.IsNullOrWhiteSpace(titleCreator[0])) bookInfo.Title   = titleCreator[0];
                    if (!string.IsNullOrWhiteSpace(titleCreator[1])) bookInfo.Creator = titleCreator[1];
                }
                else
                {
                    if (string.IsNullOrEmpty(bookInfo.Title))   bookInfo.Title   = titleCreator[0] ?? "";
                    if (string.IsNullOrEmpty(bookInfo.Creator)) bookInfo.Creator = titleCreator[1] ?? "";
                }
            }

            // 出力ファイルパス決定
            string outFile = GetOutFilePath(srcFilePath, dstDir?.FullName, bookInfo, autoFileName, outExt);

            // 第2パス: 変換実行
            ConvertFile(srcFilePath, ext, outFile, converter, epub3Writer, encType, bookInfo, imageInfoReader, txtIdx);
        }

        if (ext != "txt") ArchiveTextExtractor.ClearCache(srcFilePath);
    }
}));

return rootCommand.Parse(args).Invoke(null!);

// ── ヘルパー関数 ────────────────────────────────────────────────

static Dictionary<string, string> LoadIni(string path)
{
    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (!File.Exists(path)) return dict;
    foreach (var line in File.ReadAllLines(path, Encoding.UTF8))
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith('#') || trimmed.StartsWith(';') || !trimmed.Contains('=')) continue;
        int idx = trimmed.IndexOf('=');
        dict[trimmed[..idx].Trim()] = trimmed[(idx + 1)..].Trim();
    }
    return dict;
}

static bool GetBool(Dictionary<string, string> ini, string key) =>
    ini.TryGetValue(key, out var v) && v == "1";

static int GetInt(Dictionary<string, string> ini, string key) =>
    ini.TryGetValue(key, out var v) && int.TryParse(v, out int i) ? i : 0;

static void ApplyIniSettings(Dictionary<string, string> ini, AozoraEpub3Converter converter)
{
    converter.SetNoIllust(GetBool(ini, "NoIllust"));
    converter.SetWithMarkId(GetBool(ini, "MarkId"));
    converter.SetAutoYoko(
        GetBool(ini, "AutoYoko"), GetBool(ini, "AutoYokoNum1"),
        GetBool(ini, "AutoYokoNum3"), GetBool(ini, "AutoYokoEQ1"));
    converter.SetCharOutput(GetInt(ini, "DakutenType"), GetBool(ini, "IvsBMP"), GetBool(ini, "IvsSSP"));
    converter.SetSpaceHyphenation(GetInt(ini, "SpaceHyphenation"));
    converter.SetCommentPrint(GetBool(ini, "CommentPrint"), GetBool(ini, "CommentConvert"));
    converter.SetRemoveEmptyLine(GetInt(ini, "RemoveEmptyLine"), GetInt(ini, "MaxEmptyLine"));

    int pbSize = 0, pbEmpty = 0, pbEmptySize = 0, pbChapter = 0, pbChapterSize = 0;
    if (GetBool(ini, "PageBreak"))
    {
        pbSize = GetInt(ini, "PageBreakSize") * 1024;
        if (GetBool(ini, "PageBreakEmpty"))
        {
            pbEmpty     = GetInt(ini, "PageBreakEmptyLine");
            pbEmptySize = GetInt(ini, "PageBreakEmptySize") * 1024;
        }
        if (GetBool(ini, "PageBreakChapter"))
        {
            pbChapter     = 1;
            pbChapterSize = GetInt(ini, "PageBreakChapterSize") * 1024;
        }
    }
    converter.SetForcePageBreak(pbSize, pbEmpty, pbEmptySize, pbChapter, pbChapterSize);

    int maxLength = ini.TryGetValue("ChapterNameLength", out var cl) && int.TryParse(cl, out int clv) ? clv : 64;
    string chapterPattern = GetBool(ini, "ChapterPattern") && ini.TryGetValue("ChapterPatternText", out var cp) ? cp : "";

    converter.SetChapterLevel(
        maxLength,
        GetBool(ini, "ChapterExclude"), GetBool(ini, "ChapterUseNextLine"),
        !ini.ContainsKey("ChapterSection") || GetBool(ini, "ChapterSection"),
        GetBool(ini, "ChapterH"), GetBool(ini, "ChapterH1"), GetBool(ini, "ChapterH2"), GetBool(ini, "ChapterH3"),
        GetBool(ini, "SameLineChapter"),
        GetBool(ini, "ChapterName"),
        GetBool(ini, "ChapterNumOnly"), GetBool(ini, "ChapterNumTitle"),
        GetBool(ini, "ChapterNumParen"), GetBool(ini, "ChapterNumParenTitle"),
        chapterPattern);
}

static BookInfo? GetBookInfo(
    string srcFilePath, string ext, int txtIdx,
    ImageInfoReader imageInfoReader, AozoraEpub3Converter converter,
    string encType, BookInfo.TitleType titleType)
{
    try
    {
        var textEntryName = new string[1];
        var stream = ArchiveTextExtractor.GetTextInputStream(srcFilePath, ext, textEntryName, txtIdx);
        if (stream == null) return null;
        using var src = new StreamReader(stream, ResolveEncoding(encType));
        var bookInfo = converter.GetBookInfo(srcFilePath, src, imageInfoReader, titleType, false);
        bookInfo.TextEntryName = textEntryName[0];
        return bookInfo;
    }
    catch (Exception e)
    {
        LogAppender.Println($"[ERROR] GetBookInfo: {e.Message}");
        return null;
    }
}

static string GetOutFilePath(string srcFilePath, string? dstPath, BookInfo bookInfo, bool autoFileName, string outExt)
{
    string dir = Path.GetFullPath(dstPath ?? Path.GetDirectoryName(Path.GetFullPath(srcFilePath))!);
    string baseName;
    if (autoFileName && (bookInfo.Creator != null || bookInfo.Title != null))
    {
        string part = "";
        if (!string.IsNullOrEmpty(bookInfo.Creator))
        {
            string c = Regex.Replace(bookInfo.Creator, @"[\\/:*?<>|""\t]", "");
            if (c.Length > 64) c = c[..64];
            part = $"[{c}] ";
        }
        string t = Regex.Replace(bookInfo.Title ?? "", @"[\\/:*!?<>|""\t]", "");
        baseName = part + t;
        if (baseName.Length > 250) baseName = baseName[..250];
    }
    else
    {
        baseName = Path.GetFileNameWithoutExtension(srcFilePath);
    }
    if (string.IsNullOrEmpty(outExt)) outExt = ".epub";

    string outFile = Path.GetFullPath(Path.Combine(dir, baseName + outExt));
    // パストラバーサル対策: 出力パスが dir 配下にあることを検証
    if (!outFile.StartsWith(dir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
        !outFile.Equals(dir, StringComparison.OrdinalIgnoreCase))
        throw new IOException($"出力パスが許可されたディレクトリ外です: {outFile}");
    return outFile;
}

static void ConvertFile(
    string srcFilePath, string ext, string outFile,
    AozoraEpub3Converter converter, Epub3Writer writer,
    string encType, BookInfo bookInfo, ImageInfoReader imageInfoReader, int txtIdx)
{
    try
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        LogAppender.Println($"変換開始 : {srcFilePath}");

        TextReader? src = null;
        if (!bookInfo.ImageOnly)
        {
            var stream = ArchiveTextExtractor.GetTextInputStream(srcFilePath, ext, null, txtIdx);
            if (stream == null)
            {
                LogAppender.Println("[ERROR] テキストストリームを開けませんでした");
                return;
            }
            src = new StreamReader(stream, ResolveEncoding(encType));
        }

        writer.Write(converter, src!, srcFilePath, ext, outFile, bookInfo, imageInfoReader);

        sw.Stop();
        LogAppender.Println($"変換完了[{sw.Elapsed.TotalSeconds:F1}s] : {outFile}");
    }
    catch (Exception e)
    {
        LogAppender.Println($"[ERROR] 変換中にエラーが発生しました : {e.Message}");
    }
}

static string? GetSameCoverFileName(string srcFilePath)
{
    string basePath = srcFilePath[..(srcFilePath.LastIndexOf('.') + 1)];
    foreach (string ext2 in new[] { "png", "jpg", "jpeg", "PNG", "JPG", "JPEG" })
    {
        string path = basePath + ext2;
        if (File.Exists(path)) return path;
    }
    return null;
}

/// <summary>
/// エンコード名を解決する。"MS932" (Java互換エイリアス) はコードページ932に変換。
/// </summary>
static Encoding ResolveEncoding(string name) =>
    name.Equals("MS932", StringComparison.OrdinalIgnoreCase) ||
    name.Equals("Shift_JIS", StringComparison.OrdinalIgnoreCase) ||
    name.Equals("SJIS", StringComparison.OrdinalIgnoreCase)
        ? Encoding.GetEncoding(932)
        : Encoding.GetEncoding(name);

static void ConvertUrlToEpub(
    string urlValue, string webConfig, NarouFormatSettings webSettings,
    int downloadInterval, Dictionary<string, string> ini, bool vertical,
    int titleIndex, string outExt, string? dstPath)
{
    string outDir = dstPath ?? Directory.GetCurrentDirectory();

    List<string>? lines;
    WebAozoraConverter? webConverter;
    try
    {
        (lines, webConverter) = Task.Run(() => WebAozoraConverter.ConvertToAozoraLinesWithConverterAsync(
            urlValue, webConfig, webSettings, downloadInterval, outDir)).GetAwaiter().GetResult();
    }
    catch (Exception e)
    {
        LogAppender.Println($"[ERROR] URL変換失敗: {e.Message}");
        return;
    }

    if (lines == null)
    {
        LogAppender.Println("[ERROR] URL変換に失敗しました");
        return;
    }

    // ファイナライズ処理
    new AozoraTextFinalizer(webSettings).Finalize(lines);

    string aozoraText = string.Join("\n", lines);
    string templatePath = Path.Combine(AppContext.BaseDirectory, "template") + Path.DirectorySeparatorChar;

    var epub3WriterWeb = new Epub3Writer(templatePath);
    var converterWeb = new AozoraEpub3Converter(epub3WriterWeb, templatePath);
    ApplyIniSettings(ini, converterWeb);
    converterWeb.vertical = vertical;

    // BookInfo 取得（1パス目）
    // ImageInfoReader: dstPath を親パスとして設定（画像ファイル参照用）
    string dummySrc = Path.Combine(outDir, "web_source.txt");
    var imageInfoReaderWeb = new ImageInfoReader(true, dummySrc);
    BookInfo bookInfoWeb;
    using (var webSrc1 = new StringReader(aozoraText))
    {
        bookInfoWeb = converterWeb.GetBookInfo("web", webSrc1, imageInfoReaderWeb,
            (BookInfo.TitleType)Math.Clamp(titleIndex, 0, 5), false);
    }
    bookInfoWeb.Vertical = vertical;
    bookInfoWeb.InsertTocPage = true;
    bookInfoWeb.TocVertical = !vertical ? false : true;
    bookInfoWeb.TitlePageType = BookInfo.TITLE_HORIZONTAL;
    bookInfoWeb.InsertTitleToc = true;
    bookInfoWeb.InsertTitlePage = true;

    // 表紙画像設定
    if (webConverter?.CoverImagePath != null && File.Exists(webConverter.CoverImagePath))
    {
        bookInfoWeb.CoverFileName = "cover.jpg";
        bookInfoWeb.InsertCoverPage = true;
    }

    // 出力ファイル名を決定
    string urlTitle = bookInfoWeb.Title ?? "converted";
    string safeTitle = Regex.Replace(urlTitle, @"[\\/:*?<>|""\t]", "");
    if (safeTitle.Length > 200) safeTitle = safeTitle[..200];
    if (!string.IsNullOrEmpty(bookInfoWeb.Creator))
    {
        string safeCreator = Regex.Replace(bookInfoWeb.Creator, @"[\\/:*?<>|""\t]", "");
        safeTitle = $"[{safeCreator}] {safeTitle}";
    }

    string outFile = Path.Combine(outDir, safeTitle + outExt);

    // 変換実行（2パス目）
    using var webSrc2 = new StringReader(aozoraText);
    try
    {
        epub3WriterWeb.Write(converterWeb, webSrc2, "web", "txt", outFile, bookInfoWeb, imageInfoReaderWeb);
        LogAppender.Println($"変換完了: {outFile}");
    }
    catch (Exception e)
    {
        LogAppender.Println($"[ERROR] EPUB生成失敗: {e.Message}");
    }
}
