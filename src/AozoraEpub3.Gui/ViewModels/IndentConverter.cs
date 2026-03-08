using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>
/// ツリーのインデントレベルを Margin に変換する。
/// Level 0 → Margin(0), Level 1 → Margin(16,0,0,0) のように左マージンを付ける。
/// </summary>
public sealed class IndentConverter : IValueConverter
{
    public static readonly IndentConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int level)
            return new Thickness(level * 16, 0, 0, 0);
        return new Thickness(0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
