using System.Text.Json;
using AozoraEpub3.Core.Editor;

namespace AozoraEpub3.Gui.Services;

/// <summary>
/// カードコレクションの保存/読み込み。
/// 保存先: %APPDATA%\AozoraEpub3\cards\{collection-id}.json
/// </summary>
public static class CardStorageService
{
    private static readonly string BaseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AozoraEpub3", "cards");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static CardCollection? Load(string collectionId)
    {
        var path = GetFilePath(collectionId);
        if (!File.Exists(path)) return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<CardCollection>(json, JsonOptions);
    }

    public static void Save(CardCollection collection, string collectionId)
    {
        Directory.CreateDirectory(BaseDir);
        var json = JsonSerializer.Serialize(collection, JsonOptions);
        File.WriteAllText(GetFilePath(collectionId), json);
    }

    /// <summary>保存済みコレクション一覧（IDリスト）</summary>
    public static List<string> ListCollections()
    {
        if (!Directory.Exists(BaseDir)) return [];
        return Directory.GetFiles(BaseDir, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name != null)
            .Cast<string>()
            .ToList();
    }

    /// <summary>新規コレクションを作成し、IDを返す</summary>
    public static string CreateNew(string title, string author)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        var collection = new CardCollection
        {
            Title = title,
            Author = author
        };
        Save(collection, id);
        return id;
    }

    private static string GetFilePath(string collectionId)
        => Path.Combine(BaseDir, $"{collectionId}.json");
}
