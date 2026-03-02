using System;
using System.Text;
using Avalonia;

namespace AozoraEpub3.Gui;

class Program
{
    // Avalonia requires STA thread on Windows
    [STAThread]
    public static void Main(string[] args)
    {
        // Required for Shift-JIS (MS932) input file decoding
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
