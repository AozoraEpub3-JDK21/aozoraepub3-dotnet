using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AozoraEpub3.Gui.Services;

/// <summary>
/// EPUB のソースマッピング情報を生成・保存する。
/// チャプター ↔ XHTML ファイルのマッピング、行番号マッピング、要素マッピングを段階的に提供する。
/// </summary>
public static class SourceMappingService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>EPUB ファイルパスからメタ情報ファイルパスを導出する。</summary>
    public static string GetMetaFilePath(string epubPath)
        => epubPath + ".meta.json";

    /// <summary>
    /// Phase E1: EPUB を解析してチャプター ↔ XHTML ファイルのマッピングを生成する。
    /// </summary>
    public static EpubSourceMap GenerateFromEpub(EpubPreviewService previewService, string epubPath)
    {
        var map = new EpubSourceMap
        {
            EpubFileName = Path.GetFileName(epubPath),
            GeneratedAt = DateTime.UtcNow.ToString("o"),
        };

        // スパイン情報
        for (int i = 0; i < previewService.SpineItems.Count; i++)
        {
            var spine = previewService.SpineItems[i];
            var chapter = new ChapterMapping
            {
                SpineIndex = i,
                Id = spine.Id,
                Href = spine.Href,
            };

            // Phase E2: 行番号マッピング — XHTML 内の <p> 要素を解析
            if (File.Exists(spine.AbsolutePath))
            {
                try
                {
                    var content = File.ReadAllText(spine.AbsolutePath);
                    chapter.LineCount = content.Split('\n').Length;

                    // Phase E3: 要素マッピング — <p>, <h1>-<h6>, <img> 要素を抽出
                    chapter.Elements = ExtractElements(content);
                }
                catch { /* 解析失敗は無視 */ }
            }

            // 目次ラベルを付与
            var tocEntry = previewService.TocEntries
                .FirstOrDefault(t => t.SpineIndex == i);
            if (tocEntry != null)
                chapter.TocLabel = tocEntry.Label;

            map.Chapters.Add(chapter);
        }

        return map;
    }

    /// <summary>メタ情報を JSON ファイルに保存する。</summary>
    public static void Save(EpubSourceMap map, string metaFilePath)
    {
        var json = JsonSerializer.Serialize(map, _jsonOptions);
        File.WriteAllText(metaFilePath, json);
    }

    /// <summary>メタ情報を JSON ファイルから読み込む。</summary>
    public static EpubSourceMap? Load(string metaFilePath)
    {
        if (!File.Exists(metaFilePath)) return null;
        try
        {
            var json = File.ReadAllText(metaFilePath);
            return JsonSerializer.Deserialize<EpubSourceMap>(json, _jsonOptions);
        }
        catch { return null; }
    }

    /// <summary>EPUB 生成時に自動的にメタ情報を生成・保存する。</summary>
    public static void GenerateAndSave(EpubPreviewService previewService, string epubPath)
    {
        var map = GenerateFromEpub(previewService, epubPath);
        var metaPath = GetMetaFilePath(epubPath);
        Save(map, metaPath);
    }

    // ───── Phase E3: 要素抽出 ──────────────────────────────────────

    private static List<ElementMapping> ExtractElements(string xhtmlContent)
    {
        var elements = new List<ElementMapping>();
        try
        {
            var doc = XDocument.Parse(xhtmlContent);
            XNamespace xhtml = "http://www.w3.org/1999/xhtml";

            var lineNum = 0;
            foreach (var line in xhtmlContent.Split('\n'))
            {
                lineNum++;
                // 簡易的な要素検出（XDocument の行番号は LoadOptions.SetLineInfo で取得可能だが、
                // ここでは正規表現で高速に処理）
                foreach (Match m in Regex.Matches(line, @"<(p|h[1-6]|img)\b[^>]*>"))
                {
                    var tagName = m.Groups[1].Value;
                    var text = "";

                    // テキストコンテンツを抽出（<p>...</p> の場合）
                    if (tagName == "p" || tagName.StartsWith('h'))
                    {
                        var closeTag = $"</{tagName}>";
                        var startIdx = m.Index + m.Length;
                        var endIdx = line.IndexOf(closeTag, startIdx, StringComparison.Ordinal);
                        if (endIdx >= 0)
                        {
                            text = Regex.Replace(line[startIdx..endIdx], "<[^>]+>", "");
                            if (text.Length > 80) text = text[..80] + "...";
                        }
                    }

                    // img の src を抽出
                    if (tagName == "img")
                    {
                        var srcMatch = Regex.Match(m.Value, @"src=""([^""]+)""");
                        if (srcMatch.Success)
                            text = srcMatch.Groups[1].Value;
                    }

                    // id 抽出
                    var idMatch = Regex.Match(m.Value, @"id=""([^""]+)""");

                    elements.Add(new ElementMapping
                    {
                        Tag = tagName,
                        Line = lineNum,
                        Id = idMatch.Success ? idMatch.Groups[1].Value : null,
                        Text = string.IsNullOrEmpty(text) ? null : text,
                    });
                }
            }
        }
        catch { /* パース失敗時は空リストを返す */ }

        return elements;
    }
}

// ───── データモデル ──────────────────────────────────────────────────

/// <summary>EPUB ソースマッピング全体</summary>
public sealed class EpubSourceMap
{
    public string EpubFileName { get; set; } = "";
    public string GeneratedAt { get; set; } = "";
    public List<ChapterMapping> Chapters { get; set; } = [];
}

/// <summary>チャプター単位のマッピング</summary>
public sealed class ChapterMapping
{
    public int SpineIndex { get; set; }
    public string Id { get; set; } = "";
    public string Href { get; set; } = "";
    public string? TocLabel { get; set; }
    public int LineCount { get; set; }
    public List<ElementMapping>? Elements { get; set; }
}

/// <summary>XHTML 内の要素マッピング（Phase E3）</summary>
public sealed class ElementMapping
{
    public string Tag { get; set; } = "";
    public int Line { get; set; }
    public string? Id { get; set; }
    public string? Text { get; set; }
}
