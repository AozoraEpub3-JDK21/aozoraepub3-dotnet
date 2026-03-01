using System.Net;
using System.Text.Json;
using AozoraEpub3.Core.Converter;

namespace AozoraEpub3.Core.Web.Api;

/// <summary>
/// なろう小説API クライアント
/// 参考: https://dev.syosetu.com/man/api/
/// </summary>
public class NarouApiClient
{
    private const string ApiEndpoint = "https://api.syosetu.com/novelapi/api/";
    private const string ApiEndpointR18 = "https://api.syosetu.com/novel18api/api/";

    private static readonly HttpClient _http = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
    })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static NarouApiClient()
    {
        _http.DefaultRequestHeaders.Add("User-Agent", "AozoraEpub3/1.0");
    }

    /// <summary>Nコードから作品メタデータを取得</summary>
    public async Task<NovelMetadata?> GetNovelMetadataAsync(string ncode, bool isR18 = false)
    {
        ncode = ncode.ToLowerInvariant();
        string endpoint = isR18 ? ApiEndpointR18 : ApiEndpoint;
        string url = $"{endpoint}?ncode={ncode}&out=json&of=t-n-u-w-s-nt-e-ga&gzip=5";

        LogAppender.Println($"なろうAPI: メタデータ取得中... {ncode}");

        try
        {
            string json = await _http.GetStringAsync(url);
            return ParseMetadata(json);
        }
        catch (Exception e)
        {
            LogAppender.Println($"なろうAPI エラー: {e.Message}");
            return null;
        }
    }

    private static NovelMetadata? ParseMetadata(string json)
    {
        try
        {
            // レスポンスは [{"allcount":1}, {...作品データ...}] の配列形式
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() < 2)
                return null;

            var data = doc.RootElement[1];
            var meta = new NovelMetadata();

            if (data.TryGetProperty("ncode", out var ncode)) meta.Ncode = ncode.GetString() ?? "";
            if (data.TryGetProperty("title", out var title)) meta.Title = title.GetString() ?? "";
            if (data.TryGetProperty("writer", out var writer)) meta.Writer = writer.GetString() ?? "";
            if (data.TryGetProperty("story", out var story)) meta.Story = story.GetString() ?? "";
            if (data.TryGetProperty("general_all_no", out var allNo)) meta.GeneralAllNo = allNo.GetInt32();
            if (data.TryGetProperty("end", out var end)) meta.End = end.GetInt32();
            if (data.TryGetProperty("novel_type", out var nt)) meta.NovelType = nt.GetInt32();
            if (data.TryGetProperty("length", out var len)) meta.Length = len.GetInt32();

            LogAppender.Println($"なろうAPI: 取得成功 - {meta.Title}");
            return meta;
        }
        catch (Exception e)
        {
            LogAppender.Println($"なろうAPI JSONパースエラー: {e.Message}");
            return null;
        }
    }
}
