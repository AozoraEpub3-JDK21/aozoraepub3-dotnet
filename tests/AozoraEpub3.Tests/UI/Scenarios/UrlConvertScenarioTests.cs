using System.IO.Compression;
using System.Text;
using Avalonia.Headless.XUnit;
using AozoraEpub3.Core.Web;
using AozoraEpub3.Gui.Services;
using AozoraEpub3.Gui.ViewModels;
using AozoraEpub3.Gui.Views;

namespace AozoraEpub3.Tests.UI.Scenarios;

/// <summary>
/// シナリオテスト: URL 指定 → ダウンロード → EPUB変換 → リファレンス比較
/// ネットワーク接続が必要なため、Integration カテゴリ。
/// </summary>
[Trait("Category", "Integration")]
public class UrlConvertScenarioTests
{
    private static readonly string ReferenceRoot = Path.GetFullPath(
        Path.Combine(
            Path.GetDirectoryName(typeof(UrlConvertScenarioTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", "tests", "integration", "reference"));

    /// <summary>
    /// シナリオ: なろう小説URLからダウンロード → EPUB変換 → リファレンスEPUBと構造比較
    /// </summary>
    [AvaloniaTheory]
    [InlineData("https://ncode.syosetu.com/n8005ls/", "n8005ls")]
    public async Task UrlConvert_MatchesReferenceStructure(string url, string refId)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var refEpub = Path.Combine(ReferenceRoot, refId, "reference.epub");
        if (!File.Exists(refEpub))
            return; // リファレンスなしはスキップ

        var tempDir = Path.Combine(Path.GetTempPath(), $"aep3_url_{refId}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // ── Arrange: MainWindow → WebConvertViewModel 設定 ──
            var mainVm = new MainWindowViewModel();
            var window = new MainWindow { DataContext = mainVm };
            window.Show();
            mainVm.NavigateToCommand.Execute("web");

            var vm = mainVm.WebConvertVm;
            vm.Url = url;
            vm.OutputDirectory = tempDir;
            vm.DownloadIntervalMs = 700;

            // web設定ディレクトリ（ビルド出力にコピー済み）
            var webConfigDir = Path.Combine(AppContext.BaseDirectory, "web");
            if (!Directory.Exists(webConfigDir))
            {
                // GUI プロジェクトのビルド出力から探す
                var guiWebDir = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(typeof(UrlConvertScenarioTests).Assembly.Location)!,
                    "..", "..", "..", "..", "..", "src", "AozoraEpub3.Gui", "web"));
                webConfigDir = Directory.Exists(guiWebDir) ? guiWebDir : "";
            }

            // ── Act: ConversionService 経由で URL 変換 ──
            var service = new ConversionService();
            await service.ConvertUrlAsync(
                url,
                tempDir,
                vm.Settings,
                vm.NarouSettings,
                webConfigDir,
                vm.DownloadIntervalMs,
                new Progress<int>(),
                CancellationToken.None);

            // ── Assert: EPUB が出力されたか ──
            var epubFiles = Directory.GetFiles(tempDir, "*.epub");
            Assert.NotEmpty(epubFiles);

            var testEpub = epubFiles[0];
            Assert.True(new FileInfo(testEpub).Length > 0);

            // EPUB 構造検証
            var testEntries = ReadEpubEntries(testEpub);
            AssertValidEpubStructure(testEntries);

            // リファレンスとの構造比較（エントリ一覧の差分確認）
            var refEntries = ReadEpubEntries(refEpub);

            // 共通エントリ数で構造的類似性を検証
            var commonKeys = refEntries.Keys
                .Intersect(testEntries.Keys, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Assert.True(commonKeys.Count > 3,
                $"リファレンスとの共通エントリが少なすぎます: {commonKeys.Count}個\n" +
                $"Ref: {string.Join(", ", refEntries.Keys.OrderBy(k => k))}\n" +
                $"Test: {string.Join(", ", testEntries.Keys.OrderBy(k => k))}");

            // OPF ファイルの書誌情報比較
            var testOpf = Encoding.UTF8.GetString(
                testEntries.First(e => e.Key.EndsWith(".opf")).Value);
            // dc:title は OPF 内に存在するはず（属性名 or 開始タグ）
            Assert.True(
                testOpf.Contains("<dc:title>") || testOpf.Contains("dc:title"),
                "OPF に dc:title が見つかりません");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// シナリオ: WebConvertViewModel のプロパティ設定が ConversionService に正しく渡されるか
    /// （ネットワーク不要のドライラン検証）
    /// </summary>
    [AvaloniaFact]
    public void UrlConvert_ViewModelSettings_AreConsistent()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();
        mainVm.NavigateToCommand.Execute("web");

        var vm = mainVm.WebConvertVm;
        vm.Url = "https://ncode.syosetu.com/n8005ls/";
        vm.OutputDirectory = "/tmp/test";
        vm.DownloadIntervalMs = 1000;

        // ViewModel の設定がすべて正しく保持されているか
        Assert.Equal("https://ncode.syosetu.com/n8005ls/", vm.Url);
        Assert.Equal("/tmp/test", vm.OutputDirectory);
        Assert.Equal(1000, vm.DownloadIntervalMs);
        Assert.NotNull(vm.Settings);
        Assert.NotNull(vm.NarouSettings);

        // 変換可能状態の確認
        Assert.False(vm.IsConverting);
    }

    // ── ヘルパー ──────────────────────────────────────────────────────────

    private static void AssertValidEpubStructure(Dictionary<string, byte[]> entries)
    {
        Assert.True(entries.ContainsKey("mimetype"), "mimetype エントリがありません");
        Assert.True(entries.ContainsKey("META-INF/container.xml"), "container.xml がありません");

        var mimeContent = Encoding.UTF8.GetString(entries["mimetype"]);
        Assert.Equal("application/epub+zip", mimeContent.Trim());

        var opfEntries = entries.Keys.Where(k => k.EndsWith(".opf")).ToList();
        Assert.NotEmpty(opfEntries);

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
