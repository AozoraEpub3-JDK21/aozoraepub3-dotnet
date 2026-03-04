using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using AozoraEpub3.Core.Converter;
using AozoraEpub3.Core.Info;
using Scriban;
using Scriban.Runtime;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SharpCompress.Archives.Rar;
// ImageInfo の名前衝突解消: SixLabors.ImageSharp.ImageInfo より AozoraEpub3.Core.Info.ImageInfo を優先
using ImageInfo = AozoraEpub3.Core.Info.ImageInfo;
using SixImage = SixLabors.ImageSharp.Image;

namespace AozoraEpub3.Core.Io;

/// <summary>ePub3用のファイル一式をZipで固めたファイルを生成.
/// 本文は改ページでセクション毎に分割されて xhtml/以下に 0001.xhtml 0002.xhtml の連番ファイル名で格納
/// 画像は images/以下に 0001.jpg 0002.png のようにリネームして格納
/// </summary>
public class Epub3Writer : IEpub3Writer
{
    // ─── EPUB 内パス定数 ─────────────────────────────────────────
    const string MimetypePath = "mimetype";
    const string OpsPath = "OPS/";
    const string ImagesPath = "images/";
    const string CssPath = "css/";
    const string XhtmlPath = "xhtml/";
    const string FontsPath = "fonts/";
    const string GaijiPath = "gaiji/";

    const string VerticalTextCss    = "vertical_text.css";
    const string VerticalTextCssVm  = "vertical_text.sbn";
    const string HorizontalTextCss  = "horizontal_text.css";
    const string HorizontalTextCssVm = "horizontal_text.sbn";

    const string XhtmlHeaderVm = "xhtml_header.sbn";
    const string XhtmlFooterVm = "xhtml_footer.sbn";
    const string TitleFile   = "title.xhtml";
    const string TitleMVm    = "title_middle.sbn";
    const string TitleHVm    = "title_horizontal.sbn";
    const string XhtmlNavFile = "nav.xhtml";
    const string XhtmlNavVm   = "xhtml_nav.sbn";
    const string CoverFile   = "cover.xhtml";
    const string CoverVm     = "cover.sbn";
    const string PackageFile = "package.opf";
    const string PackageVm   = "package.sbn";
    const string TocFile     = "toc.ncx";
    const string TocVm       = "toc.ncx.sbn";

    static readonly string[] TemplateFileNamesVertical = {
        "META-INF/container.xml",
        OpsPath + CssPath + "vertical_middle.css",
        OpsPath + CssPath + "vertical_image.css",
        OpsPath + CssPath + "vertical_font.css",
        OpsPath + CssPath + "vertical.css",
    };
    static readonly string[] TemplateFileNamesHorizontal = {
        "META-INF/container.xml",
        OpsPath + CssPath + "horizontal_middle.css",
        OpsPath + CssPath + "horizontal_image.css",
        OpsPath + CssPath + "horizontal_font.css",
        OpsPath + CssPath + "horizontal.css",
    };
    string[] GetTemplateFiles() =>
        bookInfo?.Vertical == true ? TemplateFileNamesVertical : TemplateFileNamesHorizontal;

    // ─── プロパティ ──────────────────────────────────────────────
    int dispW = 600;
    int dispH = 800;
    int maxImageW = 0;
    int maxImageH = 0;
    int maxImagePixels = 0;
    float imageScale = 1;
    int imageFloatType = 0;
    int imageFloatW = 0;
    int imageFloatH = 0;
    int singlePageSizeW = 400;
    int singlePageSizeH = 600;
    int singlePageWidth = 550;
    int imageSizeType = SectionInfo.IMAGE_SIZE_TYPE_HEIGHT;
    bool fitImage = true;
    int rotateAngle = 0;
    int autoMarginLimitH = 0;
    int autoMarginLimitV = 0;
    int autoMarginWhiteLevel = 100;
    float autoMarginPadding = 0;
    int autoMarginNombre = 0;
    float autoMarginNombreSize = 0.03f;
    int coverW = 600;
    int coverH = 800;
    float jpegQuality = 0.8f;
    float gammaValue = 0;
    bool navNest = false;
    bool ncxNest = false;
    bool isSvgImage = false;
    bool isKindle = false;
    string[] pageMargin = { "0", "0", "0", "0" };
    string[] bodyMargin = { "0", "0", "0", "0" };
    float lineHeight = 1.8f;
    int fontSize = 100;
    bool boldUseGothic = false;
    bool gothicUseBold = false;

    // ─── 内部状態 ─────────────────────────────────────────────────
    ZipArchive? zipArchive;
    StreamWriter? currentEntryWriter;

    int sectionIndex = 0;
    int imageIndex = 0;

    readonly List<SectionInfo> sectionInfos = new();
    readonly List<ChapterInfo> chapterInfos = new();
    readonly List<GaijiInfo> vecGaijiInfo = new();
    readonly HashSet<string> gaijiNameSet = new();
    readonly List<ImageInfo> imageInfos = new();
    readonly HashSet<string> outImageFileNames = new();

    ScriptObject? scriptObject;
    TemplateContext? templateContext;

    string templatePath;
    BookInfo? bookInfo;
    ImageInfoReader? imageInfoReader;
    bool canceled = false;

    /// <inheritdoc/>
    public Action<int>? ProgressCallback { get; set; }

    // ─── コンストラクタ ──────────────────────────────────────────
    public Epub3Writer(string templatePath)
    {
        this.templatePath = templatePath;
    }

