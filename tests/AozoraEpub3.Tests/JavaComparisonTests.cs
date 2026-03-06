using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AozoraEpub3.Core.Converter;
using AozoraEpub3.Core.Info;
using AozoraEpub3.Core.Io;
using Xunit;
using Xunit.Abstractions;

namespace AozoraEpub3.Tests;

/// <summary>
/// narou.rb + Java AozoraEpub3 で生成したリファレンス EPUB との比較テスト。
///
/// 前提: tests/integration/generate-reference-epubs.ps1 を実行して
///       tests/integration/reference/{id}/ にリファレンスファイルを配置すること。
///
/// 比較方針:
///   - OPS/package.opf   : タイムスタンプ・UUID を正規化してから比較
///   - OPS/toc.ncx       : UUID を正規化してから比較
///   - その他すべての EPUB エントリ: バイト完全一致
///   - ZIP 内エントリの一覧も一致すること（ファイル数・パス名）
/// </summary>
[Trait("Category", "JavaComparison")]
public class JavaComparisonTests
{
    private readonly ITestOutputHelper _output;

    // リファレンスディレクトリ (tests/integration/reference/)
    private static readonly string ReferenceRoot = Path.GetFullPath(
        Path.Combine(
            Path.GetDirectoryName(typeof(JavaComparisonTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", "tests", "integration", "reference"));

    public JavaComparisonTests(ITestOutputHelper output) => _output = output;

    // ── テストケース定義 ────────────────────────────────────────────────────────

    public static IEnumerable<object[]> TestCases => new[]
    {
        // [id,  encoding]
        new object[] { "n8005ls",                    "UTF-8"  },
        new object[] { "n0063lr",                    "UTF-8"  },
        new object[] { "n9623lp",                    "UTF-8"  },
        new object[] { "kakuyomu_822139840468926025", "UTF-8"  },
        new object[] { "aozora_1567_14913",           "MS932"  },
    };

    // ── メインテスト ────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(TestCases))]
    public void CompareWithJavaOutput(string id, string inputEncoding)
    {
        // ── 前提: リファレンスファイルの存在確認 ──────────────────────────────
        string refDir       = Path.Combine(ReferenceRoot, id);
        string referenceEpub = Path.Combine(refDir, "reference.epub");
        string inputTxt      = Path.Combine(refDir, "input.txt");

        if (!Directory.Exists(refDir) || !File.Exists(referenceEpub) || !File.Exists(inputTxt))
        {
            _output.WriteLine(
                $"SKIP: リファレンスファイルが存在しません。" +
                $"tests/integration/generate-reference-epubs.ps1 を実行してください。\n" +
                $"  期待パス: {refDir}");
            // リファレンスなしはスキップ扱い（CI では失敗にしたくない）
            return;
        }

        // ── .NET CLI で変換 ────────────────────────────────────────────────────
        string testEpub = GenerateEpubWithDotNet(inputTxt, inputEncoding, id);

        try
        {
            // ── EPUB エントリを読み込み ────────────────────────────────────────
            var refEntries  = ReadEpubEntries(referenceEpub);
            var testEntries = ReadEpubEntries(testEpub);

            // ── 比較実行 ────────────────────────────────────────────────────────
            var result = CompareEpubs(refEntries, testEntries);

            if (!result.IsEqual)
            {
                _output.WriteLine(result.Report);
                Assert.Fail($"EPUB 不一致 [{id}]:\n{result.Report}");
            }
            else
            {
                _output.WriteLine($"[{id}] OK: {refEntries.Count} エントリがすべて一致");
            }
        }
        finally
        {
            TryDelete(testEpub);
        }
    }

    // ── .NET 変換 ────────────────────────────────────────────────────────────────

    private string GenerateEpubWithDotNet(string inputTxt, string inputEncoding, string id)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        Encoding enc = inputEncoding.Equals("UTF-8", StringComparison.OrdinalIgnoreCase)
            ? Encoding.UTF8
            : Encoding.GetEncoding(932);  // MS932

        string text = File.ReadAllText(inputTxt, enc);

        string tmpDir  = Path.Combine(Path.GetTempPath(), $"aozora_cmp_{id}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmpDir);
        string outPath = Path.Combine(tmpDir, "test.epub");

        var writer    = new Epub3Writer("");
        var converter = new AozoraEpub3Converter(writer, "");

        // カバー画像が reference ディレクトリ内にあれば利用
        string refDir    = Path.Combine(ReferenceRoot, id);
        string imgReader_basePath = refDir;

        BookInfo bookInfo;
        using (var reader1 = new StringReader(text))
        {
            var imgR = new ImageInfoReader(true, imgReader_basePath);
            bookInfo = converter.GetBookInfo(
                inputTxt, reader1, imgR,
                BookInfo.TitleType.TITLE_AUTHOR, false);
        }

        bookInfo.Vertical      = true;
        bookInfo.InsertTocPage = true;
        bookInfo.TitlePageType = BookInfo.TITLE_HORIZONTAL;

        converter.SetSpaceHyphenation(1);
        // Java リファレンスは dakutenType=2（フォントモード）で生成されているため合わせる
        converter.SetCharOutput(2, false, true);
        converter.InitGaijiFontMap();

        using var reader2 = new StringReader(text);
        var imgR2 = new ImageInfoReader(true, imgReader_basePath);
        writer.Write(converter, reader2, inputTxt, "txt", outPath, bookInfo, imgR2);

        return outPath;
    }

    // ── EPUB 読み込み ────────────────────────────────────────────────────────────

    private static Dictionary<string, byte[]> ReadEpubEntries(string epubPath)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using var zip = ZipFile.OpenRead(epubPath);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;  // ディレクトリエントリをスキップ
            using var stream = entry.Open();
            using var ms     = new MemoryStream();
            stream.CopyTo(ms);
            result[entry.FullName] = ms.ToArray();
        }
        return result;
    }

    // ── 比較ロジック ─────────────────────────────────────────────────────────────

    private CompareResult CompareEpubs(
        Dictionary<string, byte[]> reference,
        Dictionary<string, byte[]> test)
    {
        var sb      = new StringBuilder();
        bool allOk  = true;

        // ─ エントリ一覧の差分 ─
        var refKeys  = new HashSet<string>(reference.Keys, StringComparer.OrdinalIgnoreCase);
        var testKeys = new HashSet<string>(test.Keys,      StringComparer.OrdinalIgnoreCase);

        var onlyInRef  = refKeys.Except(testKeys).OrderBy(x => x).ToList();
        var onlyInTest = testKeys.Except(refKeys).OrderBy(x => x).ToList();

        if (onlyInRef.Count > 0)
        {
            allOk = false;
            sb.AppendLine($"[エントリ不足] .NET 出力に存在しないエントリ ({onlyInRef.Count}):");
            foreach (var e in onlyInRef) sb.AppendLine($"  - {e}");
        }
        if (onlyInTest.Count > 0)
        {
            allOk = false;
            sb.AppendLine($"[余分なエントリ] .NET 出力にのみ存在するエントリ ({onlyInTest.Count}):");
            foreach (var e in onlyInTest) sb.AppendLine($"  + {e}");
        }

        // ─ 共通エントリのコンテンツ比較 ─
        // vertical_font.css は narou.rb 互換の拡張スタイルを含むため Java 単体出力と異なる（意図的差異）
        var skipEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "OPS/css/vertical_font.css" };
        foreach (var key in refKeys.Intersect(testKeys).OrderBy(x => x))
        {
            if (skipEntries.Contains(key)) continue;
            byte[] refBytes  = reference[key];
            byte[] testBytes = test[key];

            bool isTextFile = IsTextEntry(key);

            if (isTextFile)
            {
                string refText  = NormalizeEpubText(key, Encoding.UTF8.GetString(refBytes));
                string testText = NormalizeEpubText(key, Encoding.UTF8.GetString(testBytes));

                if (refText != testText)
                {
                    allOk = false;
                    sb.AppendLine($"\n[不一致] {key}");
                    AppendTextDiff(sb, key, refText, testText);
                }
                else
                {
                    _output.WriteLine($"  OK (text) {key}");
                }
            }
            else
            {
                if (!refBytes.SequenceEqual(testBytes))
                {
                    allOk = false;
                    sb.AppendLine($"\n[不一致] {key}");
                    sb.AppendLine($"  ref  サイズ: {refBytes.Length} bytes");
                    sb.AppendLine($"  test サイズ: {testBytes.Length} bytes");
                }
                else
                {
                    _output.WriteLine($"  OK (bin)  {key}  ({refBytes.Length} bytes)");
                }
            }
        }

        return new CompareResult(allOk, sb.ToString());
    }

    // ── テキストエントリ判定 ─────────────────────────────────────────────────────

    private static bool IsTextEntry(string entryName)
    {
        var ext = Path.GetExtension(entryName).ToLowerInvariant();
        return ext is ".xhtml" or ".html" or ".opf" or ".ncx" or ".css" or ".xml";
    }

    // ── タイムスタンプ / UUID 正規化 ─────────────────────────────────────────────

    /// <summary>
    /// ファイル種別に応じて非決定論的な値（タイムスタンプ・UUID）を固定文字列に置換する。
    /// </summary>
    private static string NormalizeEpubText(string entryName, string content)
    {
        // 行末正規化: CRLF → LF + 末尾改行の統一
        content = content.Replace("\r\n", "\n").TrimEnd('\n');

        string name = entryName.ToLowerInvariant();

        if (name.EndsWith("package.opf"))
        {
            // dc:date (発行日)
            content = Regex.Replace(
                content,
                @"<dc:date>[^<]*</dc:date>",
                "<dc:date>NORMALIZED_DATE</dc:date>",
                RegexOptions.IgnoreCase);

            // dcterms:modified (最終更新日時)
            content = Regex.Replace(
                content,
                @"<meta\s+property=""dcterms:modified"">[^<]*</meta>",
                @"<meta property=""dcterms:modified"">NORMALIZED_DATE</meta>",
                RegexOptions.IgnoreCase);

            // unique-identifier (UUID)
            content = Regex.Replace(
                content,
                @"<dc:identifier[^>]*>urn:uuid:[0-9a-f\-]+</dc:identifier>",
                "<dc:identifier id=\"unique-id\">urn:uuid:NORMALIZED_UUID</dc:identifier>",
                RegexOptions.IgnoreCase);

            // dcterms:identifier (UUID refines)
            content = Regex.Replace(
                content,
                @"property=""dcterms:identifier"">urn:uuid:[0-9a-f\-]+</meta>",
                @"property=""dcterms:identifier"">urn:uuid:NORMALIZED_UUID</meta>",
                RegexOptions.IgnoreCase);
        }
        else if (name.EndsWith("toc.ncx"))
        {
            // dtb:uid (NCX の UUID)
            content = Regex.Replace(
                content,
                @"<meta\s+name=""dtb:uid""\s+content=""[^""]*""\s*/>",
                @"<meta name=""dtb:uid"" content=""NORMALIZED_UUID""/>",
                RegexOptions.IgnoreCase);
        }

        return content;
    }

    // ── テキスト差分出力 ─────────────────────────────────────────────────────────

    private static void AppendTextDiff(StringBuilder sb, string entryName, string refText, string testText)
    {
        string[] refLines  = refText.Split('\n');
        string[] testLines = testText.Split('\n');

        int maxLines = Math.Max(refLines.Length, testLines.Length);
        int diffCount = 0;
        const int maxDiffShow = 30;

        for (int i = 0; i < maxLines && diffCount < maxDiffShow; i++)
        {
            string r = i < refLines.Length  ? refLines[i]  : "<(行なし)>";
            string t = i < testLines.Length ? testLines[i] : "<(行なし)>";

            if (r != t)
            {
                sb.AppendLine($"  行 {i + 1}:");
                sb.AppendLine($"    REF : {Truncate(r, 200)}");
                sb.AppendLine($"    TEST: {Truncate(t, 200)}");
                diffCount++;
            }
        }

        int totalDiff = Enumerable.Range(0, maxLines)
            .Count(i => (i < refLines.Length ? refLines[i] : "") !=
                        (i < testLines.Length ? testLines[i] : ""));

        if (totalDiff > maxDiffShow)
            sb.AppendLine($"  ... 他 {totalDiff - maxDiffShow} 行も不一致");

        sb.AppendLine($"  合計不一致行数: {totalDiff} / {maxLines}");
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private static void TryDelete(string path)
    {
        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (dir != null && Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
        catch { /* 無視 */ }
    }

    // ── 結果型 ─────────────────────────────────────────────────────────────────

    private record CompareResult(bool IsEqual, string Report);
}
