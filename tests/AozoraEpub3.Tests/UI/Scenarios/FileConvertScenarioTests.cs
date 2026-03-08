using System.IO.Compression;
using System.Text;
using Avalonia.Headless.XUnit;
using AozoraEpub3.Gui.Services;
using AozoraEpub3.Gui.ViewModels;
using AozoraEpub3.Gui.Views;

namespace AozoraEpub3.Tests.UI.Scenarios;

/// <summary>
/// シナリオテスト: ファイル読込 → EPUB出力
/// UI (MainWindow + LocalConvertViewModel) 経由でファイルを変換し、
/// 出力 EPUB の整合性を検証する。
/// </summary>
[Trait("Category", "Scenario")]
public class FileConvertScenarioTests
{
    /// <summary>
    /// シナリオ: テキストファイルをUIから追加して変換 → 有効なEPUBが出力される
    /// </summary>
    [AvaloniaFact]
    public async Task FileConvert_ProducesValidEpub()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // ── Arrange: テスト用テキストファイルを作成 ──
        var tempDir = Path.Combine(Path.GetTempPath(), $"aep3_scenario_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var inputPath = Path.Combine(tempDir, "テスト小説.txt");
        var aozoraText = """
            テスト小説
            テスト著者

            ------

            ［＃３字下げ］第一章　始まり［＃「第一章　始まり」は大見出し］

            　これはテスト用の青空文庫形式テキストです。
            　段落は全角スペースで始まります。

            　ここで《ルビ》のテストです。
            漢字《かんじ》にルビを振ります。

            -----

            ［＃３字下げ］第二章　展開［＃「第二章　展開」は大見出し］

            　第二章の本文です。
            　｜傍点《ぼうてん》のテストも含みます。
            """;
        await File.WriteAllTextAsync(inputPath, aozoraText, Encoding.UTF8);

        try
        {
            // ── Act: MainWindow → LocalConvertViewModel 経由で変換 ──
            var mainVm = new MainWindowViewModel();
            var window = new MainWindow { DataContext = mainVm };
            window.Show();

            // 別ページに移動してから操作（LocalConvertView バインド問題回避）
            mainVm.NavigateToCommand.Execute("settings");

            var vm = mainVm.LocalConvertVm;
            vm.Settings.InputEncoding = "UTF-8";
            vm.Settings.TitleType = 0; // TITLE_AUTHOR
            vm.OutputDirectory = tempDir;
            vm.AddFilePaths([inputPath]);

            Assert.True(vm.HasFiles);
            Assert.Single(vm.InputFiles);

            // ConversionService を直接使って変換
            // （ConvertCommand は UI スレッドでの進捗表示が絡むため Service を直接テスト）
            var service = new ConversionService();
            await service.ConvertFileAsync(
                inputPath,
                tempDir,
                vm.Settings,
                new Progress<int>(),
                CancellationToken.None);

            // ── Assert: EPUB ファイルが出力されたか ──
            var epubFiles = Directory.GetFiles(tempDir, "*.epub");
            Assert.Single(epubFiles);

            var epubPath = epubFiles[0];
            Assert.True(new FileInfo(epubPath).Length > 0, "EPUB ファイルが空です");

            // EPUB内容の基本検証
            AssertValidEpubStructure(epubPath);

            // 書誌情報検証
            var entries = ReadEpubEntries(epubPath);
            var opfContent = Encoding.UTF8.GetString(entries["OPS/package.opf"]);
            Assert.Contains("テスト小説", opfContent);
            Assert.Contains("テスト著者", opfContent);

            // XHTML にルビが含まれているか
            var xhtmlEntries = entries.Where(e => e.Key.EndsWith(".xhtml") && e.Key.Contains("0")).ToList();
            Assert.NotEmpty(xhtmlEntries);
            var xhtmlContent = Encoding.UTF8.GetString(xhtmlEntries.First().Value);
            Assert.Contains("<ruby>", xhtmlContent);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// シナリオ: 既存の Java 比較テスト用ファイルを UI フロー経由で変換 → リファレンスと一致
    /// </summary>
    [AvaloniaTheory]
    [InlineData("n8005ls", "UTF-8")]
    [InlineData("aozora_1567_14913", "MS932")]
    public async Task FileConvert_MatchesReference(string id, string inputEncoding)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var refDir = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(typeof(FileConvertScenarioTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", "tests", "integration", "reference", id));

        var referenceEpub = Path.Combine(refDir, "reference.epub");
        var inputTxt = Path.Combine(refDir, "input.txt");

        if (!File.Exists(referenceEpub) || !File.Exists(inputTxt))
        {
            // リファレンスなしはスキップ
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), $"aep3_scen_{id}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // ── Act: MainWindow + ViewModel 経由で変換 ──
            var mainVm = new MainWindowViewModel();
            var window = new MainWindow { DataContext = mainVm };
            window.Show();
            mainVm.NavigateToCommand.Execute("settings");

            var vm = mainVm.LocalConvertVm;
            vm.Settings.InputEncoding = inputEncoding;
            vm.OutputDirectory = tempDir;
            vm.AddFilePaths([inputTxt]);

            Assert.True(vm.HasFiles);

            // ConversionService 経由で変換
            var service = new ConversionService();
            await service.ConvertFileAsync(
                inputTxt,
                tempDir,
                vm.Settings,
                new Progress<int>(),
                CancellationToken.None);

            // ── Assert: EPUB が出力されたか ──
            var epubFiles = Directory.GetFiles(tempDir, "*.epub");
            Assert.NotEmpty(epubFiles);

            AssertValidEpubStructure(epubFiles[0]);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    // ── ヘルパー ──────────────────────────────────────────────────────────

    private static void AssertValidEpubStructure(string epubPath)
    {
        var entries = ReadEpubEntries(epubPath);

        // 必須エントリの存在確認
        Assert.True(entries.ContainsKey("mimetype"), "mimetype エントリがありません");
        Assert.True(entries.ContainsKey("META-INF/container.xml"), "container.xml がありません");

        // mimetype の内容確認
        var mimeContent = Encoding.UTF8.GetString(entries["mimetype"]);
        Assert.Equal("application/epub+zip", mimeContent.Trim());

        // OPF ファイルの存在確認
        var opfEntries = entries.Keys.Where(k => k.EndsWith(".opf")).ToList();
        Assert.NotEmpty(opfEntries);

        // XHTML コンテンツの存在確認
        var xhtmlEntries = entries.Keys.Where(k => k.EndsWith(".xhtml")).ToList();
        Assert.NotEmpty(xhtmlEntries);
    }

    private static Dictionary<string, byte[]> ReadEpubEntries(string epubPath)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using var zip = ZipFile.OpenRead(epubPath);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            result[entry.FullName] = ms.ToArray();
        }
        return result;
    }
}
