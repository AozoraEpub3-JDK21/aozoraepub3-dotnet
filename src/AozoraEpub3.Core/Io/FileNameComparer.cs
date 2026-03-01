namespace AozoraEpub3.Core.Io;

/// <summary>ファイル名並び替え用コンパレータ</summary>
public sealed class FileNameComparer : IComparer<string>
{
    public static readonly FileNameComparer Instance = new();

    private FileNameComparer() { }

    public int Compare(string? x, string? y)
    {
        if (x == null && y == null) return 0;
        if (x == null) return -1;
        if (y == null) return 1;

        string s1 = x.ToLowerInvariant();
        string s2 = y.ToLowerInvariant();
        int len = Math.Min(s1.Length, s2.Length);

        for (int i = 0; i < len; i++)
        {
            int diff = Replace(s1[i]) - Replace(s2[i]);
            if (diff != 0) return diff;
        }
        return s1.Length - s2.Length;
    }

    private static char Replace(char c) => c switch
    {
        '_' => '/',
        '一' => '一',
        '二' => (char)('一' + 1),
        '三' => (char)('一' + 2),
        '四' => (char)('一' + 3),
        '五' => (char)('一' + 4),
        '六' => (char)('一' + 5),
        '七' => (char)('一' + 6),
        '八' => (char)('一' + 7),
        '九' => (char)('一' + 8),
        '十' => (char)('一' + 9),
        '上' => '上',
        '前' => (char)('上' + 1),
        '中' => (char)('上' + 2),
        '下' => (char)('上' + 3),
        '後' => (char)('上' + 4),
        _ => c,
    };
}
