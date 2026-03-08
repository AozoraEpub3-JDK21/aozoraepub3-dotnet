using System.IO.Compression;
using System.Text;
using Avalonia.Headless.XUnit;
using AozoraEpub3.Core.Editor;
using AozoraEpub3.Gui.Services;
using AozoraEpub3.Gui.ViewModels;
using AozoraEpub3.Gui.Views;

namespace AozoraEpub3.Tests.UI.Scenarios;

/// <summary>
/// シナリオテスト: 執筆 → EPUB出力 → プレビュー → epubcheck
/// CardEditor (本にする) で執筆 → EPUB変換 → 検証のフルフローを検証する。
/// </summary>
[Trait("Category", "Scenario")]
public class WritingToEpubScenarioTests
{
    /// <summary>
    /// シナリオ: カードモードで執筆 → EPUB変換 → 有効なEPUBが出力される
    /// </summary>
    [AvaloniaFact]
    public async Task CardEditor_WriteAndConvert_ProducesValidEpub()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var tempDir = Path.Combine(Path.GetTempPath(), $"aep3_write_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // ── Arrange: MainWindow + CardEditorViewModel でプロジェクト作成 ──
            var mainVm = new MainWindowViewModel();
            var window = new MainWindow { DataContext = mainVm };
            window.Show();
            mainVm.NavigateToCommand.Execute("project");

            var vm = mainVm.CardEditorVm;

            // ダイアログをモックして一時ディレクトリを返す
            var projectDir = Path.Combine(tempDir, "test-project.aep3proj");
            vm.SaveProjectAsRequested += _ => Task.FromResult<string?>(projectDir);

            // プロジェクト作成
            vm.NewProjectCommand.Execute(null);
            await Task.Delay(500); // async コマンドの完了待ち

            Assert.True(vm.IsProjectLoaded, "プロジェクトが作成されませんでした");
            Assert.NotEmpty(vm.TreeItems);

            // ── Act: カードに内容を書き込む ──
            // 最初のエピソードカードを選択
            var episodeCard = vm.TreeItems
                .FirstOrDefault(t => t.DisplayTitle.Contains("第1話") || t.DisplayTitle.Contains("エピソード"));
            if (episodeCard != null)
            {
                vm.SelectTreeItemCommand.Execute(episodeCard);
                await Task.Delay(100);

                vm.ActiveCardTitle = "第一章　旅立ち";
                vm.ActiveCardBody = """
                    　山田太郎は朝早く目を覚ました。
                    　今日から長い旅が始まる。
                    　彼は荷物をまとめ、家を出た。

                    　外は春の《はる》の陽気《ようき》に包まれていた。
                    　桜《さくら》の花びらが風に舞う。

                    　「さあ、行こう」

                    　太郎は一歩を踏み出した。
                    """;
            }

            // 新しいエピソードを追加
            vm.AddEpisodeCommand.Execute(null);
            await Task.Delay(100);

            var newEpisode = vm.TreeItems.LastOrDefault();
            if (newEpisode != null)
            {
                vm.SelectTreeItemCommand.Execute(newEpisode);
                await Task.Delay(100);

                vm.ActiveCardTitle = "第二章　出会い";
                vm.ActiveCardBody = """
                    　旅を始めて三日が経った。
                    　太郎は山道を歩いていた。

                    　突然、目の前に一人の少女が現れた。
                    　「あなた、旅人？」
                    　少女は不思議そうに太郎を見つめた。

                    　「ええ、東の街へ向かっているところです」
                    """;
            }

            // プロジェクト保存
            vm.SaveProjectCommand.Execute(null);
            await Task.Delay(200);

            // ── EPUB 変換: ProjectCombineService + ConversionService 直接実行 ──
            var combineService = new ProjectCombineService();
            var projectService = new ProjectService();
            var projectData = projectService.Load(projectDir);
            Assert.NotNull(projectData);

            var combinedText = combineService.Combine(projectDir, projectData);
            Assert.NotEmpty(combinedText);

            // 一時テキストファイルに書き出し
            var tempTxtPath = Path.Combine(tempDir, "combined.txt");
            await File.WriteAllTextAsync(tempTxtPath, combinedText, Encoding.UTF8);

            // EPUB に変換
            var settings = new LocalConvertSettingsViewModel { InputEncoding = "UTF-8" };
            var service = new ConversionService();
            await service.ConvertFileAsync(
                tempTxtPath,
                tempDir,
                settings,
                new Progress<int>(),
                CancellationToken.None);

            // ── Assert: EPUB が出力されたか ──
            var epubFiles = Directory.GetFiles(tempDir, "*.epub");
            Assert.NotEmpty(epubFiles);

            var epubPath = epubFiles[0];
            Assert.True(new FileInfo(epubPath).Length > 0, "EPUB ファイルが空です");

            // EPUB 構造検証
            var entries = ReadEpubEntries(epubPath);
            AssertValidEpubStructure(entries);

            // コンテンツ検証
            var xhtmlEntries = entries
                .Where(e => e.Key.EndsWith(".xhtml") && !e.Key.Contains("nav") && !e.Key.Contains("title"))
                .ToList();
            Assert.NotEmpty(xhtmlEntries);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// シナリオ: EPUB → PreviewViewModel で開く → 基本プロパティ検証
    /// </summary>
    [AvaloniaFact]
    public async Task Preview_OpenEpub_LoadsMetadata()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var tempDir = Path.Combine(Path.GetTempPath(), $"aep3_prev_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // まず EPUB を作成
            var epubPath = await CreateTestEpub(tempDir);

            // MainWindow → PreviewViewModel 経由で開く
            var mainVm = new MainWindowViewModel();
            var window = new MainWindow { DataContext = mainVm };
            window.Show();

            mainVm.OpenPreview(epubPath);

            var vm = mainVm.PreviewVm;
            Assert.True(vm.IsEpubLoaded, "EPUB がロードされませんでした");
            Assert.Equal(epubPath, vm.EpubFilePath);
            Assert.NotEmpty(vm.CurrentPageDisplay);

            // プレビュー画面に遷移していることを確認
            Assert.IsType<PreviewViewModel>(mainVm.CurrentPage);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// シナリオ: EPUB → epubcheck 検証 OK
    /// epubcheck.jar がない環境でも「jarが見つからない」結果が正しく返ることを確認。
    /// </summary>
    [AvaloniaFact]
    public async Task Validate_WithMissingJar_ReportsError()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        var vm = mainVm.ValidateVm;
        vm.EpubFilePath = "/nonexistent/test.epub";
        vm.JarPath = "/nonexistent/epubcheck.jar";

        // 実行
        var result = await EpubcheckService.RunAsync(
            vm.EpubFilePath, vm.JarPath);

        // JAR が見つからない場合のエラー確認
        Assert.Contains("not found", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// シナリオ: EPUB → epubcheck 検証 OK パターン（JAR が存在する環境のみ）
    /// </summary>
    [AvaloniaFact]
    public async Task Validate_ValidEpub_PassesEpubcheck()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        // epubcheck.jar の存在チェック
        var jarPath = FindEpubcheckJar();
        if (jarPath == null)
            return; // epubcheck がない環境ではスキップ

        var tempDir = Path.Combine(Path.GetTempPath(), $"aep3_check_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var epubPath = await CreateTestEpub(tempDir);

            // MainWindow → ValidateViewModel 経由で検証
            var mainVm = new MainWindowViewModel();
            var window = new MainWindow { DataContext = mainVm };
            window.Show();
            mainVm.NavigateToCommand.Execute("validate");

            var vm = mainVm.ValidateVm;
            var result = await EpubcheckService.RunAsync(epubPath, jarPath);

            // エラーが0件であること
            var errors = result.Messages.Where(m => m.IsError).ToList();
            Assert.Empty(errors);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    /// <summary>
    /// シナリオ: 意図的に壊した EPUB → epubcheck NG パターン
    /// </summary>
    [AvaloniaFact]
    public async Task Validate_InvalidEpub_ReportsErrors()
    {
        // epubcheck.jar の存在チェック
        var jarPath = FindEpubcheckJar();
        if (jarPath == null)
            return;

        var tempDir = Path.Combine(Path.GetTempPath(), $"aep3_ng_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // 意図的に壊した EPUB を作成（空のZIPにmimetypeだけ入れる）
            var brokenEpub = Path.Combine(tempDir, "broken.epub");
            using (var zip = ZipFile.Open(brokenEpub, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("mimetype");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("application/epub+zip");
                // container.xml や OPF を意図的に省略
            }

            var mainVm = new MainWindowViewModel();
            var window = new MainWindow { DataContext = mainVm };
            window.Show();

            var result = await EpubcheckService.RunAsync(brokenEpub, jarPath);

            // エラーが検出されること
            Assert.True(result.Messages.Any(m => m.IsError),
                $"壊れた EPUB なのにエラーが検出されませんでした。Summary: {result.Summary}");
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    // ── ヘルパー ──────────────────────────────────────────────────────────

    private static async Task<string> CreateTestEpub(string outputDir)
    {
        var inputPath = Path.Combine(outputDir, "test_input.txt");
        var aozoraText = """
            テスト作品
            テスト著者

            ------

            　これはテスト用の青空文庫形式テキストです。
            　二段落目です。
            """;
        await File.WriteAllTextAsync(inputPath, aozoraText, Encoding.UTF8);

        var settings = new LocalConvertSettingsViewModel { InputEncoding = "UTF-8" };
        var service = new ConversionService();
        await service.ConvertFileAsync(
            inputPath, outputDir, settings, new Progress<int>(), CancellationToken.None);

        var epubFiles = Directory.GetFiles(outputDir, "*.epub");
        if (epubFiles.Length == 0)
            throw new InvalidOperationException("テスト用 EPUB の生成に失敗しました");
        return epubFiles[0];
    }

    private static string? FindEpubcheckJar()
    {
        // よくある配置場所をチェック
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "epubcheck.jar"),
            Path.Combine(AppContext.BaseDirectory, "epubcheck", "epubcheck.jar"),
            @"D:\tools\epubcheck\epubcheck.jar",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "epubcheck", "epubcheck.jar"),
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static void AssertValidEpubStructure(Dictionary<string, byte[]> entries)
    {
        Assert.True(entries.ContainsKey("mimetype"));
        Assert.True(entries.ContainsKey("META-INF/container.xml"));

        var mimeContent = Encoding.UTF8.GetString(entries["mimetype"]);
        Assert.Equal("application/epub+zip", mimeContent.Trim());

        Assert.True(entries.Keys.Any(k => k.EndsWith(".opf")));
        Assert.True(entries.Keys.Any(k => k.EndsWith(".xhtml")));
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
