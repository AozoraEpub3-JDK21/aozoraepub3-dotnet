using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp;
using AngleSharp.Dom;
using AozoraEpub3.Core.Converter;

namespace AozoraEpub3.Core.Web;

/// <summary>
/// HTMLページを青空文庫テキスト行リストに変換するコンバーター。
/// Java版 WebAozoraConverter の in-memory 移植版。
/// Jsoup の代わりに AngleSharp を使用する。
/// </summary>
public class WebAozoraConverter
{
    // プレースホルダー文字（注記生成中に使用、最後に実文字に置換）
    private const char PhOpen = '\uE000';   // ［
    private const char PhClose = '\uE001';  // ］
    private const char PhHash = '\uE002';   // ＃

    private static string PhChuki(string content) =>
        $"{PhOpen}{PhHash}{content}{PhClose}";

    private static string RestorePlaceholders(string text) =>
        text.Replace(PhOpen, '［').Replace(PhClose, '］').Replace(PhHash, '＃');

    // ─── HTTP ──────────────────────────────────────────────────
    private static readonly HttpClient _http = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        AllowAutoRedirect = true
    })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static WebAozoraConverter()
    {
        _http.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,*/*");
        _http.DefaultRequestHeaders.Add("Accept-Language", "ja,en-US;q=0.9");
    }

    // ─── インスタンスフィールド ─────────────────────────────────
    private readonly Dictionary<ExtractId, ExtractInfo[]> _queryMap;
    private readonly Dictionary<ExtractId, List<string[]>> _replaceMap;
    private readonly NarouFormatSettings _settings;
    private string _baseUri = "";
    private string _pageBaseUri = "";
    private int _interval = 700;
    private string? _dstPath;

    /// <summary>ダウンロード待ち画像リスト (srcUrl, localRelPath)</summary>
    private readonly List<(string url, string localPath)> _pendingImageDownloads = new();

    /// <summary>ダウンロード失敗した話のURL</summary>
    private readonly List<string> _failedHrefs = new();

    /// <summary>表紙画像のローカルパス（ダウンロード成功時にセット）</summary>
    public string? CoverImagePath { get; private set; }

    private WebAozoraConverter(
        Dictionary<ExtractId, ExtractInfo[]> queryMap,
        Dictionary<ExtractId, List<string[]>> replaceMap,
        NarouFormatSettings settings)
    {
        _queryMap = queryMap;
        _replaceMap = replaceMap;
        _settings = settings;
    }

    // ─── パブリック API ─────────────────────────────────────────

    /// <summary>
    /// 指定URLのWebページを青空文庫テキスト行リストに変換する。
    /// configPath/{fqdn}/extract.txt が存在するサイトのみ対応。
    /// </summary>
    public static async Task<List<string>?> ConvertToAozoraLinesAsync(
        string urlString,
        string configPath,
        NarouFormatSettings settings,
        int interval = 700,
        string? dstPath = null,
        CancellationToken ct = default)
    {
        urlString = urlString.Trim();

        // FQDN と baseUri を抽出
        int protoEnd = urlString.IndexOf("//", StringComparison.Ordinal) + 2;
        int pathStart = urlString.IndexOf('/', protoEnd);
        if (pathStart < 0) pathStart = urlString.Length;
        string fqdn = urlString[protoEnd..pathStart];
        string baseUri = urlString[..pathStart];

        var queryMap = LoadQueryMap(fqdn, configPath);
        if (queryMap.Count == 0)
        {
            LogAppender.Println($"サイトの定義がありません: {configPath}/{fqdn}/extract.txt");
            return null;
        }

        var replaceMap = LoadReplaceMap(fqdn, configPath);
        var converter = new WebAozoraConverter(queryMap, replaceMap, settings)
        {
            _interval = Math.Max(500, interval),
            _baseUri = baseUri,
            _dstPath = dstPath
        };

        return await converter.ConvertAsync(urlString, ct);
    }

    /// <summary>
    /// 変換結果とコンバーターインスタンスを返す版。
    /// コンバーターから表紙画像パス等のメタ情報を取得するため。
    /// </summary>
    public static async Task<(List<string>? lines, WebAozoraConverter? converter)> ConvertToAozoraLinesWithConverterAsync(
        string urlString,
        string configPath,
        NarouFormatSettings settings,
        int interval = 700,
        string? dstPath = null,
        CancellationToken ct = default)
    {
        urlString = urlString.Trim();

        int protoEnd = urlString.IndexOf("//", StringComparison.Ordinal) + 2;
        int pathStart = urlString.IndexOf('/', protoEnd);
        if (pathStart < 0) pathStart = urlString.Length;
        string fqdn = urlString[protoEnd..pathStart];
        string baseUri = urlString[..pathStart];

        var queryMap = LoadQueryMap(fqdn, configPath);
        if (queryMap.Count == 0)
        {
            LogAppender.Println($"サイトの定義がありません: {configPath}/{fqdn}/extract.txt");
            return (null, null);
        }

        var replaceMap = LoadReplaceMap(fqdn, configPath);
        var converter = new WebAozoraConverter(queryMap, replaceMap, settings)
        {
            _interval = Math.Max(500, interval),
            _baseUri = baseUri,
            _dstPath = dstPath
        };

        var lines = await converter.ConvertAsync(urlString, ct);
        return (lines, converter);
    }

    // ─── 内部変換ロジック ───────────────────────────────────────

    private async Task<List<string>?> ConvertAsync(string urlString, CancellationToken ct)
    {
        // listBaseUrl = urlString の最後の '/' まで
        string listBaseUrl = urlString.EndsWith("/")
            ? urlString
            : urlString[..(urlString.LastIndexOf('/') + 1)];

        // TOCページダウンロード
        LogAppender.Append(urlString);
        string tocHtml;
        try
        {
            tocHtml = await DownloadHtmlAsync(urlString, null, ct);
            LogAppender.Println(" : List Loaded.");
        }
        catch (Exception e)
        {
            LogAppender.Println($" 失敗: {e.Message}");
            return null;
        }

        var doc = await ParseAsync(tocHtml);
        var lines = new List<string>(1024);

        // ── ヘッダ ──
        string? series = GetExtractText(doc, _queryMap.GetValueOrDefault(ExtractId.SERIES));
        string? title = GetExtractText(doc, _queryMap.GetValueOrDefault(ExtractId.TITLE));
        if (title == null) title = series;
        if (title == null)
        {
            LogAppender.Println("TITLE: タイトルが取得できません");
            return null;
        }
        string? author = GetExtractText(doc, _queryMap.GetValueOrDefault(ExtractId.AUTHOR));

        if (series != null) lines.Add(series);
        lines.Add(title);
        if (author != null) lines.Add(author);
        lines.Add("");

        // 表紙画像
        string? coverLocalPath = null;
        var coverElems = GetExtractElements(doc, _queryMap.GetValueOrDefault(ExtractId.COVER_IMG));
        if (coverElems?.Length > 0)
        {
            string? coverSrc = coverElems[0].GetAttribute("src");
            if (!string.IsNullOrEmpty(coverSrc))
            {
                if (_dstPath != null)
                {
                    coverLocalPath = Path.Combine(_dstPath, "cover.jpg");
                    // 表紙はPrintImageのURL解決のみ使い、保存先は直接指定
                    string? coverSrcUrl = coverElems[0].GetAttribute("src");
                    if (!string.IsNullOrEmpty(coverSrcUrl))
                    {
                        string resolvedUrl = ResolveImageUrl(coverSrcUrl);
                        _pendingImageDownloads.Add((resolvedUrl, coverLocalPath));
                    }
                }
                lines.Add("［＃挿絵（cover.jpg）入る］");
                lines.Add("");
            }
        }

        // 掲載URL
        if (_settings.IncludeTocUrl)
        {
            lines.Add("［＃区切り線］");
            lines.Add("");
            lines.Add("掲載ページ：");
            lines.Add($"<a href=\"{urlString}\">{urlString}</a>");
            lines.Add("［＃区切り線］");
            lines.Add("");
        }

        // あらすじ
        if (_settings.IncludeStory)
        {
            var descElem = GetExtractFirstElement(doc, _queryMap.GetValueOrDefault(ExtractId.DESCRIPTION));
            if (descElem != null)
            {
                lines.Add("あらすじ：");
                lines.Add("［＃ここから２字下げ］");
                lines.Add("［＃ここから２字上げ］");
                var descLines = new List<string>();
                var sb = new StringBuilder();
                PrintNodeInternal(descLines, sb, descElem, null, null, true);
                FlushBuffer(descLines, sb);
                lines.AddRange(descLines);
                lines.Add("［＃ここで字上げ終わり］");
                lines.Add("［＃ここで字下げ終わり］");
                lines.Add("");
                lines.Add("［＃区切り線］");
                lines.Add("");
            }
        }

        // ── 各話URLリスト取得 ──
        var hrefElems = GetExtractElements(doc, _queryMap.GetValueOrDefault(ExtractId.HREF));
        if (hrefElems == null)
        {
            // HREF なし → 単一ページ作品
            var contentElems = GetExtractElements(doc, _queryMap.GetValueOrDefault(ExtractId.CONTENT_ARTICLE));
            if (contentElems != null)
            {
                DocToAozoraLines(doc, lines, newChapter: false,
                    listSubTitle: null, postDate: null, publishDate: null);
            }
            else
            {
                LogAppender.Println("HREF: 各話のリンク先URLが取得できません");
                return null;
            }
        }
        else
        {
            // ── TOC ページネーション (B7: Java版互換) ──
            // PAGE_URL が定義されていれば最後のページリンクから総ページ数を取得して残りの目次ページを収集
            if (_queryMap.ContainsKey(ExtractId.PAGE_URL))
            {
                var tocPageUrlElem = GetExtractFirstElement(doc, _queryMap.GetValueOrDefault(ExtractId.PAGE_URL));
                if (tocPageUrlElem != null)
                {
                    string? pageHref = tocPageUrlElem.GetAttribute("href");
                    if (!string.IsNullOrEmpty(pageHref))
                    {
                        var pm = Regex.Match(pageHref, @"[?&]p=(\d+)");
                        if (pm.Success && int.TryParse(pm.Groups[1].Value, out int tocTotalPages) && tocTotalPages > 1)
                        {
                            var tocPageUrlInfo = _queryMap[ExtractId.PAGE_URL][0];
                            for (int pageIdx = 2; pageIdx <= tocTotalPages; pageIdx++)
                            {
                                string nextTocUrl = tocPageUrlInfo.Replace(pageHref + "\t" + pageIdx);
                                nextTocUrl = MakeAbsolute(nextTocUrl, listBaseUrl) ?? nextTocUrl;

                                LogAppender.Append($"  目次ページ {pageIdx}/{tocTotalPages}");
                                try
                                {
                                    await Task.Delay(Math.Max(500, _interval), ct);
                                    string tocPageHtml = await DownloadHtmlAsync(nextTocUrl, null, ct);
                                    LogAppender.Println(" : Loaded.");
                                    var tocPageDoc = await ParseAsync(tocPageHtml);
                                    var nextHrefs = GetExtractElements(tocPageDoc, _queryMap.GetValueOrDefault(ExtractId.HREF));
                                    if (nextHrefs != null)
                                    {
                                        // hrefElems に追加 (IElement[] → List に変換して結合)
                                        var combined = new List<IElement>(hrefElems);
                                        combined.AddRange(nextHrefs);
                                        hrefElems = combined.ToArray();
                                    }
                                }
                                catch (Exception e)
                                {
                                    LogAppender.Println($" 失敗: {e.Message}");
                                }
                            }
                        }
                    }
                }
            }

            // 一覧ページのサブタイトル・更新日時
            var subtitleList = GetExtractStrings(doc, _queryMap.GetValueOrDefault(ExtractId.SUBTITLE_LIST));
            string[]? postDateList = GetDateList(doc, _queryMap.GetValueOrDefault(ExtractId.CONTENT_UPDATE_LIST));

            // 各話 URL を収集
            var chapterHrefs = new List<string>();
            var hrefExtractInfo = _queryMap.GetValueOrDefault(ExtractId.HREF);
            foreach (var hrefElem in hrefElems)
            {
                string? href = hrefElem.GetAttribute("href");
                if (string.IsNullOrEmpty(href)) continue;

                // パターンフィルタリング
                if (hrefExtractInfo?.Length > 0 && hrefExtractInfo[0].HasPattern &&
                    !hrefExtractInfo[0].Matches(href))
                    continue;

                // 絶対URL化
                href = MakeAbsolute(href, listBaseUrl);
                if (href != null) chapterHrefs.Add(href);
            }

            // ── 各話変換ループ ──
            string preChapterTitle = "";
            int downloadCount = 0;

            for (int chapterIdx = 0; chapterIdx < chapterHrefs.Count; chapterIdx++)
            {
                if (ct.IsCancellationRequested) break;

                string chapterHref = chapterHrefs[chapterIdx];

                // レート制限
                if (downloadCount > 0)
                {
                    int delay = (downloadCount % 10 == 0) ? 5000 : _interval;
                    try { await Task.Delay(delay, ct); }
                    catch (OperationCanceledException) { break; }
                }

                LogAppender.Append($"[{chapterIdx + 1}/{chapterHrefs.Count}] {chapterHref}");
                string chapterHtml;
                try
                {
                    chapterHtml = await DownloadHtmlAsync(chapterHref, urlString, ct);
                    LogAppender.Println(" : Loaded.");
                    downloadCount++;
                }
                catch (Exception e)
                {
                    LogAppender.Println($" : 取得失敗 - {e.Message}");
                    _failedHrefs.Add(chapterHref);
                    continue;
                }

                _pageBaseUri = chapterHref;
                var chapterDoc = await ParseAsync(chapterHtml);

                // コンテンツ検証: CONTENT_ARTICLE 要素が空なら再ダウンロード1回
                var contentCheck = GetExtractElements(chapterDoc, _queryMap.GetValueOrDefault(ExtractId.CONTENT_ARTICLE));
                if (contentCheck == null || contentCheck.Length == 0)
                {
                    LogAppender.Println("  本文要素なし、再取得中...");
                    try
                    {
                        await Task.Delay(3000, ct);
                        chapterHtml = await DownloadHtmlAsync(chapterHref, urlString, ct, 0);
                        chapterDoc = await ParseAsync(chapterHtml);
                        contentCheck = GetExtractElements(chapterDoc, _queryMap.GetValueOrDefault(ExtractId.CONTENT_ARTICLE));
                        if (contentCheck == null || contentCheck.Length == 0)
                        {
                            LogAppender.Println("  再取得後も本文なし、スキップ");
                            _failedHrefs.Add(chapterHref);
                            continue;
                        }
                        LogAppender.Println("  再取得成功");
                    }
                    catch (Exception e)
                    {
                        LogAppender.Println($"  再取得失敗: {e.Message}");
                        _failedHrefs.Add(chapterHref);
                        continue;
                    }
                }

                // 章タイトル (大見出し)
                string? chapterTitle = GetExtractText(chapterDoc, _queryMap.GetValueOrDefault(ExtractId.CONTENT_CHAPTER));
                bool newChapter = chapterTitle != null && chapterTitle != preChapterTitle;
                if (newChapter)
                {
                    preChapterTitle = chapterTitle!;
                    lines.Add("");
                    lines.Add("［＃改ページ］");
                    if (_settings.ChapterUseCenterPage) lines.Add("［＃ページの左右中央］");
                    if (_settings.ChapterUseHashira)
                    {
                        lines.Add($"［＃ここから柱］{title}［＃ここで柱終わり］");
                    }
                    lines.Add($"［＃{_settings.Indent}字下げ］［＃大見出し］{chapterTitle}［＃大見出し終わり］");
                    lines.Add("");
                }

                string? postDate = (postDateList != null && chapterIdx < postDateList.Length)
                    ? postDateList[chapterIdx] : null;
                string? subTitle = (subtitleList != null && chapterIdx < subtitleList.Count)
                    ? subtitleList[chapterIdx] : null;

                DocToAozoraLines(chapterDoc, lines, newChapter, subTitle, postDate, null);
            }
        }

        // ── フッタ ──
        lines.Add("");
        lines.Add("［＃改ページ］");
        lines.Add($"底本：　<a href=\"{urlString}\">{urlString}</a>");
        lines.Add($"変換日時：　{DateTime.Now:yyyy/MM/dd HH:mm:ss}");

        // ── 画像ダウンロード ──
        if (_dstPath != null && _pendingImageDownloads.Count > 0)
        {
            await DownloadPendingImagesAsync(urlString, ct);

            // 表紙画像のパスを設定
            if (coverLocalPath != null && File.Exists(coverLocalPath))
                CoverImagePath = coverLocalPath;
        }

        // ── 失敗記録 ──
        if (_failedHrefs.Count > 0)
        {
            LogAppender.Println($"{_failedHrefs.Count} 話のダウンロードに失敗しました");
            if (_dstPath != null)
            {
                string failedFile = Path.Combine(_dstPath, "failed_downloads.txt");
                try
                {
                    Directory.CreateDirectory(_dstPath);
                    File.WriteAllLines(failedFile, _failedHrefs);
                    LogAppender.Println($"失敗リスト: {failedFile}");
                }
                catch { /* ignore */ }
            }
        }

        return lines;
    }

    // ─── 章HTML→行リスト変換 ────────────────────────────────────

    private void DocToAozoraLines(
        IDocument doc, List<string> lines, bool newChapter,
        string? listSubTitle, string? postDate, string? publishDate)
    {
        var contentDivs = GetExtractElements(doc, _queryMap.GetValueOrDefault(ExtractId.CONTENT_ARTICLE));
        if (contentDivs == null || contentDivs.Length == 0)
        {
            LogAppender.Println("CONTENT_ARTICLE: 本文が取得できません");
            return;
        }

        if (!newChapter)
        {
            lines.Add("");
            lines.Add("［＃改ページ］");
        }

        // 話タイトル（中見出し）
        string? subTitle = GetExtractText(doc, _queryMap.GetValueOrDefault(ExtractId.CONTENT_SUBTITLE));
        if (subTitle == null) subTitle = listSubTitle;
        if (subTitle != null)
            lines.Add($"［＃{_settings.Indent}字下げ］［＃中見出し］{subTitle}［＃中見出し終わり］");

        // 更新日時・公開日
        {
            string? contentUpdate = GetExtractText(doc, _queryMap.GetValueOrDefault(ExtractId.CONTENT_UPDATE));
            if (!string.IsNullOrEmpty(contentUpdate)) postDate = contentUpdate;

            bool showPost = _settings.ShowPostDate && !string.IsNullOrWhiteSpace(postDate);
            bool showPublish = _settings.ShowPublishDate && !string.IsNullOrEmpty(publishDate);
            if (showPost || showPublish)
            {
                lines.Add("［＃ここから地から１字上げ］");
                lines.Add("［＃ここから１段階小さな文字］");
                if (showPublish) lines.Add($"{publishDate} 公開");
                if (showPost) lines.Add($"{postDate!.Trim()} 更新");
                lines.Add("［＃ここで小さな文字終わり］");
                lines.Add("［＃ここで字上げ終わり］");
            }
        }

        lines.Add("");

        // 本文前の画像
        var imgElems = GetExtractElements(doc, _queryMap.GetValueOrDefault(ExtractId.CONTENT_IMG));
        if (imgElems?.Length > 0)
            PrintImage(lines, imgElems[0]);

        // 前書き
        var preambleDivs = GetExtractElements(doc, _queryMap.GetValueOrDefault(ExtractId.CONTENT_PREAMBLE));
        if (preambleDivs?.Length > 0)
        {
            var preambleStart = GetExtractFirstElement(doc, _queryMap.GetValueOrDefault(ExtractId.CONTENT_PREAMBLE_START));
            var preambleEnd = GetExtractFirstElement(doc, _queryMap.GetValueOrDefault(ExtractId.CONTENT_PREAMBLE_END));
            AppendSectionWithStyle(lines, preambleDivs, preambleStart, preambleEnd,
                _settings.AuthorCommentStyle, "前書き");
        }

        // 本文
        string? separator = null;
        if (_queryMap.TryGetValue(ExtractId.CONTENT_ARTICLE_SEPARATOR, out var sepInfo) && sepInfo.Length > 0)
            separator = sepInfo[0].Query;

        bool first = true;
        foreach (var elem in contentDivs)
        {
            if (first) first = false;
            else
            {
                lines.Add("");
                if (separator != null) lines.Add(separator);
            }
            var startElem = GetExtractFirstElement(doc, _queryMap.GetValueOrDefault(ExtractId.CONTENT_ARTICLE_START));
            var endElem = GetExtractFirstElement(doc, _queryMap.GetValueOrDefault(ExtractId.CONTENT_ARTICLE_END));
            PrintNode(lines, elem, startElem, endElem, false);
        }

        // 後書き
        var appendixDivs = GetExtractElements(doc, _queryMap.GetValueOrDefault(ExtractId.CONTENT_APPENDIX));
        if (appendixDivs?.Length > 0)
        {
            lines.Add("");
            lines.Add("");
            var appendixStart = GetExtractFirstElement(doc, _queryMap.GetValueOrDefault(ExtractId.CONTENT_APPENDIX_START));
            var appendixEnd = GetExtractFirstElement(doc, _queryMap.GetValueOrDefault(ExtractId.CONTENT_APPENDIX_END));
            AppendSectionWithStyle(lines, appendixDivs, appendixStart, appendixEnd,
                _settings.AuthorCommentStyle, "後書き");
        }
    }

    private void AppendSectionWithStyle(
        List<string> lines, IElement[] divs, IElement? startElem, IElement? endElem,
        string style, string sectionName)
    {
        switch (style)
        {
            case "css":
                lines.Add($"［＃ここから{sectionName}］");
                foreach (var elem in divs) PrintNode(lines, elem, startElem, endElem, true);
                lines.Add($"［＃ここで{sectionName}終わり］");
                break;
            case "simple":
                lines.Add("");
                lines.Add("［＃ここから８字下げ］");
                lines.Add("［＃ここから２段階小さな文字］");
                foreach (var elem in divs) PrintNode(lines, elem, startElem, endElem, true);
                lines.Add("");
                lines.Add("［＃ここで小さな文字終わり］");
                lines.Add("［＃ここで字下げ終わり］");
                break;
            default: // plain
                lines.Add("");
                lines.Add("");
                foreach (var elem in divs) PrintNode(lines, elem, startElem, endElem, true);
                lines.Add("");
                lines.Add("");
                lines.Add("［＃区切り線］");
                lines.Add("");
                break;
        }
    }

    // ─── DOM ノード走査 ─────────────────────────────────────────

    private void PrintNode(List<string> lines, INode parent,
        IElement? start, IElement? end, bool noHr)
    {
        var sb = new StringBuilder();
        PrintNodeInternal(lines, sb, parent, start, end, noHr);
        FlushBuffer(lines, sb);
    }

    private void PrintNodeInternal(List<string> lines, StringBuilder sb,
        INode parent, IElement? start, IElement? end, bool noHr)
    {
        bool started = (start == null);

        foreach (var node in parent.ChildNodes)
        {
            if (!started)
            {
                if (node.Equals(start)) { started = true; continue; }
                if (node is IElement childElem) PrintNodeInternal(lines, sb, childElem, start, end, noHr);
                continue;
            }

            if (end != null && node.Equals(end)) return;

            if (node.NodeType == NodeType.Text)
            {
                string text = ((IText)node).Data;
                PrintText(sb, text);
            }
            else if (node is IElement elem)
            {
                string tag = elem.LocalName;
                switch (tag)
                {
                    case "br":
                        if (node.NextSibling != null)
                        {
                            FlushBuffer(lines, sb);
                        }
                        break;
                    case "div":
                    case "p":
                        if (node.PreviousSibling != null && !IsBlockNode(node.PreviousSibling))
                            FlushBuffer(lines, sb);
                        PrintNodeInternal(lines, sb, node, start, end, noHr);
                        if (node.NextSibling != null)
                            FlushBuffer(lines, sb);
                        break;
                    case "ruby":
                        PrintRuby(sb, elem);
                        break;
                    case "img":
                        FlushBuffer(lines, sb);
                        PrintImage(lines, elem);
                        break;
                    case "hr":
                        if (!noHr)
                        {
                            FlushBuffer(lines, sb);
                            lines.Add("［＃区切り線］");
                        }
                        break;
                    case "b":
                    case "strong":
                        sb.Append(PhChuki("ここから太字"));
                        PrintNodeInternal(lines, sb, node, start, end, noHr);
                        sb.Append(PhChuki("ここで太字終わり"));
                        break;
                    case "sup":
                        sb.Append(PhChuki("上付き小文字"));
                        PrintNodeInternal(lines, sb, node, start, end, noHr);
                        sb.Append(PhChuki("上付き小文字終わり"));
                        break;
                    case "sub":
                        sb.Append(PhChuki("下付き小文字"));
                        PrintNodeInternal(lines, sb, node, start, end, noHr);
                        sb.Append(PhChuki("下付き小文字終わり"));
                        break;
                    case "em":
                        if (elem.ClassList.Contains("emphasisDots"))
                        {
                            sb.Append(PhChuki("傍点"));
                            PrintNodeInternal(lines, sb, node, start, end, noHr);
                            sb.Append(PhChuki("傍点終わり"));
                        }
                        else
                        {
                            PrintNodeInternal(lines, sb, node, start, end, noHr);
                        }
                        break;
                    case "strike":
                    case "s":
                        sb.Append(PhChuki("取消線"));
                        PrintNodeInternal(lines, sb, node, start, end, noHr);
                        sb.Append(PhChuki("取消線終わり"));
                        break;
                    case "tr":
                        PrintNodeInternal(lines, sb, node, start, end, noHr);
                        FlushBuffer(lines, sb);
                        break;
                    case "a":
                        // リンクテキストのみ出力（href は無視）
                        PrintNodeInternal(lines, sb, node, start, end, noHr);
                        break;
                    default:
                        PrintNodeInternal(lines, sb, node, start, end, noHr);
                        break;
                }
            }
        }
    }

    private static void FlushBuffer(List<string> lines, StringBuilder sb)
    {
        string line = sb.ToString().TrimEnd();
        if (line.Length > 0 || lines.Count == 0 || lines[^1].Length > 0)
            lines.Add(RestorePlaceholders(line));
        sb.Clear();
    }

    private static bool IsBlockNode(INode node)
    {
        if (node.NodeType == NodeType.Text && string.IsNullOrWhiteSpace(((IText)node).Data))
            return true;
        if (node is IElement elem)
        {
            string tag = elem.LocalName;
            return tag is "br" or "div" or "p" or "hr" or "table";
        }
        return false;
    }

    private static readonly Regex _rtSplitRegex = new(@"<rt>", RegexOptions.IgnoreCase);
    private static readonly Regex _rtCloseSplitRegex = new(@"</rt>", RegexOptions.IgnoreCase);
    private static readonly Regex _rpTagRegex = new(@"<rp>.*?</rp>", RegexOptions.IgnoreCase);
    private static readonly Regex _anyTagRegex = new(@"<[^>]+>");

    // ─── ルビ処理 ───────────────────────────────────────────────

    private void PrintRuby(StringBuilder sb, IElement ruby)
    {
        // <ruby>漢字<rt>ふりがな</rt></ruby> → ｜漢字《ふりがな》
        string html = ruby.InnerHtml;
        string[] parts = _rtSplitRegex.Split(html, 2);
        if (parts.Length < 2)
        {
            PrintText(sb, _anyTagRegex.Replace(parts[0], ""));
            return;
        }

        string rubyBase = _anyTagRegex.Replace(_rpTagRegex.Replace(parts[0], ""), "");

        string rtContent = _rtCloseSplitRegex.Split(parts[1], 2)[0];
        string rubyText = _anyTagRegex.Replace(_rpTagRegex.Replace(rtContent, ""), "");

        sb.Append('｜');
        PrintText(sb, WebUtility.HtmlDecode(rubyBase));
        sb.Append('《');
        PrintText(sb, WebUtility.HtmlDecode(rubyText));
        sb.Append('》');
    }

    // ─── 画像処理 ───────────────────────────────────────────────

    private void PrintImage(List<string> lines, IElement img)
    {
        PrintImage(lines, img, null);
    }

    private void PrintImage(List<string> lines, IElement img, string? imageOutFile)
    {
        string? src = img.GetAttribute("src");
        if (string.IsNullOrEmpty(src)) return;

        // 相対/絶対URLを正規化してファイルパスを生成 + ダウンロードURL解決
        string imagePath;
        int idx = src.IndexOf("//", StringComparison.Ordinal);
        if (idx > 0)
        {
            // 絶対URL (http:// or https://)
            imagePath = EscapeUrlToFile(src[(idx + 2)..]);
        }
        else if (idx == 0)
        {
            // プロトコル相対URL (//domain/path)
            imagePath = EscapeUrlToFile(src[2..]);
            src = _baseUri[.._baseUri.IndexOf("//", StringComparison.Ordinal)] + src;
        }
        else if (src[0] == '/')
        {
            // 同ホスト絶対パス
            imagePath = "_" + EscapeUrlToFile(src);
            src = _baseUri + src;
        }
        else
        {
            // 相対パス
            imagePath = "__/" + EscapeUrlToFile(src);
            if (_pageBaseUri.EndsWith("/")) src = _pageBaseUri + src;
            else src = _pageBaseUri + "/" + src;
        }

        if (imagePath.EndsWith("/")) imagePath += "image.png";

        // 画像ダウンロード登録
        if (_dstPath != null)
        {
            if (imageOutFile != null)
            {
                _pendingImageDownloads.Add((src, imageOutFile));
            }
            else
            {
                string localPath = Path.Combine(_dstPath, "images", imagePath);
                _pendingImageDownloads.Add((src, localPath));
            }
        }

        if (imageOutFile != null)
        {
            // 表紙: 注記はConvertAsync側で追加するためここでは何もしない
        }
        else
        {
            lines.Add($"［＃挿絵（images/{imagePath}）入る］");
        }
    }

    /// <summary>画像srcを絶対URLに解決する</summary>
    private string ResolveImageUrl(string src)
    {
        int idx = src.IndexOf("//", StringComparison.Ordinal);
        if (idx > 0) return src; // 絶対URL
        if (idx == 0) return _baseUri[.._baseUri.IndexOf("//", StringComparison.Ordinal)] + src;
        if (src[0] == '/') return _baseUri + src;
        return _pageBaseUri.EndsWith("/") ? _pageBaseUri + src : _pageBaseUri + "/" + src;
    }

    private static string EscapeUrlToFile(string url)
    {
        // URLをファイルシステム安全な文字列に変換（簡易版）
        return url
            .Replace("?", "_")
            .Replace("&", "_")
            .Replace("=", "_")
            .Replace("#", "_");
    }

    // ─── テキスト変換 ───────────────────────────────────────────

    private void PrintText(StringBuilder sb, string text)
    {
        text = text.ReplaceLineEndings("").Replace("\r", "").Replace("\n", "");
        if (text.Length == 0) return;

        // HTML エンティティデコード（テキストノードは通常デコード済みだが念のため）
        text = WebUtility.HtmlDecode(text);

        if (_settings.EnableAutoJoinInBrackets) text = AutoJoinInBracketsInline(text);
        if (_settings.EnableAutoJoinLine) text = AutoJoinLineInline(text);
        if (_settings.EnableNarouTag) text = ConvertNarouTags(text);

        text = AddHalfIndentBracket(text);
        text = InsertSeparateSpace(text);
        text = ConvertTatechuyoko(text);
        text = ConvertNovelRule(text);

        foreach (char ch in text)
        {
            switch (ch)
            {
                case '《': sb.Append("※［＃始め二重山括弧］"); break;
                case '》': sb.Append("※［＃終わり二重山括弧］"); break;
                case '※': sb.Append("※［＃米印、1-2-8］"); break;
                case '\t': sb.Append(' '); break;
                default: sb.Append(RestorePlaceholders(ch.ToString())); break;
            }
        }
    }

    private static string AutoJoinInBracketsInline(string text)
    {
        bool changed = true;
        while (changed)
        {
            string before = text;
            text = Regex.Replace(text, "「([^「」]*)\n([^「」]*)」", "「$1　$2」");
            text = Regex.Replace(text, "『([^『』]*)\n([^『』]*)』", "『$1　$2』");
            changed = text != before;
        }
        return text;
    }

    private static string AutoJoinLineInline(string text) =>
        text.Replace("、\n", "、");

    private string ConvertNarouTags(string text)
    {
        text = text.Replace("[newpage]", PhChuki("改ページ"));

        // [chapter:タイトル]
        var sb = new StringBuilder();
        int idx = 0;
        const string chapterKey = "[chapter:";
        while (true)
        {
            int start = text.IndexOf(chapterKey, idx, StringComparison.Ordinal);
            if (start < 0) { sb.Append(text, idx, text.Length - idx); break; }
            sb.Append(text, idx, start - idx);
            int titleStart = start + chapterKey.Length;
            int end = text.IndexOf(']', titleStart);
            if (end < 0) { sb.Append(text, start, text.Length - start); break; }
            string chTitle = text[titleStart..end];
            sb.Append(PhChuki($"{_settings.Indent}字下げ"));
            sb.Append(PhChuki("中見出し"));
            sb.Append(chTitle);
            sb.Append(PhChuki("中見出し終わり"));
            idx = end + 1;
        }
        text = sb.ToString();

        // [jump:URL]
        var sb2 = new StringBuilder();
        idx = 0;
        const string jumpKey = "[jump:";
        while (true)
        {
            int jstart = text.IndexOf(jumpKey, idx, StringComparison.Ordinal);
            if (jstart < 0) { sb2.Append(text, idx, text.Length - idx); break; }
            sb2.Append(text, idx, jstart - idx);
            int urlStart = jstart + jumpKey.Length;
            int jend = text.IndexOf(']', urlStart);
            if (jend < 0) { sb2.Append(text, jstart, text.Length - jstart); break; }
            string jumpUrl = text[urlStart..jend];
            sb2.Append($"<a href=\"{jumpUrl}\">{jumpUrl}</a>");
            idx = jend + 1;
        }
        return sb2.ToString();
    }

    private string AddHalfIndentBracket(string text)
    {
        if (!_settings.EnableHalfIndentBracket) return text;
        return Regex.Replace(text, @"^[ 　\t]*([「『〔（【〈《≪〝])",
            PhChuki("二分アキ") + "$1", RegexOptions.Multiline);
    }

    private static readonly Regex SeparateSpacePattern = new(@"([!?！？]+)([^!?！？])");
    private static string InsertSeparateSpace(string text)
    {
        return SeparateSpacePattern.Replace(text, m =>
        {
            string marks = m.Groups[1].Value;
            string next = m.Groups[2].Value;
            if (Regex.IsMatch(next, @"[」］｝\]\}』】〉》〕＞>≫)）\u201d\u201c\u2019〟　☆★♪［―]"))
                return marks + next;
            if (Regex.IsMatch(next, @"[ 、。]"))
                return marks + "　";
            return marks + "　" + next;
        });
    }

    private static string ConvertTatechuyoko(string text)
    {
        string tcyOpen = PhChuki("縦中横");
        string tcyClose = PhChuki("縦中横終わり");

        // 4個以上の！を2個ずつ縦中横化
        text = Regex.Replace(text, "！{4,}", m =>
        {
            int len = m.Length % 2 == 1 ? m.Length + 1 : m.Length;
            return string.Concat(Enumerable.Repeat(tcyOpen + "!!" + tcyClose, len / 2));
        });
        text = text.Replace("！！！", tcyOpen + "!!!" + tcyClose);

        // 4個以上の？
        text = Regex.Replace(text, "？{4,}", m =>
        {
            int len = m.Length % 2 == 1 ? m.Length + 1 : m.Length;
            return string.Concat(Enumerable.Repeat(tcyOpen + "??" + tcyClose, len / 2));
        });
        text = text.Replace("？？？", tcyOpen + "???" + tcyClose);

        text = text.Replace("！！？", tcyOpen + "!!?" + tcyClose);
        text = text.Replace("？！！", tcyOpen + "?!!" + tcyClose);
        text = text.Replace("！！", tcyOpen + "!!" + tcyClose);
        text = text.Replace("！？", tcyOpen + "!?" + tcyClose);
        text = text.Replace("？！", tcyOpen + "?!" + tcyClose);
        text = text.Replace("？？", tcyOpen + "??" + tcyClose);
        return text;
    }

    private static string ConvertNovelRule(string text)
    {
        // 。の後の閉じ括弧類→閉じ括弧を先頭に移動
        text = Regex.Replace(text, "。([」』）])", "$1");

        // …を偶数個に調整
        text = Regex.Replace(text, "…+", m =>
        {
            int len = m.Length % 2 != 0 ? m.Length + 1 : m.Length;
            return new string('…', len);
        });

        // ‥を偶数個に調整
        text = Regex.Replace(text, "‥+", m =>
        {
            int len = m.Length % 2 != 0 ? m.Length + 1 : m.Length;
            return new string('‥', len);
        });

        return text;
    }

    // ─── CSS クエリ補助メソッド ────────────────────────────────

    private string? GetExtractText(IDocument doc, ExtractInfo[]? infos)
    {
        if (infos == null) return null;
        foreach (var info in infos)
        {
            var elements = doc.QuerySelectorAll(info.Query);
            if (elements.Length == 0) continue;

            var buf = new StringBuilder();
            if (info.Idx == null)
            {
                foreach (var elem in elements)
                    buf.Append(' ').Append(ReplaceHtmlText(elem.InnerHtml, info));
            }
            else
            {
                foreach (int i in info.Idx)
                {
                    int pos = i < 0 ? elements.Length + i : i;
                    if (pos >= 0 && pos < elements.Length)
                        buf.Append(' ').Append(ReplaceHtmlText(elements[pos].InnerHtml, info));
                }
            }

            string text = buf.Length > 0 ? buf.ToString(1, buf.Length - 1) : "";
            if (text.Length > 0) return text;
        }
        return null;
    }

    private List<string>? GetExtractStrings(IDocument doc, ExtractInfo[]? infos)
    {
        if (infos == null) return null;
        foreach (var info in infos)
        {
            var elements = doc.QuerySelectorAll(info.Query);
            if (elements.Length == 0) continue;

            var result = new List<string>();
            if (info.Idx == null)
            {
                foreach (var elem in elements)
                    result.Add(ReplaceHtmlText(elem.InnerHtml, info));
            }
            else
            {
                foreach (int i in info.Idx)
                {
                    int pos = i < 0 ? elements.Length + i : i;
                    if (pos >= 0 && pos < elements.Length)
                        result.Add(ReplaceHtmlText(elements[pos].InnerHtml, info));
                }
            }
            return result;
        }
        return null;
    }

    private IElement[]? GetExtractElements(IDocument doc, ExtractInfo[]? infos)
    {
        if (infos == null) return null;
        foreach (var info in infos)
        {
            var elements = doc.QuerySelectorAll(info.Query);
            if (elements.Length == 0) continue;

            if (info.Idx == null) return elements.ToArray();

            var result = new List<IElement>();
            foreach (int i in info.Idx)
            {
                int pos = i < 0 ? elements.Length + i : i;
                if (pos >= 0 && pos < elements.Length)
                    result.Add(elements[pos]);
            }
            if (result.Count > 0) return result.ToArray();
        }
        return null;
    }

    private IElement? GetExtractFirstElement(IDocument doc, ExtractInfo[]? infos)
    {
        if (infos == null) return null;
        foreach (var info in infos)
        {
            var elements = doc.QuerySelectorAll(info.Query);
            if (elements.Length == 0) continue;

            int pos = (info.Idx?.Length > 0) ? info.Idx[0] : 0;
            if (pos < 0) pos = elements.Length + pos;
            if (pos >= 0 && pos < elements.Length) return elements[pos];
        }
        return null;
    }

    private string[]? GetDateList(IDocument doc, ExtractInfo[]? infos)
    {
        var strings = GetExtractStrings(doc, infos);
        return strings?.ToArray();
    }

    private static string ReplaceHtmlText(string html, ExtractInfo? info)
    {
        html = html.Replace("\n", "").Replace("\r", "").Replace("\t", " ");
        if (info != null) html = info.Replace(html);
        // <br> → 空白、<rt>タグ削除、全タグ削除
        html = Regex.Replace(html, "<br ?/?>", " ");
        html = Regex.Replace(html, "<rt>.*?</rt>", "");
        html = Regex.Replace(html, "<[^>]+>", "");
        return WebUtility.HtmlDecode(html).Trim();
    }

    // ─── 画像ダウンロード ─────────────────────────────────────

    private async Task DownloadPendingImagesAsync(string referer, CancellationToken ct)
    {
        LogAppender.Println($"画像ダウンロード: {_pendingImageDownloads.Count} 件");
        int success = 0, fail = 0;

        foreach (var (url, localPath) in _pendingImageDownloads)
        {
            if (ct.IsCancellationRequested) break;

            // 既存ファイルはスキップ
            if (File.Exists(localPath))
            {
                success++;
                continue;
            }

            try
            {
                await DownloadFileAsync(url, localPath, referer, ct);
                success++;
            }
            catch (Exception)
            {
                // リトライ1回
                try
                {
                    await Task.Delay(3000, ct);
                    await DownloadFileAsync(url, localPath, referer, ct);
                    success++;
                }
                catch (OperationCanceledException) { break; }
                catch (Exception e2)
                {
                    LogAppender.Println($"画像が取得できませんでした : {url} ({e2.Message})");
                    fail++;
                }
            }

            // レート制限
            try { await Task.Delay(200, ct); }
            catch (OperationCanceledException) { break; }
        }

        LogAppender.Println($"画像ダウンロード完了: 成功 {success}, 失敗 {fail}");
    }

    private async Task DownloadFileAsync(string url, string localPath, string? referer, CancellationToken ct)
    {
        // 親ディレクトリ作成
        string? dir = Path.GetDirectoryName(localPath);
        if (dir != null) Directory.CreateDirectory(dir);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (referer != null)
            request.Headers.Add("Referer", referer);
        if (_queryMap.TryGetValue(ExtractId.COOKIE, out var cookieInfos) && cookieInfos.Length > 0)
            request.Headers.Add("Cookie", cookieInfos[0].Query);

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await contentStream.CopyToAsync(fileStream, ct);
    }

    // ─── HTTP ダウンロード ─────────────────────────────────────

    private async Task<string> DownloadHtmlAsync(string url, string? referer, CancellationToken ct, int maxRetries = 2)
    {
        Exception? lastEx = null;
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (attempt > 0)
            {
                LogAppender.Println($"  リトライ {attempt}/{maxRetries}...");
                await Task.Delay(3000, ct);
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (referer != null)
                    request.Headers.Add("Referer", referer);

                if (_queryMap.TryGetValue(ExtractId.COOKIE, out var cookieInfos) && cookieInfos.Length > 0)
                    request.Headers.Add("Cookie", cookieInfos[0].Query);

                using var response = await _http.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { lastEx = e; }
        }
        throw lastEx!;
    }

    // ─── HTML パース ───────────────────────────────────────────

    private static IBrowsingContext? _browsingContext;
    private static IBrowsingContext GetContext()
    {
        _browsingContext ??= BrowsingContext.New(Configuration.Default);
        return _browsingContext;
    }

    private static async Task<IDocument> ParseAsync(string html)
    {
        return await GetContext().OpenAsync(req => req.Content(html));
    }

    // ─── URL 正規化 ────────────────────────────────────────────

    private string? MakeAbsolute(string href, string listBaseUrl)
    {
        if (string.IsNullOrEmpty(href)) return null;
        if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return href;
        if (href[0] == '/') return _baseUri + href;
        return listBaseUrl + href;
    }

    // ─── extract.txt / replace.txt 読み込み ────────────────────

    private static Dictionary<ExtractId, ExtractInfo[]> LoadQueryMap(string fqdn, string configPath)
    {
        string extractFile = Path.Combine(configPath, fqdn, "extract.txt");
        var map = new Dictionary<ExtractId, ExtractInfo[]>();
        if (!File.Exists(extractFile)) return map;

        foreach (string rawLine in File.ReadLines(extractFile, Encoding.UTF8))
        {
            if (rawLine.Length == 0 || rawLine[0] == '#') continue;
            string[] values = rawLine.Split('\t', StringSplitOptions.None);
            if (values.Length < 2) continue;

            if (!Enum.TryParse<ExtractId>(values[0], out var id)) continue;

            string[] queryStrings = values[1].Split(',');
            Regex? pattern = values.Length > 2 && values[2].Length > 0
                ? new Regex(values[2]) : null;
            string? replacement = values.Length > 3 ? values[3] : null;

            map[id] = queryStrings
                .Select(q => new ExtractInfo(q.Trim(), pattern, replacement))
                .ToArray();
        }
        return map;
    }

    private static Dictionary<ExtractId, List<string[]>> LoadReplaceMap(string fqdn, string configPath)
    {
        string replaceFile = Path.Combine(configPath, fqdn, "replace.txt");
        var map = new Dictionary<ExtractId, List<string[]>>();
        if (!File.Exists(replaceFile)) return map;

        foreach (string rawLine in File.ReadLines(replaceFile, Encoding.UTF8))
        {
            if (rawLine.Length == 0 || rawLine[0] == '#') continue;
            string[] values = rawLine.Split('\t');
            if (values.Length < 2) continue;
            if (!Enum.TryParse<ExtractId>(values[0], out var id)) continue;
            if (!map.TryGetValue(id, out var list))
            {
                list = new List<string[]>();
                map[id] = list;
            }
            list.Add([values[1], values.Length >= 3 ? values[2] : ""]);
        }
        return map;
    }
}