    // ─── 設定メソッド ────────────────────────────────────────────
    public void SetImageParam(
        int dispW, int dispH, int coverW, int coverH,
        int resizeW, int resizeH,
        int singlePageSizeW, int singlePageSizeH, int singlePageWidth,
        int imageSizeType, bool fitImage, bool isSvgImage, int rotateAngle,
        float imageScale, int imageFloatType, int imageFloatW, int imageFloatH,
        float jpegQuality, float gamma,
        int autoMarginLimitH, int autoMarginLimitV, int autoMarginWhiteLevel,
        float autoMarginPadding, int autoMarginNombre, float nombreSize)
    {
        this.dispW = dispW; this.dispH = dispH;
        this.maxImageW = resizeW; this.maxImageH = resizeH;
        this.singlePageSizeW = singlePageSizeW;
        this.singlePageSizeH = singlePageSizeH;
        this.singlePageWidth = singlePageWidth;
        this.imageScale = imageScale;
        this.imageFloatType = imageFloatType;
        this.imageFloatW = imageFloatW; this.imageFloatH = imageFloatH;
        this.imageSizeType = imageSizeType;
        this.fitImage = fitImage; this.isSvgImage = isSvgImage;
        this.rotateAngle = rotateAngle;
        this.coverW = coverW; this.coverH = coverH;
        this.jpegQuality = jpegQuality;
        this.gammaValue = gamma;
        this.autoMarginLimitH = autoMarginLimitH;
        this.autoMarginLimitV = autoMarginLimitV;
        this.autoMarginWhiteLevel = autoMarginWhiteLevel;
        this.autoMarginPadding = autoMarginPadding;
        this.autoMarginNombre = autoMarginNombre;
        this.autoMarginNombreSize = nombreSize;
    }

    public void SetTocParam(bool navNest, bool ncxNest)
    {
        this.navNest = navNest;
        this.ncxNest = ncxNest;
    }

    public void SetStyles(string[] pageMargin, string[] bodyMargin, float lineHeight,
        int fontSize, bool boldUseGothic, bool gothicUseBold)
    {
        this.pageMargin = pageMargin; this.bodyMargin = bodyMargin;
        this.lineHeight = lineHeight; this.fontSize = fontSize;
        this.boldUseGothic = boldUseGothic; this.gothicUseBold = gothicUseBold;
    }

    public void Cancel() => canceled = true;
    public void SetIsKindle(bool isKindle) => this.isKindle = isKindle;
    public string GetGaijiFontPath()
    {
        // templatePath が設定されている場合はその親ディレクトリ配下の gaiji/ を使用
        // 例: templatePath = "/app/template/" → "/app/gaiji/"
        if (!string.IsNullOrEmpty(templatePath))
        {
            string trimmed = templatePath.TrimEnd('/', Path.DirectorySeparatorChar);
            string? parent = Path.GetDirectoryName(trimmed);
            if (parent != null)
                return Path.Combine(parent, "gaiji") + Path.DirectorySeparatorChar;
        }
        // templatePath 未設定（テスト等）→ AppContext.BaseDirectory 配下の gaiji/
        return Path.Combine(AppContext.BaseDirectory, "gaiji") + Path.DirectorySeparatorChar;
    }

    // ─── テンプレートヘルパー ─────────────────────────────────────
    void InitTemplateContext()
    {
        scriptObject = new ScriptObject();
        templateContext = new TemplateContext { MemberRenamer = member => member.Name };
        templateContext.PushGlobal(scriptObject);
    }

    void SetContextVar(string key, object? value) => scriptObject![key] = value;

    Stream GetTemplateInputStream(string fileName)
    {
        int idx = fileName.LastIndexOf('/');
        if (idx > 0)
        {
            string customPath = templatePath + fileName[..idx] + "_custom/" + fileName[(idx + 1)..];
            if (File.Exists(customPath)) return File.OpenRead(customPath);
        }
        string filePath = templatePath + fileName;
        if (File.Exists(filePath)) return File.OpenRead(filePath);

        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        // .NET replaces special chars like '-' with '_' in embedded resource names
        string resourceName = "AozoraEpub3.Core.Resources.template." + fileName.Replace('/', '.').Replace('-', '_');
        var stream = asm.GetManifestResourceStream(resourceName);
        if (stream != null) return stream;

        throw new IOException($"Template not found: {fileName}");
    }

    void WriteFileToZip(ZipArchive zip, string fileName)
    {
        var entry = zip.CreateEntry(fileName, CompressionLevel.Optimal);
        using var dst = entry.Open();
        using var src = GetTemplateInputStream(fileName);
        src.CopyTo(dst);
    }

