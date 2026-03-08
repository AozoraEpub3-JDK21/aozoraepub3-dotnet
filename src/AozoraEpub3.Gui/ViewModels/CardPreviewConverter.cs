using System.Globalization;
using Avalonia.Data.Converters;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>
/// カード本文を先頭40文字のプレビューテキストに変換する。
/// StoryCard.GetPreviewText() はメソッドなので AXAML バインディングに直接使えないため。
/// </summary>
public sealed class CardPreviewConverter : IValueConverter
{
    public static readonly CardPreviewConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string body) return "";
        const int maxChars = 40;
        var clean = body.Replace("\n", " ");
        return clean.Length <= maxChars ? clean : clean[..maxChars] + "…";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
