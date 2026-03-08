using Avalonia;
using Avalonia.Headless;
using AozoraEpub3.Gui;

[assembly: AvaloniaTestApplication(typeof(AozoraEpub3.Tests.UI.TestAppBuilder))]

namespace AozoraEpub3.Tests.UI;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
