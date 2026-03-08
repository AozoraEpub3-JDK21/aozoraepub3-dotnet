using System.Text.Json;

namespace AozoraEpub3.Core.Editor;

/// <summary>
/// ユーザー定義の注記・スニペット辞書エントリ。
/// </summary>
public sealed class CustomDictionaryEntry
{
    /// <summary>表示名（サジェスト候補に表示）</summary>
    public string Name { get; set; } = "";

    /// <summary>カテゴリ（グループ分け用）</summary>
    public string Category { get; set; } = "ユーザー定義";

    /// <summary>挿入テキスト</summary>
    public string InsertText { get; set; } = "";

    /// <summary>挿入後のカーソル位置（先頭からのオフセット）。0なら末尾。</summary>
    public int CursorOffset { get; set; }

    /// <summary>行レベル挿入（行頭に挿入し改行を付加）</summary>
    public bool IsLineLevel { get; set; }
}

/// <summary>
/// ユーザー定義辞書ファイル（JSON）のルートモデル。
/// </summary>
public sealed class CustomDictionaryFile
{
    /// <summary>辞書の表示名</summary>
    public string Name { get; set; } = "";

    /// <summary>辞書エントリのリスト</summary>
    public List<CustomDictionaryEntry> Entries { get; set; } = [];
}

/// <summary>
/// カスタム辞書サービス（E6-6）。
/// ユーザー定義の注記・スニペット辞書を JSON から読み込み、
/// EditorSuggestService のサジェスト候補に統合する。
/// </summary>
public sealed class CustomDictionaryService
{
    private readonly List<CustomDictionaryEntry> _entries = [];

    /// <summary>読み込まれた全エントリ</summary>
    public IReadOnlyList<CustomDictionaryEntry> Entries => _entries;

    /// <summary>JSON ファイルからカスタム辞書を読み込む。</summary>
    public void LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return;

        var json = File.ReadAllText(filePath);
        LoadFromJson(json);
    }

    /// <summary>JSON 文字列からカスタム辞書を読み込む。</summary>
    public void LoadFromJson(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var dict = JsonSerializer.Deserialize<CustomDictionaryFile>(json, options);
        if (dict?.Entries == null) return;

        foreach (var entry in dict.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.Name)) continue;
            if (entry.CursorOffset == 0)
                entry.CursorOffset = entry.InsertText.Length;
            _entries.Add(entry);
        }
    }

    /// <summary>全エントリをクリア。</summary>
    public void Clear() => _entries.Clear();

    /// <summary>カスタム辞書エントリを ChukiSuggestItem に変換する。</summary>
    public IReadOnlyList<ChukiSuggestItem> ToSuggestItems()
    {
        return _entries.Select(e => new ChukiSuggestItem
        {
            DisplayName = e.Name,
            Category = e.Category,
            InsertText = e.InsertText,
            CursorOffset = e.CursorOffset,
            Priority = 200 // ユーザー定義は高優先度
        }).ToList();
    }

    /// <summary>カスタム辞書エントリを SnippetResult に変換する。</summary>
    public SnippetResult? GetSnippet(string name)
    {
        var entry = _entries.FirstOrDefault(e =>
            e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (entry == null) return null;

        return new SnippetResult
        {
            TextToInsert = entry.InsertText,
            CursorOffset = entry.CursorOffset,
            IsLineLevel = entry.IsLineLevel
        };
    }

    /// <summary>前方一致でフィルタリングしたエントリを返す。</summary>
    public IReadOnlyList<CustomDictionaryEntry> Search(string prefix)
    {
        if (string.IsNullOrEmpty(prefix))
            return _entries;

        return _entries
            .Where(e => e.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>デフォルトのカスタム辞書パスを返す。</summary>
    public static string GetDefaultDictionaryPath()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AozoraEpub3");
        return Path.Combine(appData, "custom_dictionary.json");
    }

    /// <summary>サンプル辞書 JSON を生成する。</summary>
    public static string GenerateSampleJson()
    {
        var sample = new CustomDictionaryFile
        {
            Name = "マイ辞書",
            Entries =
            [
                new CustomDictionaryEntry
                {
                    Name = "キャラ名ルビ",
                    Category = "固有名詞",
                    InsertText = "｜山田太郎《やまだたろう》",
                    CursorOffset = 14
                },
                new CustomDictionaryEntry
                {
                    Name = "場面転換",
                    Category = "構造",
                    InsertText = "　　　　◇　◇　◇",
                    IsLineLevel = true
                },
                new CustomDictionaryEntry
                {
                    Name = "回想開始",
                    Category = "演出",
                    InsertText = "［＃ここから１字下げ］\n――あのときのことを、思い出す。\n",
                    CursorOffset = 15
                }
            ]
        };

        return JsonSerializer.Serialize(sample, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }
}
