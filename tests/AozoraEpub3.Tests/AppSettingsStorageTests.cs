using AozoraEpub3.Gui.Services;

namespace AozoraEpub3.Tests;

public class AppSettingsStorageTests
{
    [Fact]
    public void Load_InvalidJson_RaisesLoadFailed_AndReturnsDefault()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"aep3_settings_{Guid.NewGuid():N}.json");
        File.WriteAllText(tempPath, "{ invalid json");

        string? raised = null;
        void OnLoadFailed(string message) => raised = message;
        AppSettingsStorage.LoadFailed += OnLoadFailed;
        try
        {
            var settings = AppSettingsStorage.Load(tempPath);
            Assert.NotNull(settings);
            Assert.NotNull(raised);
            Assert.Contains("設定読込に失敗しました", raised);
        }
        finally
        {
            AppSettingsStorage.LoadFailed -= OnLoadFailed;
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    [Fact]
    public void Save_WhenTargetIsDirectory_RaisesSaveFailed_AndReturnsFalse()
    {
        var rootDir = Path.Combine(Path.GetTempPath(), $"aep3_settings_dir_{Guid.NewGuid():N}");
        var pathAsDirectory = Path.Combine(rootDir, "settings.json");
        Directory.CreateDirectory(pathAsDirectory);

        string? raised = null;
        void OnSaveFailed(string message) => raised = message;
        AppSettingsStorage.SaveFailed += OnSaveFailed;
        try
        {
            var ok = AppSettingsStorage.Save(new GuiSettings(), pathAsDirectory);
            Assert.False(ok);
            Assert.NotNull(raised);
            Assert.Contains("設定保存に失敗しました", raised);
        }
        finally
        {
            AppSettingsStorage.SaveFailed -= OnSaveFailed;
            if (Directory.Exists(rootDir)) Directory.Delete(rootDir, recursive: true);
        }
    }
}
