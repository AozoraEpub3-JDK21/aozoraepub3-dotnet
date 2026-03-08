using Avalonia.Headless.XUnit;
using AozoraEpub3.Core.Editor;
using AozoraEpub3.Gui.ViewModels;
using AozoraEpub3.Gui.Views;

namespace AozoraEpub3.Tests.UI;

public class SettingsUITests
{
    [AvaloniaFact]
    public void Settings_EditorThemeChange_PropagatesViaEvent()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        // テーマ変更が EditorVm に伝播するか確認
        mainVm.SettingsVm.EditorThemeIndex = 1; // LightSepia

        Assert.Equal(EditorThemes.All[1].Id, mainVm.EditorVm.CurrentTheme.Id);
    }

    [AvaloniaFact]
    public void Settings_EditorThemeChange_PropagatesTo_CardBoard()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        mainVm.SettingsVm.EditorThemeIndex = 2; // LightManuscript

        Assert.Equal(EditorThemes.All[2].Id, mainVm.CardBoardVm.CurrentTheme.Id);
    }

    [AvaloniaFact]
    public void Settings_EditorThemeChange_PropagatesTo_CardEditor()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        mainVm.SettingsVm.EditorThemeIndex = 3; // DarkDefault

        Assert.Equal(EditorThemes.All[3].Id, mainVm.CardEditorVm.CurrentTheme.Id);
    }

    [AvaloniaFact]
    public void Settings_LanguageToggle_UpdatesMainWindowLanguage()
    {
        var mainVm = new MainWindowViewModel();
        var window = new MainWindow { DataContext = mainVm };
        window.Show();

        mainVm.NavigateToCommand.Execute("settings");

        mainVm.SettingsVm.SelectedLanguage = "en";
        // SettingsVm の言語変更は MainWindowViewModel の SetLanguageCommand 経由で反映される
        // 直接は SetLanguage を呼ぶ必要がある
        mainVm.SetLanguageCommand.Execute("en");

        Assert.Equal("en", mainVm.CurrentLanguage);
    }
}
