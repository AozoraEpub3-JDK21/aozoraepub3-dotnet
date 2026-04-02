using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using AozoraEpub3.Core.Converter;
using AozoraEpub3.Core.Info;
using AozoraEpub3.Core.Io;
using AozoraEpub3.Core.Web;
using AozoraEpub3.Gui.ViewModels;

namespace AozoraEpub3.Gui.Services;

/// <summary>
/// AozoraEpub3.Core の変換処理をラップし、UI 向けの非同期インターフェースを提供する。
/// <para>
/// 変換処理は必ず UIスレッド外（Task.Run）で実行すること。
/// ログ出力は LogAppender.OutputAction を購読して ViewModel に転送する。
/// </para>
/// </summary>
public sealed class ConversionService
{
    // Epub3Writer に渡すテンプレートパス。GUIでは実行ファイル隣の template/ を優先し、
    // 存在しなければ埋め込みリソースにフォールバックする。
    private static string TemplatePath =>
        Path.Combine(AppContext.BaseDirectory, "template") + Path.DirectorySeparatorChar;

    // ───── Local file conversion ─────────────────────────────────────────────

    /// <summary>ローカルファイルを変換する。</summary>
    /// <param name="inputPath">入力ファイルパス (.txt / .zip / .rar)</param>
    /// <param name="outputDir">出力ディレクトリ（null = 入力と同じ場所）</param>
    /// <param name="settings">変換設定 ViewModel</param>
    /// <param name="progress">進捗コールバック (0–100)</param>
    /// <param name="ct">キャンセルトークン</param>
    public async Task ConvertFileAsync(
        string inputPath,
        string? outputDir,
        LocalConvertSettingsViewModel settings,
        IProgress<int>? progress,
        CancellationToken ct)
    {
        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            string ext = Path.GetExtension(inputPath).TrimStart('.').ToLowerInvariant();
            bool isFile = ext == "txt";

            var epub3Writer = new Epub3Writer(TemplatePath);
            epub3Writer.ProgressCallback = v => progress?.Report(v);
            if (settings.ForKindle) epub3Writer.SetIsKindle(true);

            var converter = new AozoraEpub3Converter(epub3Writer, TemplatePath);

            var encoding = settings.InputEncoding == "UTF-8"
                ? Encoding.UTF8
                : Encoding.GetEncoding(932);  // MS932/Shift-JIS

            var titleType = (BookInfo.TitleType)Math.Clamp(settings.TitleType, 0, 5);

            // 第1パス: BookInfo 取得
            var textEntryName = new string[1];
            var imageInfoReader = new ImageInfoReader(isFile, inputPath);

            var stream1 = ArchiveTextExtractor.GetTextInputStream(inputPath, ext, textEntryName, 0);
            if (stream1 is null)
                throw new InvalidOperationException($"テキストストリームを開けませんでした: {inputPath}");

            BookInfo bookInfo;
            using (var reader1 = new StreamReader(stream1, encoding))
            {
                bookInfo = converter.GetBookInfo(inputPath, reader1, imageInfoReader, titleType, false);
            }
            bookInfo.TextEntryName = textEntryName[0];

            // BookInfo / Converter へ設定適用
            settings.ApplyTo(bookInfo, converter);

            // ファイル名から表題・著者を補完
            var titleCreator = BookInfo.GetFileTitleCreator(Path.GetFileName(inputPath));
            if (titleCreator != null)
            {
                if (settings.UseFileName)
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

            // 表紙設定
            ApplyCoverSettings(inputPath, settings, bookInfo);

            // アーカイブの場合は画像情報ロード
            if (!isFile)
            {
                if (ext == "rar") imageInfoReader.LoadRarImageInfos(false);
                else              imageInfoReader.LoadZipImageInfos(false);
            }

            // 出力パスの決定
            var outDir = string.IsNullOrEmpty(outputDir)
                ? Path.GetDirectoryName(Path.GetFullPath(inputPath)) ?? "."
                : outputDir;
            var outName = settings.UseInputFileName
                ? Path.GetFileNameWithoutExtension(inputPath) + settings.OutputExtension
                : BuildOutputName(bookInfo, inputPath, settings.OutputExtension);
            var outPath = Path.Combine(outDir, outName);

            ct.ThrowIfCancellationRequested();

            // 第2パス: 変換実行
            var stream2 = ArchiveTextExtractor.GetTextInputStream(inputPath, ext, null, 0);
            if (stream2 is null)
                throw new InvalidOperationException($"テキストストリームを開けませんでした（第2パス）: {inputPath}");
            using var reader2 = new StreamReader(stream2, encoding);

            epub3Writer.Write(converter, reader2, inputPath, ext, outPath, bookInfo, imageInfoReader);

            ArchiveTextExtractor.ClearCache(inputPath);
        }, ct);
    }

    // ───── Web (URL) conversion ───────────────────────────────────────────────

    /// <summary>Web小説URLを変換する。</summary>
    public async Task ConvertUrlAsync(
        string url,
        string? outputDir,
        LocalConvertSettingsViewModel settings,
        NarouFormatSettings narouSettings,
        string webConfigDir,
        int downloadIntervalMs,
        IProgress<int>? progress,
        CancellationToken ct)
    {
        var outDir = string.IsNullOrEmpty(outputDir) ? "." : outputDir;

        // HTML取得 + 画像ダウンロード (async)
        var (lines, webConverter) = await WebAozoraConverter.ConvertToAozoraLinesWithConverterAsync(
            url, webConfigDir, narouSettings, downloadIntervalMs, outDir, ct: ct);

        if (lines == null || lines.Count == 0)
            throw new InvalidOperationException($"Web変換に失敗しました: {url}");

        // テキストファイナライズ（in-place で lines を変更）
        var finalizer = new AozoraTextFinalizer(narouSettings);
        finalizer.Finalize(lines);
        var joinedText = string.Join("\n", lines);

        await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            var epub3Writer = new Epub3Writer(TemplatePath);
            epub3Writer.ProgressCallback = v => progress?.Report(v);
            if (settings.ForKindle) epub3Writer.SetIsKindle(true);

            var converter = new AozoraEpub3Converter(epub3Writer, TemplatePath);

            var titleType = (BookInfo.TitleType)Math.Clamp(settings.TitleType, 0, 5);

            // 第1パス
            string dummySrc = Path.Combine(outDir, "web_source.txt");
            BookInfo bookInfo;
            using (var reader1 = new StringReader(joinedText))
            {
                var imageInfoReader1 = new ImageInfoReader(true, dummySrc);
                bookInfo = converter.GetBookInfo(url, reader1, imageInfoReader1, titleType, false);
            }

            settings.ApplyTo(bookInfo, converter);

            // 表紙画像設定
            if (webConverter?.CoverImagePath != null && File.Exists(webConverter.CoverImagePath))
            {
                bookInfo.CoverFileName = "cover.jpg";
                bookInfo.InsertCoverPage = true;
            }
            bookInfo.InsertTitlePage = true;

            var outName = BuildOutputName(bookInfo, url, settings.OutputExtension);
            var outPath = Path.Combine(outDir, outName);

            ct.ThrowIfCancellationRequested();

            // 第2パス
            var imageInfoReader2 = new ImageInfoReader(true, dummySrc);
            using var reader2 = new StringReader(joinedText);
            epub3Writer.Write(converter, reader2, url, "txt", outPath, bookInfo, imageInfoReader2);
        }, ct);
    }

    // ───── Helpers ────────────────────────────────────────────────────────────

    private static void ApplyCoverSettings(string inputPath, LocalConvertSettingsViewModel settings, BookInfo bookInfo)
    {
        switch (settings.CoverImageType)
        {
            case 0:
                // 先頭の挿絵
                bookInfo.CoverImageIndex = 0;
                bookInfo.CoverFileName = "";
                break;
            case 1:
                // 同名画像ファイル
                bookInfo.CoverFileName = GetSameCoverFileName(inputPath);
                break;
            case 2:
                // ファイル指定
                string path = settings.CoverImagePath;
                if (!File.Exists(path))
                {
                    string alt = Path.Combine(Path.GetDirectoryName(inputPath)!, path);
                    path = File.Exists(alt) ? alt : "";
                }
                bookInfo.CoverFileName = string.IsNullOrEmpty(path) ? null : path;
                break;
            default:
                // -1 = なし
                bookInfo.CoverFileName = null;
                bookInfo.CoverImageIndex = -1;
                break;
        }
    }

    private static string? GetSameCoverFileName(string srcFilePath)
    {
        string basePath = srcFilePath[..(srcFilePath.LastIndexOf('.') + 1)];
        foreach (string imgExt in new[] { "png", "jpg", "jpeg", "PNG", "JPG", "JPEG" })
        {
            string path = basePath + imgExt;
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private static string BuildOutputName(BookInfo info, string fallbackPath, string ext)
    {
        string? title   = info.Title?.Trim();
        string? creator = info.Creator?.Trim();

        string baseName;
        if (!string.IsNullOrEmpty(creator) && !string.IsNullOrEmpty(title))
        {
            string c = Regex.Replace(creator, @"[\\/:*?<>|""\t]", "");
            if (c.Length > 64) c = c[..64];
            string t = Regex.Replace(title, @"[\\/:*?<>|""\t]", "");
            baseName = $"[{c}] {t}";
        }
        else if (!string.IsNullOrEmpty(title))
        {
            baseName = Regex.Replace(title, @"[\\/:*?<>|""\t]", "");
        }
        else
        {
            baseName = Path.GetFileNameWithoutExtension(fallbackPath);
        }

        if (baseName.Length > 250) baseName = baseName[..250];
        return baseName + ext;
    }
}
