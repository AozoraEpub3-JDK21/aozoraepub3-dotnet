using System.IO;
using System.Text;
using AozoraEpub3.Core.Converter;
using AozoraEpub3.Core.Editor;
using AozoraEpub3.Core.Info;
using AozoraEpub3.Core.Io;

namespace AozoraEpub3.Gui.Services;

/// <summary>
/// エディタテキスト → XHTML 変換サービス。
/// ハイブリッド記法テキストを青空文庫記法に変換し、
/// AozoraEpub3Converter で XHTML body を生成する。
/// </summary>
public sealed class LivePreviewService
{
    private BookInfo? _cachedBookInfo;
    private readonly EditorConversionEngine _engine;

    public LivePreviewService(ConversionProfile profile)
    {
        _engine = new EditorConversionEngine(profile);
    }

    /// <summary>
    /// エディタテキストを縦書き XHTML に変換する。
    /// </summary>
    public string ConvertToXhtml(string editorText, bool vertical = true)
    {
        if (string.IsNullOrWhiteSpace(editorText))
            return BuildXhtml("", vertical);
        var aozoraText = _engine.FormatAndConvert(editorText);
        var bodyHtml = ConvertAozoraToXhtmlBody(aozoraText);
        return BuildXhtml(bodyHtml, vertical);
    }

    /// <summary>Lint 警告のみ取得する。</summary>
    public IReadOnlyList<LintWarning> GetLintWarnings(string editorText)
        => _engine.GetLintWarnings(editorText);

    /// <summary>変換プロファイルを変更して新しいインスタンスを返す。</summary>
    public static LivePreviewService Create(ConversionProfile profile) => new(profile);

    private string ConvertAozoraToXhtmlBody(string aozoraText)
    {
        var fullText = "プレビュー\n著者\n\n" + aozoraText;
        var previewWriter = new PreviewWriter();
        var converter = new AozoraEpub3Converter(previewWriter, "");

        BookInfo bookInfo;
        using (var reader1 = new StringReader(fullText))
        {
            var imgReader = new ImageInfoReader(true, "");
            bookInfo = converter.GetBookInfo("preview.txt", reader1, imgReader,
                BookInfo.TitleType.TITLE_AUTHOR, false);
        }
        bookInfo.Vertical = true;
        bookInfo.TitlePageType = BookInfo.TITLE_NONE;
        _cachedBookInfo = bookInfo;

        var bodyWriter = new StringWriter();
        using var reader2 = new StringReader(fullText);
        converter.ConvertTextToEpub3(bodyWriter, reader2, bookInfo);

        var body = bodyWriter.ToString();
        if (body.StartsWith("<p><br/></p>\n"))
            body = body.Substring("<p><br/></p>\n".Length);
        return body;
    }

    private static string BuildXhtml(string bodyContent, bool vertical)
    {
        var writingMode = vertical ? "vertical-rl" : "horizontal-tb";
        return $$"""
            <!DOCTYPE html>
            <html xmlns="http://www.w3.org/1999/xhtml" xml:lang="ja" lang="ja">
            <head>
            <meta charset="utf-8"/>
            <title>Preview</title>
            <style type="text/css">
            @page { margin: 1em; }
            html {
                writing-mode: {{writingMode}};
                -webkit-writing-mode: {{writingMode}};
                background-color: #ffffff;
            }
            body {
                font-family: "ＭＳ 明朝", "Yu Mincho", serif;
                font-size: 16px;
                line-height: 1.8;
                margin: 0;
                padding: 2em 3em;
                background-color: #ffffff;
                color: #1a1a1a;
            }
            p { text-indent: 1em; margin: 0; }
            .bold { font-weight: bold; }
            .indent_1 { margin-left: 1em; }
            .indent_2 { margin-left: 2em; }
            .indent_3 { margin-left: 3em; }
            h1, h2, h3 { font-weight: bold; }
            h1 { font-size: 1.6em; }
            h2 { font-size: 1.4em; }
            h3 { font-size: 1.2em; }
            ruby { ruby-align: center; }
            .tcy span {
                text-combine-upright: all;
                -webkit-text-combine: horizontal;
            }
            .sesame { text-emphasis-style: sesame; -webkit-text-emphasis-style: sesame; }
            .introduction { margin-bottom: 1em; padding: 0.5em; border-left: 3px solid #ccc; }
            .postscript { margin-top: 1em; padding: 0.5em; border-left: 3px solid #ccc; }
            </style>
            </head>
            <body>
            {{bodyContent}}
            </body>
            </html>
            """;
    }

    /// <summary>
    /// ライブプレビュー用の軽量 IEpub3Writer 実装。
    /// EPUB 出力は行わず、変換処理のコールバックを受け流すだけ。
    /// </summary>
    private sealed class PreviewWriter : IEpub3Writer
    {
        public Action<int>? ProgressCallback { get; set; }
        public string GetGaijiFontPath() => "";
        public string? GetImageFilePath(string srcImageFileName, int lineNum) => null;
        public bool IsCoverImage() => false;
        public int GetImageIndex() => -1;
        public int GetImagePageType(string srcFilePath, int tagLevel, int lineNum, bool hasCaption)
            => PageBreakType.IMAGE_PAGE_NONE;
        public double GetImageWidthRatio(string srcFilePath, bool hasCaption) => 1.0;
        public void NextSection(TextWriter bw, int lineNum, int pageType, int imagePageType, string? srcImageFilePath)
        {
            // プレビューではセクション切り替えなし — 全セクションを連続出力
        }
        public void AddChapter(string? chapterId, string name, int chapterLevel) { }
        public void AddGaijiFont(string className, string gaijiFilePath) { }
    }
}