    /// <summary>テンプレートを Scriban でレンダリングして ZIP エントリに書き込む</summary>
    void MergeTemplateToZip(ZipArchive zip, string entryName, string templateRelPath)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), leaveOpen: true);
        MergeTemplateToWriter(writer, templateRelPath);
    }

    void MergeTemplateToWriter(TextWriter writer, string templateRelPath)
    {
        string vmPath = templatePath + templateRelPath;
        string templateText = File.Exists(vmPath)
            ? File.ReadAllText(vmPath, Encoding.UTF8)
            : ReadEmbeddedTemplate(templateRelPath);

        var template = Template.Parse(templateText);
        if (template.HasErrors)
            throw new IOException($"Template parse error ({templateRelPath}): {template.Messages[0]}");

        string output = template.Render(templateContext);
        writer.Write(output);
    }

    string ReadEmbeddedTemplate(string relPath)
    {
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        string resourceName = "AozoraEpub3.Core.Resources.template." + relPath.Replace('/', '.').Replace('\\', '.');
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new IOException($"Embedded template not found: {relPath}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    // ─── Write() メインメソッド ───────────────────────────────────
    /// <summary>epubファイルを出力</summary>
    public void Write(AozoraEpub3Converter converter, TextReader src, string srcFile,
        string srcExt, string epubFile, BookInfo bookInfo, ImageInfoReader imageInfoReader)
    {
        FileStream? fs = null;
        try
        {
            canceled = false;
            this.bookInfo = bookInfo;
            this.imageInfoReader = imageInfoReader;
            sectionIndex = 0; imageIndex = 0;
            sectionInfos.Clear(); chapterInfos.Clear();
            vecGaijiInfo.Clear(); gaijiNameSet.Clear();
            imageInfos.Clear(); outImageFileNames.Clear();

            InitTemplateContext();

            string title = bookInfo.Title ?? "";
            string creator = bookInfo.Creator ?? "";
            if (creator == "") bookInfo.Creator = null;

            // 固有ID (MD5 ベースの deterministic UUID)
            byte[] idBytes = Encoding.UTF8.GetBytes(title + "-" + creator);
            byte[] md5 = MD5.HashData(idBytes);
            var guid = new Guid(md5);
            SetContextVar("identifier", guid.ToString());

            SetContextVar("cover_name", "表紙");
            SetContextVar("title", CharUtils.EscapeHtml(title));
            if (bookInfo.TitleAs != null) SetContextVar("titleAs", CharUtils.EscapeHtml(bookInfo.TitleAs));
            SetContextVar("creator", CharUtils.EscapeHtml(creator));
            if (bookInfo.CreatorAs != null) SetContextVar("creatorAs", CharUtils.EscapeHtml(bookInfo.CreatorAs));
            if (bookInfo.Publisher != null) SetContextVar("publisher", bookInfo.Publisher);
            SetContextVar("bookInfo", bookInfo);
            SetContextVar("modified", bookInfo.Modified.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));
            SetContextVar("navNest", navNest);
            if (isKindle) SetContextVar("kindle", true);
            if (isSvgImage) SetContextVar("svgImage", true);
            SetContextVar("pageMargin", pageMargin);
            SetContextVar("bodyMargin", bodyMargin);
            SetContextVar("lineHeight", lineHeight);
            SetContextVar("fontSize", fontSize);
            SetContextVar("boldUseGothic", boldUseGothic);
            SetContextVar("gothicUseBold", gothicUseBold);

            // ZIP 作成
            fs = new FileStream(epubFile, FileMode.Create, FileAccess.Write);
            zipArchive = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: true, Encoding.UTF8);

            // mimetype は非圧縮で先頭に
            {
                var mimeEntry = zipArchive.CreateEntry(MimetypePath, CompressionLevel.NoCompression);
                using var dst = mimeEntry.Open();
                using var tmplStream = GetTemplateInputStream(MimetypePath);
                tmplStream.CopyTo(dst);
            }

            // テンプレートファイルをコピー
            foreach (string fn in GetTemplateFiles())
                WriteFileToZip(zipArchive, fn);

            int archivePathLength = 0;
            if (bookInfo.TextEntryName != null)
                archivePathLength = bookInfo.TextEntryName.IndexOf('/') + 1;

            // 本文を出力 (SwitchableWriter 経由)
            var sw = new SwitchableWriter();
            WriteSections(converter, src, sw, bookInfo);
            if (canceled) return;

            // 外字 CSS
            SetContextVar("vecGaijiInfo", vecGaijiInfo);
            if (bookInfo.Vertical)
                MergeTemplateToZip(zipArchive, OpsPath + CssPath + VerticalTextCss,
                    OpsPath + CssPath + VerticalTextCssVm);
            else
                MergeTemplateToZip(zipArchive, OpsPath + CssPath + HorizontalTextCss,
                    OpsPath + CssPath + HorizontalTextCssVm);

            // タイトルページ
            if (!bookInfo.ImageOnly &&
                (bookInfo.TitlePageType == BookInfo.TITLE_MIDDLE ||
                 bookInfo.TitlePageType == BookInfo.TITLE_HORIZONTAL))
            {
                string vmRel = OpsPath + XhtmlPath +
                    (bookInfo.TitlePageType == BookInfo.TITLE_HORIZONTAL ? TitleHVm : TitleMVm);
                if (bookInfo.TitlePageType == BookInfo.TITLE_HORIZONTAL) converter.vertical = false;

                string? line;
                if ((line = bookInfo.GetTitleText()) != null)    SetContextVar("TITLE",     converter.ConvertTitleLineToEpub3(line));
                if ((line = bookInfo.GetSubTitleText()) != null) SetContextVar("SUBTITLE",  converter.ConvertTitleLineToEpub3(line));
                if ((line = bookInfo.GetOrgTitleText()) != null) SetContextVar("ORGTITLE",  converter.ConvertTitleLineToEpub3(line));
                if ((line = bookInfo.GetSubOrgTitleText()) != null) SetContextVar("SUBORGTITLE", converter.ConvertTitleLineToEpub3(line));
                if ((line = bookInfo.GetCreatorText()) != null)  SetContextVar("CREATOR",   converter.ConvertTitleLineToEpub3(line));
                if ((line = bookInfo.GetSubCreatorText()) != null) SetContextVar("SUBCREATOR", converter.ConvertTitleLineToEpub3(line));
                if ((line = bookInfo.GetSeriesText()) != null)   SetContextVar("SERIES",    converter.ConvertTitleLineToEpub3(line));
                if ((line = bookInfo.GetPublisherText()) != null) SetContextVar("PUBLISHER", converter.ConvertTitleLineToEpub3(line));

                MergeTemplateToZip(zipArchive, OpsPath + XhtmlPath + TitleFile, vmRel);
                SetContextVar("title_page", true);

                var titleLineInfo = bookInfo.GetChapterLineInfo(bookInfo.TitleLine);
                if (titleLineInfo != null)
                    chapterInfos.Insert(0, new ChapterInfo("title", null, bookInfo.Title, ChapterLineInfo.LEVEL_TITLE));
            }

            if (canceled) return;

            // 表紙画像処理
            byte[]? coverImageBytes = null;
            ImageInfo? coverImageInfo = null;

            if (!string.IsNullOrEmpty(bookInfo.CoverFileName))
            {
                try
                {
                    foreach (var ii in imageInfos) ii.IsCover = false;
                    byte[] buf;
                    if (bookInfo.CoverFileName!.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        using var http = new System.Net.Http.HttpClient();
                        buf = http.GetByteArrayAsync(bookInfo.CoverFileName).GetAwaiter().GetResult();
                    }
                    else buf = File.ReadAllBytes(bookInfo.CoverFileName);

                    coverImageBytes = buf;
                    using var ms = new MemoryStream(buf);
                    coverImageInfo = ImageInfo.GetImageInfo(ms);
                    if (coverImageInfo != null)
                    {
                        string ext = coverImageInfo.Ext;
                        if (isKindle || ext == "jpeg") ext = "jpg";
                        coverImageInfo.Id = "0000";
                        coverImageInfo.OutFileName = "0000." + ext;
                        if (!System.Text.RegularExpressions.Regex.IsMatch(ext, "^(png|jpg|jpeg|gif)$"))
                        {
                            LogAppender.Println("表紙画像フォーマットエラー: " + bookInfo.CoverFileName);
                            coverImageInfo = null;
                        }
                        else
                        {
                            coverImageInfo.IsCover = true;
                            imageInfos.Insert(0, coverImageInfo);
                        }
                    }
                }
                catch (Exception e) { LogAppender.Println($"表紙画像取得エラー: {e.Message}"); }
            }
            else if (bookInfo.CoverImageIndex > -1 && srcExt != "txt")
            {
                string? imageFileName = imageInfoReader.GetImageFileName(bookInfo.CoverImageIndex);
                if (imageFileName != null)
                {
                    var imageInfo = imageInfoReader.GetImageInfo(imageFileName);
                    if (imageInfo != null)
                    {
                        imageFileName = imageFileName[archivePathLength..];
                        outImageFileNames.Add(imageFileName);
                        foreach (var ii in imageInfos) ii.IsCover = false;
                        imageInfo.IsCover = true;
                        if (!imageInfos.Contains(imageInfo)) imageInfos.Add(imageInfo);
                    }
                }
            }

            // 表紙ページ出力
            if (bookInfo.InsertCoverPage)
            {
                ImageInfo? insertCoverInfo = coverImageInfo;
                if (insertCoverInfo == null && bookInfo.CoverImageIndex > -1)
                {
                    insertCoverInfo = imageInfoReader.GetImageInfo(bookInfo.CoverImageIndex);
                    if (insertCoverInfo != null)
                    {
                        insertCoverInfo.IsCover = true;
                        if (!bookInfo.ImageOnly && insertCoverInfo.Id == null)
                        {
                            imageIndex++;
                            string imageId = imageIndex.ToString("D4");
                            insertCoverInfo.Id = imageId;
                            string ext = isKindle ? "jpg" : insertCoverInfo.Ext;
                            insertCoverInfo.OutFileName = imageId + "." + ext;
                        }
                    }
                }
                if (insertCoverInfo != null)
                {
                    var sectionInfo = new SectionInfo("cover-page");
                    if (imageSizeType != SectionInfo.IMAGE_SIZE_TYPE_AUTO)
                    {
                        if ((double)insertCoverInfo.Width / insertCoverInfo.Height >= (double)coverW / coverH)
                            sectionInfo.ImageFitW = true;
                        else sectionInfo.ImageFitH = true;
                    }
                    SetContextVar("sectionInfo", sectionInfo);
                    SetContextVar("coverImage", insertCoverInfo);
                    MergeTemplateToZip(zipArchive, OpsPath + XhtmlPath + CoverFile,
                        OpsPath + XhtmlPath + CoverVm);
                }
                else bookInfo.InsertCoverPage = false;
            }

            // package.opf
            SetContextVar("sections", sectionInfos);
            SetContextVar("images", imageInfos);
            SetContextVar("vecGaijiInfo", vecGaijiInfo);
            MergeTemplateToZip(zipArchive, OpsPath + PackageFile, OpsPath + PackageVm);

            // null 除去
            for (int i = chapterInfos.Count - 1; i >= 0; i--)
                if (chapterInfos[i].ChapterName == null) chapterInfos.RemoveAt(i);

            // 表題レベルを2番目と揃える
            if (bookInfo.InsertTitleToc && chapterInfos.Count >= 2)
                chapterInfos[0].ChapterLevel = chapterInfos[1].ChapterLevel;

            // 目次階層情報設定
            int[] chapterCounts = new int[10];
            foreach (var ci in chapterInfos) chapterCounts[Math.Min(9, ci.ChapterLevel)]++;
            int[] newLevel = new int[10];
            int lv = 0;
            for (int i = 0; i < chapterCounts.Length; i++)
                if (chapterCounts[i] > 0) newLevel[i] = lv++;
            foreach (var ci in chapterInfos) ci.ChapterLevel = newLevel[Math.Min(9, ci.ChapterLevel)];

            // 開始終了情報 (nav 用)
            var preChInfo = new ChapterInfo("", null, null, 0);
            foreach (var ci in chapterInfos)
            {
                ci.LevelStart = Math.Max(0, ci.ChapterLevel - preChInfo.ChapterLevel);
                preChInfo.LevelEnd = Math.Max(0, preChInfo.ChapterLevel - ci.ChapterLevel);
                preChInfo = ci;
            }
            if (chapterInfos.Count > 0)
            {
                var last = chapterInfos[^1];
                last.LevelEnd = last.ChapterLevel;
            }

            int ncxDepth = 1;
            if (ncxNest && chapterInfos.Count > 0)
            {
                int minLevel = 99, maxLevel = 0;
                int[] navPointLevel = new int[10];
                ChapterInfo? preNcx = null;
                foreach (var ci in chapterInfos)
                {
                    if (preNcx != null)
                    {
                        int preLevel = preNcx.ChapterLevel;
                        int curLevel = ci.ChapterLevel;
                        minLevel = Math.Min(minLevel, curLevel);
                        maxLevel = Math.Max(maxLevel, curLevel);
                        navPointLevel[preLevel] = 1;
                        if (preLevel < curLevel)
                            preNcx.NavClose = 0;
                        else if (preLevel > curLevel)
                        {
                            int close = 0;
                            for (int i = curLevel; i < navPointLevel.Length; i++)
                                if (navPointLevel[i] == 1) { close++; navPointLevel[i] = 0; }
                            preNcx.NavClose = close;
                        }
                        else { preNcx.NavClose = 1; navPointLevel[preLevel] = 0; }
                    }
                    preNcx = ci;
                }
                if (minLevel < maxLevel) ncxDepth = maxLevel - minLevel + 1;
                var lastNcx = chapterInfos[^1];
                int closeCount = 1;
                foreach (var v in navPointLevel) if (v == 1) closeCount++;
                lastNcx.NavClose = closeCount;
            }
            SetContextVar("ncx_depth", ncxDepth);

            // 目次テキスト変換
            if (!bookInfo.ImageOnly)
            {
                bool prevVertical = converter.vertical;
                converter.vertical = bookInfo.TocVertical;
                int prevSpaceH = converter.GetSpaceHyphenation();
                converter.SetSpaceHyphenation(0);
                foreach (var ci in chapterInfos)
                {
                    string converted = CharUtils.EscapeHtml(ci.ChapterName ?? "");
                    if (bookInfo.TocVertical) converted = converter.ConvertTcyText(converted);
                    ci.ChapterName = converted;
                }
                converter.vertical = prevVertical;
                converter.SetSpaceHyphenation(prevSpaceH);
            }

            SetContextVar("chapters", chapterInfos);

            // nav.xhtml
            MergeTemplateToZip(zipArchive, OpsPath + XhtmlPath + XhtmlNavFile,
                OpsPath + XhtmlPath + XhtmlNavVm);
            // toc.ncx
            MergeTemplateToZip(zipArchive, OpsPath + TocFile, OpsPath + TocVm);

            if (!bookInfo.ImageOnly) ProgressCallback?.Invoke(bookInfo.TotalLineNum / 10);

            // フォントファイル格納
            if (!bookInfo.ImageOnly)
            {
                string fontsDir = templatePath + OpsPath + FontsPath;
                if (Directory.Exists(fontsDir))
                {
                    foreach (var fontFile in Directory.GetFiles(fontsDir))
                    {
                        string outFileName = OpsPath + FontsPath + Path.GetFileName(fontFile);
                        var fontEntry = zipArchive.CreateEntry(outFileName, CompressionLevel.NoCompression);
                        using var dst = fontEntry.Open();
                        using var fis = File.OpenRead(fontFile);
                        fis.CopyTo(dst);
                    }
                }
            }

            // 外字ファイル格納
            foreach (var gaijiInfo in vecGaijiInfo)
            {
                if (File.Exists(gaijiInfo.FilePath))
                {
                    string outFileName = OpsPath + GaijiPath + Path.GetFileName(gaijiInfo.FilePath);
                    var gaijiEntry = zipArchive.CreateEntry(outFileName, CompressionLevel.Optimal);
                    using var dst = gaijiEntry.Open();
                    using var fis = File.OpenRead(gaijiInfo.FilePath);
                    fis.CopyTo(dst);
                }
            }

            // 表紙画像出力
            if (coverImageInfo != null)
            {
                try
                {
                    string entryName = OpsPath + ImagesPath + coverImageInfo.OutFileName!;
                    var imgEntry = zipArchive.CreateEntry(entryName, CompressionLevel.NoCompression);
                    using var dst = imgEntry.Open();
                    if (coverImageBytes != null)
                    {
                        using var ms = new MemoryStream(coverImageBytes);
                        WriteCoverImageToStream(ms, dst, coverImageInfo);
                    }
                    imageInfos.Remove(coverImageInfo);
                    ProgressCallback?.Invoke(10);
                }
                catch (Exception e) { LogAppender.Println($"表紙画像取得エラー: {e.Message}"); }
            }

            if (canceled) return;

            // 本文画像出力
            if (srcExt == "txt")
            {
                foreach (string srcImageFileName in imageInfoReader.GetImageFileNames())
                {
                    string corrected = imageInfoReader.CorrectExt(srcImageFileName) ?? srcImageFileName;
                    if (outImageFileNames.Contains(corrected))
                    {
                        var imageInfo = imageInfoReader.GetImageInfo(corrected);
                        if (imageInfo == null)
                            LogAppender.Println($"[WARN] 画像ファイルなし: {corrected}");
                        else
                        {
                            string imageFile;
                            try { imageFile = imageInfoReader.GetImageFilePathSafe(corrected); }
                            catch (IOException) { LogAppender.Println($"[WARN] 画像パスが不正のためスキップ: {corrected}"); continue; }
                            if (File.Exists(imageFile))
                            {
                                string outName = OpsPath + ImagesPath + imageInfo.OutFileName!;
                                var imgEntry = zipArchive.CreateEntry(outName, CompressionLevel.NoCompression);
                                using var dst = imgEntry.Open();
                                using var fis = File.OpenRead(imageFile);
                                WriteImageToStream(fis, dst, imageInfo);
                                outImageFileNames.Remove(corrected);
                            }
                        }
                    }
                    if (canceled) return;
                }
            }
            else if (!bookInfo.ImageOnly)
            {
                if (srcExt == "rar")
                {
                    using var archive = RarArchive.OpenArchive(new FileInfo(srcFile), null);
                    foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                    {
                        string entryName = SanitizeArchiveEntryName(entry.Key!.Replace('\\', '/'));
                        string srcImageFileName = entryName[archivePathLength..];
                        if (outImageFileNames.Contains(srcImageFileName))
                        {
                            using var is2 = entry.OpenEntryStream();
                            WriteArchiveImage(srcImageFileName, is2);
                        }
                    }
                }
                else
                {
                    // ZIP: Shift-JIS エンコーディングで読み込み
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    using var zis = new ZipArchive(
                        new FileStream(srcFile, FileMode.Open, FileAccess.Read),
                        ZipArchiveMode.Read, leaveOpen: false,
                        Encoding.GetEncoding("shift_jis"));
                    foreach (var entry in zis.Entries)
                    {
                        try
                        {
                            string entryName = SanitizeArchiveEntryName(entry.FullName.Replace('\\', '/'));
                            string srcImageFileName = entryName[archivePathLength..];
                            if (outImageFileNames.Contains(srcImageFileName))
                            {
                                using var is2 = entry.Open();
                                WriteArchiveImage(srcImageFileName, is2);
                            }
                        }
                        catch (ArgumentException e)
                        {
                            Console.Error.WriteLine($"Skipping suspicious archive entry: {e.Message}");
                        }
                    }
                }
            }

            ProgressCallback?.Invoke(100);
        }
        catch (Exception e) { LogAppender.Println($"[ERROR] {e}"); }
        finally
        {
            zipArchive?.Dispose();  // flushes ZIP (leaveOpen: true, so fs stays open)
            zipArchive = null;
            fs?.Dispose();          // close the underlying FileStream
            templateContext = null;
            scriptObject = null;
            this.bookInfo = null;
            this.imageInfoReader = null;
        }
    }

    void WriteSections(AozoraEpub3Converter converter, TextReader src, SwitchableWriter sw, BookInfo bookInfo)
    {
        converter.vertical = bookInfo.Vertical;
        converter.ConvertTextToEpub3(sw, src, bookInfo);
        sw.Flush();
        EndSection();
    }

    // ─── セクション管理 ───────────────────────────────────────────
    /// <inheritdoc/>
    public void NextSection(TextWriter bw, int lineNum, int pageType, int imagePageType, string? srcImageFilePath)
    {
        if (sectionIndex > 0)
        {
            bw.Flush();
            EndSection();
        }
        StartSection(lineNum, pageType, imagePageType, srcImageFilePath, (SwitchableWriter)bw);
    }

    void StartSection(int lineNum, int pageType, int imagePageType, string? srcImageFilePath, SwitchableWriter sw)
    {
        sectionIndex++;
        string sectionId = sectionIndex.ToString("D4");
        var sectionInfo = new SectionInfo(sectionId);

        switch (imagePageType)
        {
            case PageBreakType.IMAGE_PAGE_W:
                sectionInfo.ImagePage = true; sectionInfo.ImageFitW = true; break;
            case PageBreakType.IMAGE_PAGE_H:
                sectionInfo.ImagePage = true; sectionInfo.ImageFitH = true; break;
            case PageBreakType.IMAGE_PAGE_NOFIT:
                sectionInfo.ImagePage = true; break;
        }
        if (pageType == PageBreakType.PAGE_MIDDLE) sectionInfo.Middle = true;
        else if (pageType == PageBreakType.PAGE_BOTTOM) sectionInfo.Bottom = true;
        sectionInfos.Add(sectionInfo);

        // ZIP エントリ作成
        var zipEntry = zipArchive!.CreateEntry(OpsPath + XhtmlPath + sectionId + ".xhtml", CompressionLevel.Optimal);
        var entryStream = zipEntry.Open();
        currentEntryWriter = new StreamWriter(entryStream, new System.Text.UTF8Encoding(false), leaveOpen: false);

        // ヘッダ出力
        SetContextVar("sectionInfo", sectionInfo);
        MergeTemplateToWriter(currentEntryWriter, OpsPath + XhtmlPath + XhtmlHeaderVm);
        currentEntryWriter.Flush();

        // SwitchableWriter を新エントリに向ける
        sw.SwitchTo(currentEntryWriter);
    }

    void EndSection()
    {
        if (currentEntryWriter == null) return;
        MergeTemplateToWriter(currentEntryWriter, OpsPath + XhtmlPath + XhtmlFooterVm);
        currentEntryWriter.Flush();
        currentEntryWriter.Dispose();
        currentEntryWriter = null;
    }

    /// <inheritdoc/>
    public void AddChapter(string? chapterId, string name, int chapterLevel)
    {
        var sectionInfo = sectionInfos[^1];
        chapterInfos.Add(new ChapterInfo(sectionInfo.SectionId, chapterId, name, chapterLevel));
    }

    /// <inheritdoc/>
    public void AddGaijiFont(string className, string gaijiFilePath)
    {
        if (gaijiNameSet.Contains(className)) return;
        vecGaijiInfo.Add(new GaijiInfo(className, gaijiFilePath));
        gaijiNameSet.Add(className);
    }

    // ─── 画像処理 ─────────────────────────────────────────────────
    /// <inheritdoc/>
    public string? GetImageFilePath(string srcImageFileName, int lineNum)
    {
        var imageInfo = imageInfoReader!.GetImageInfo(srcImageFileName);
        if (imageInfo == null)
        {
            string? alt = imageInfoReader.CorrectExt(srcImageFileName);
            if (alt != null) imageInfo = imageInfoReader.GetImageInfo(alt);
            if (imageInfo != null)
            {
                LogAppender.Warn(lineNum, "画像拡張子変更", srcImageFileName);
                srcImageFileName = alt!;
            }
        }

        imageIndex++;
        if (imageInfo != null)
        {
            string? imageId = imageInfo.Id;
            bool isCover = false;
            if (imageId == null)
            {
                imageId = imageIndex.ToString("D4");
                imageInfos.Add(imageInfo);
                outImageFileNames.Add(srcImageFileName);
                if (imageIndex - 1 == bookInfo!.CoverImageIndex) isCover = true;
            }
            string outImageFileName = imageId + "." + imageInfo.Ext.Replace("jpeg", "jpg");
            imageInfo.Id = imageId;
            imageInfo.OutFileName = outImageFileName;

            if (bookInfo!.InsertCoverPage && isCover) return null;
            return "../" + ImagesPath + outImageFileName;
        }
        else
        {
            LogAppender.Warn(lineNum, "画像ファイルなし", srcImageFileName);
        }
        return null;
    }

    /// <inheritdoc/>
    public bool IsCoverImage() => imageIndex == bookInfo!.CoverImageIndex;

    /// <inheritdoc/>
    public int GetImageIndex() => imageIndex;

    void WriteArchiveImage(string srcImageFileName, Stream srcStream)
    {
        string corrected = imageInfoReader!.CorrectExt(srcImageFileName) ?? srcImageFileName;
        var imageInfo = imageInfoReader.GetImageInfo(corrected);
        if (imageInfo?.Id == null) return;

        // 回転チェック
        if ((double)imageInfo.Width / imageInfo.Height >= (double)dispW / dispH)
        {
            if (rotateAngle != 0 && dispW < dispH &&
                (double)imageInfo.Height / imageInfo.Width < (double)dispW / dispH)
                imageInfo.RotateAngle = rotateAngle;
        }
        else
        {
            if (rotateAngle != 0 && dispW > dispH &&
                (double)imageInfo.Height / imageInfo.Width > (double)dispW / dispH)
                imageInfo.RotateAngle = rotateAngle;
        }

        // 一旦バイト配列に読み込む (Zip/Rar からの直接読み込みは不安定)
        using var ms = new MemoryStream();
        srcStream.CopyTo(ms);
        ms.Position = 0;

        string outName = OpsPath + ImagesPath + imageInfo.OutFileName!;
        var imgEntry = zipArchive!.CreateEntry(outName, CompressionLevel.NoCompression);
        using var dst = imgEntry.Open();
        WriteImageToStream(ms, dst, imageInfo);

        if (canceled) return;
        ProgressCallback?.Invoke(10);
    }

    void WriteCoverImageToStream(Stream src, Stream dst, ImageInfo imageInfo)
    {
        imageInfo.RotateAngle = 0;
        WriteImageCore(src, dst, imageInfo, coverW, coverH, 0, 0, 0);
    }

    void WriteImageToStream(Stream src, Stream dst, ImageInfo imageInfo)
    {
        WriteImageCore(src, dst, imageInfo, 0, 0, maxImageW, maxImageH, maxImagePixels);
    }

    void WriteImageCore(Stream src, Stream dst, ImageInfo imageInfo,
        int fitW, int fitH, int maxW, int maxH, int maxPixels)
    {
        try
        {
            using var img = SixLabors.ImageSharp.Image.Load<Rgba32>(src);

            // 回転
            if (imageInfo.RotateAngle == 90)             img.Mutate(x => x.Rotate(RotateMode.Rotate90));
            else if (imageInfo.RotateAngle is -90 or 270) img.Mutate(x => x.Rotate(RotateMode.Rotate270));

            int w = img.Width, h = img.Height;
            int targetW = w, targetH = h;

            if (fitW > 0 && fitH > 0)
            {
                // 表紙: 指定サイズ内に収める
                double scale = Math.Min((double)fitW / w, (double)fitH / h);
                if (scale < 1) { targetW = (int)(w * scale); targetH = (int)(h * scale); }
            }
            else
            {
                if (maxPixels >= 10000 && w * h > maxPixels)
                {
                    double scale = Math.Sqrt((double)maxPixels / (w * h));
                    targetW = (int)(w * scale); targetH = (int)(h * scale);
                }
                if (maxW > 0 && targetW > maxW)
                {
                    double scale = (double)maxW / targetW;
                    targetW = maxW; targetH = (int)(targetH * scale);
                }
                if (maxH > 0 && targetH > maxH)
                {
                    double scale = (double)maxH / targetH;
                    targetH = maxH; targetW = (int)(targetW * scale);
                }
            }

            if (targetW != w || targetH != h)
                img.Mutate(x => x.Resize(targetW, targetH));

            // ガンマ補正 (ルックアップテーブル方式)
            if (gammaValue > 0 && gammaValue != 1)
            {
                byte[] table = new byte[256];
                for (int i = 0; i < 256; i++)
                    table[i] = (byte)Math.Min(255, (int)Math.Round(255 * Math.Pow(i / 255.0, 1.0 / gammaValue)));
                img.ProcessPixelRows(accessor =>
                {
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var row = accessor.GetRowSpan(y);
                        for (int x2 = 0; x2 < row.Length; x2++)
                        {
                            var p = row[x2];
                            row[x2] = new Rgba32(table[p.R], table[p.G], table[p.B], p.A);
                        }
                    }
                });
            }

            // 出力
            string ext = imageInfo.Ext;
            if (ext is "jpg" or "jpeg" or "jpe")
                img.Save(dst, new JpegEncoder { Quality = (int)(jpegQuality * 100) });
            else
                img.Save(dst, new PngEncoder());

            // 出力サイズを記録
            imageInfo.OutWidth  = targetW;
            imageInfo.OutHeight = targetH;
        }
        catch (Exception e)
        {
            LogAppender.Println($"[WARN] 画像処理エラー: {e.Message}");
            src.CopyTo(dst);
        }
    }

    // ─── 画像ページ判定 ───────────────────────────────────────────
    /// <inheritdoc/>
    public int GetImagePageType(string srcFilePath, int tagLevel, int lineNum, bool hasCaption)
    {
        try
        {
            var imageInfo = imageInfoReader!.GetImageInfo(srcFilePath);
            if (imageInfo == null)
                imageInfo = imageInfoReader.GetImageInfo(imageInfoReader.CorrectExt(srcFilePath) ?? srcFilePath);
            if (imageInfo == null) return PageBreakType.IMAGE_PAGE_NONE;

            float imageOrgW = imageInfo.Width;
            float imageOrgH = imageInfo.Height;
            float imageW = imageOrgW;
            float imageH = imageOrgH;
            if (imageScale > 0) { imageW *= imageScale; imageH *= imageScale; }

            // 回り込みサイズ以下
            if (imageFloatType != 0 &&
                (imageOrgW >= 64 || imageOrgH >= 64) &&
                imageOrgW <= imageFloatW && imageOrgH <= imageFloatH)
            {
                if (imageFloatType == 1)
                    return imageW > dispW ? PageBreakType.IMAGE_INLINE_TOP_W : PageBreakType.IMAGE_INLINE_TOP;
                else
                    return imageW > dispW ? PageBreakType.IMAGE_INLINE_BOTTOM_W : PageBreakType.IMAGE_INLINE_BOTTOM;
            }

            // 単ページ化判定
            if (imageOrgW >= singlePageWidth || (imageOrgW >= singlePageSizeW && imageOrgH >= singlePageSizeH))
            {
                if (tagLevel == 0)
                {
                    if (!hasCaption)
                    {
                        if (imageW <= dispW && imageH < dispH)
                        {
                            if (!fitImage) return PageBreakType.IMAGE_PAGE_NOFIT;
                        }
                        else if (imageSizeType == SectionInfo.IMAGE_SIZE_TYPE_AUTO)
                            return PageBreakType.IMAGE_PAGE_NOFIT;

                        if (imageW / imageH > (double)dispW / dispH)
                        {
                            if (rotateAngle != 0 && dispW < dispH && imageW > imageH * 1.1)
                            {
                                imageInfo.RotateAngle = rotateAngle;
                                return imageH / imageW > (double)dispW / dispH
                                    ? PageBreakType.IMAGE_PAGE_W : PageBreakType.IMAGE_PAGE_H;
                            }
                            return PageBreakType.IMAGE_PAGE_W;
                        }
                        else
                        {
                            if (rotateAngle != 0 && dispW > dispH && imageW * 1.1 < imageH)
                            {
                                imageInfo.RotateAngle = rotateAngle;
                                return imageH / imageW > (double)dispW / dispH
                                    ? PageBreakType.IMAGE_PAGE_W : PageBreakType.IMAGE_PAGE_H;
                            }
                            return PageBreakType.IMAGE_PAGE_H;
                        }
                    }
                    else LogAppender.Warn(lineNum, "キャプションがあるため画像単ページ化されません");
                }
                else LogAppender.Warn(lineNum, "タグ内のため画像単ページ化できません");
            }

            // インライン画像サイズ超過
            if (imageW > dispW)
                return imageW / imageH > (double)dispW / dispH
                    ? PageBreakType.IMAGE_INLINE_W : PageBreakType.IMAGE_INLINE_H;
            if (imageH > dispH) return PageBreakType.IMAGE_INLINE_H;
        }
        catch (Exception e) { LogAppender.Println($"[WARN] GetImagePageType: {e.Message}"); }
        return PageBreakType.IMAGE_PAGE_NONE;
    }

    /// <inheritdoc/>
    public double GetImageWidthRatio(string srcFilePath, bool hasCaption)
    {
        if (imageScale == 0) return 0;
        double ratio = 0;
        try
        {
            var imageInfo = imageInfoReader!.GetImageInfo(srcFilePath);
            if (imageInfo != null)
            {
                if (bookInfo!.Vertical) { if (imageInfo.Width <= 64) return -1; }
                else if (imageInfo.Height <= 64) return -1;

                int imgW = imageInfo.Width, imgH = imageInfo.Height;
                if (imageInfo.RotateAngle is 90 or 270) { imgW = imageInfo.Height; imgH = imageInfo.Width; }

                double wRatio = (double)imgW / dispW * imageScale * 100;
                double hRatio = (double)imgH / dispH * imageScale * 100;
                if (hasCaption) { if (hRatio >= 90) { wRatio *= 100 / hRatio; wRatio *= 0.9; } }
                else if (hRatio >= 100) wRatio *= 100 / hRatio;
                ratio = wRatio;
            }
        }
        catch (Exception e) { LogAppender.Println($"[WARN] GetImageWidthRatio: {e.Message}"); }
        return Math.Min(100, ratio);
    }

    // ─── ユーティリティ ───────────────────────────────────────────
    static string SanitizeArchiveEntryName(string entryName)
    {
        if (string.IsNullOrEmpty(entryName))
            throw new ArgumentException("Entry name cannot be null or empty");
        if (entryName[0] is '/' or '\\')
            throw new ArgumentException($"Absolute path detected in archive entry: {entryName}");
        if (entryName.Contains("..") || entryName.Contains('~') || entryName.Contains('$'))
            throw new ArgumentException($"Path traversal attempt detected: {entryName}");
        string normalized = entryName.Replace('\\', '/');
        while (normalized.Contains("//")) normalized = normalized.Replace("//", "/");
        return normalized;
    }

    // ─── SwitchableWriter ─────────────────────────────────────────
    /// <summary>切り替え可能な TextWriter。ZIP エントリ切り替え時に内部 writer を差し替える。</summary>
    sealed class SwitchableWriter : TextWriter
    {
        private TextWriter? _current;
        public override Encoding Encoding => _current?.Encoding ?? Encoding.UTF8;
        public void SwitchTo(TextWriter? writer) => _current = writer;
        public override void Write(char value) => _current?.Write(value);
        public override void Write(string? value) => _current?.Write(value);
        public override void WriteLine(string? value) => _current?.WriteLine(value);
        public override void Write(char[] buffer, int index, int count) => _current?.Write(buffer, index, count);
        public override void Flush() => _current?.Flush();
        protected override void Dispose(bool disposing) { /* 内部 writer は呼び出し元が管理 */ }
    }
}
