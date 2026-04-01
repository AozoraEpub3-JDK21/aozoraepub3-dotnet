namespace AozoraEpub3.Core.Web;

/// <summary>
/// サイト固有のURL変換ロジックを差し替えるためのインターフェース。
/// Phase Aでは既存パイプラインへの委譲のみを行う。
/// </summary>
public interface IWebSiteAdapter
{
    /// <summary>アダプタ識別名（ログ・デバッグ用途）</summary>
    string Name { get; }

    /// <summary>対象FQDNを処理可能か判定</summary>
    bool CanHandle(string fqdn);

    /// <summary>
    /// 入口URLを正規化する。
    /// 既定実装では入力をそのまま返す（互換性優先）。
    /// </summary>
    string NormalizeEntryUrl(string urlString);

    /// <summary>
    /// URLを青空文庫行へ変換する。
    /// Phase Aでは既存パイプラインに委譲する。
    /// </summary>
    Task<List<string>?> ConvertAsync(WebAozoraConverter converter, string urlString, CancellationToken ct);
}

