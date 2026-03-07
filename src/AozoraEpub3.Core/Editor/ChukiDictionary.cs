using System.Reflection;
using System.Text.RegularExpressions;

namespace AozoraEpub3.Core.Editor;

/// <summary>サジェスト候補の1エントリ</summary>
public sealed class ChukiSuggestItem
{
    public required string DisplayName { get; init; }
    public required string Category { get; init; }
    public required string InsertText { get; init; }
    public required int CursorOffset { get; init; }
    public int Priority { get; init; }
}

/// <summary>
/// chuki_tag.txt から青空文庫注記のサジェスト辞書を構築する。
/// エディタの注記補完（［＃ 入力でポップアップ）で使用する。
/// </summary>
public sealed partial class ChukiDictionary
{
    private readonly List<ChukiSuggestItem> _items;

    public ChukiDictionary() : this(LoadEmbeddedChukiTag()) { }

    public ChukiDictionary(string chukiTagText)
    {
        _items = BuildItems(chukiTagText);
    }

    /// <summary>前方一致で候補をフィルタリング</summary>
    public IReadOnlyList<ChukiSuggestItem> Search(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return _items.OrderByDescending(i => i.Priority).Take(20).ToList();

        return _items
            .Where(i => i.DisplayName.StartsWith(prefix, StringComparison.Ordinal))
            .OrderByDescending(i => i.Priority)
            .Take(20)
            .ToList();
    }

    /// <summary>全候補を取得</summary>
    public IReadOnlyList<ChukiSuggestItem> All => _items;

    private static string LoadEmbeddedChukiTag()
    {
        var asm = typeof(ChukiDictionary).Assembly;
        var resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("chuki_tag.txt", StringComparison.Ordinal));

        if (resourceName == null)
            return "";

        using var stream = asm.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"^([^#\t\r\n][^\t]*)\t", RegexOptions.Multiline)]
    private static partial Regex ChukiEntryPattern();

    private static List<ChukiSuggestItem> BuildItems(string chukiTagText)
    {
        var items = new List<ChukiSuggestItem>();
        var seen = new HashSet<string>();

        // 組み込み高優先度候補（頻出注記）
        AddBuiltinItems(items, seen);

        // chuki_tag.txt からパース
        if (!string.IsNullOrEmpty(chukiTagText))
        {
            foreach (Match m in ChukiEntryPattern().Matches(chukiTagText))
            {
                var name = m.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(name) || seen.Contains(name))
                    continue;

                seen.Add(name);

                var category = CategorizeChuki(name);
                var (insertText, cursorOffset) = BuildInsertText(name);

                items.Add(new ChukiSuggestItem
                {
                    DisplayName = name,
                    Category = category,
                    InsertText = insertText,
                    CursorOffset = cursorOffset,
                    Priority = 0
                });
            }
        }

        return items;
    }

    private static void AddBuiltinItems(List<ChukiSuggestItem> items, HashSet<string> seen)
    {
        // 範囲注記（開始＋終了をペアで挿入）
        AddRangeItem(items, seen, "傍点", "装飾", 100);
        AddRangeItem(items, seen, "太字", "装飾", 95);
        AddRangeItem(items, seen, "斜体", "装飾", 90);
        AddRangeItem(items, seen, "大見出し", "見出し", 85);
        AddRangeItem(items, seen, "中見出し", "見出し", 80);
        AddRangeItem(items, seen, "小見出し", "見出し", 75);

        // 単体注記
        AddSingleItem(items, seen, "改ページ", "構造", 70);
        AddSingleItem(items, seen, "改丁", "構造", 65);

        // 字下げ開始・終了
        AddSingleItem(items, seen, "ここから１字下げ", "字下げ", 60);
        AddSingleItem(items, seen, "ここから２字下げ", "字下げ", 55);
        AddSingleItem(items, seen, "ここから３字下げ", "字下げ", 50);
        AddSingleItem(items, seen, "ここで字下げ終わり", "字下げ", 58);
        AddSingleItem(items, seen, "ここから地付き", "字詰め", 45);
        AddSingleItem(items, seen, "ここで地付き終わり", "字詰め", 43);
        AddSingleItem(items, seen, "ここから地寄せ", "字詰め", 40);
        AddSingleItem(items, seen, "ここで地寄せ終わり", "字詰め", 38);
    }

    private static void AddRangeItem(List<ChukiSuggestItem> items, HashSet<string> seen, string name, string category, int priority)
    {
        if (seen.Contains(name)) return;
        seen.Add(name);
        seen.Add(name + "終わり");

        var insertText = $"［＃{name}］［＃{name}終わり］";
        var cursorOffset = name.Length + 3; // ［＃name］の直後

        items.Add(new ChukiSuggestItem
        {
            DisplayName = name,
            Category = category,
            InsertText = insertText,
            CursorOffset = cursorOffset,
            Priority = priority
        });
    }

    private static void AddSingleItem(List<ChukiSuggestItem> items, HashSet<string> seen, string name, string category, int priority)
    {
        if (seen.Contains(name)) return;
        seen.Add(name);

        var insertText = $"［＃{name}］";
        var cursorOffset = insertText.Length;

        items.Add(new ChukiSuggestItem
        {
            DisplayName = name,
            Category = category,
            InsertText = insertText,
            CursorOffset = cursorOffset,
            Priority = priority
        });
    }

    private static string CategorizeChuki(string name)
    {
        if (name.Contains("見出し")) return "見出し";
        if (name.Contains("傍点") || name.Contains("太字") || name.Contains("斜体") || name.Contains("上付き") || name.Contains("下付き")) return "装飾";
        if (name.Contains("字下げ") || name.Contains("地付き") || name.Contains("地寄せ") || name.Contains("字上げ")) return "字下げ";
        if (name.Contains("改ページ") || name.Contains("改丁") || name.Contains("改段")) return "構造";
        if (name.Contains("訓点") || name.Contains("返り点")) return "訓点";
        return "その他";
    }

    private static (string insertText, int cursorOffset) BuildInsertText(string name)
    {
        // 「終わり」で終わる注記は単体（閉じ側）
        if (name.EndsWith("終わり"))
        {
            var text = $"［＃{name}］";
            return (text, text.Length);
        }

        // 「ここから」で始まる注記は単体（開始側）
        if (name.StartsWith("ここから") || name.StartsWith("ここで"))
        {
            var text = $"［＃{name}］";
            return (text, text.Length);
        }

        // chuki_tag.txt で「…終わり」のペアが存在するかチェック
        // ここでは簡易的に、装飾系・見出し系はペアとして扱う
        if (name.Contains("見出し") && !name.Contains("窓") && !name.Contains("同行"))
        {
            var text = $"［＃{name}］［＃{name}終わり］";
            return (text, name.Length + 3);
        }

        if (name.Contains("傍点") || name == "太字" || name == "斜体")
        {
            var text = $"［＃{name}］［＃{name}終わり］";
            return (text, name.Length + 3);
        }

        // その他は単体
        var singleText = $"［＃{name}］";
        return (singleText, singleText.Length);
    }
}
