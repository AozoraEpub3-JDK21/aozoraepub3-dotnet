namespace AozoraEpub3.Core.Web;

/// <summary>
/// サイト別アダプタの解決を行うファクトリ。
/// 未対応サイトはGenericへフォールバックする。
/// </summary>
internal static class WebSiteAdapterFactory
{
    private static readonly IReadOnlyList<IWebSiteAdapter> Adapters =
    [
        new NarouWebSiteAdapter(),
        new KakuyomuWebSiteAdapter(),
        new GenericWebSiteAdapter(),
    ];

    public static IWebSiteAdapter Resolve(string fqdn)
    {
        foreach (var adapter in Adapters)
        {
            if (adapter.CanHandle(fqdn))
                return adapter;
        }

        return Adapters[^1];
    }
}

internal abstract class WebSiteAdapterBase : IWebSiteAdapter
{
    public abstract string Name { get; }
    public abstract bool CanHandle(string fqdn);

    public virtual string NormalizeEntryUrl(string urlString) => urlString;

    public virtual Task<List<string>?> ConvertAsync(
        WebAozoraConverter converter,
        string urlString,
        CancellationToken ct) =>
        converter.ConvertWithLegacyPipelineAsync(urlString, ct);
}

/// <summary>小説家になろう用（ncode.syosetu.com）</summary>
internal sealed class NarouWebSiteAdapter : WebSiteAdapterBase
{
    public override string Name => "narou";

    public override bool CanHandle(string fqdn) =>
        fqdn.Equals("ncode.syosetu.com", StringComparison.OrdinalIgnoreCase);
}

/// <summary>カクヨム用（kakuyomu.jp）</summary>
internal sealed class KakuyomuWebSiteAdapter : WebSiteAdapterBase
{
    public override string Name => "kakuyomu";

    public override bool CanHandle(string fqdn) =>
        fqdn.Equals("kakuyomu.jp", StringComparison.OrdinalIgnoreCase);
}

/// <summary>extract.txtベースの汎用サイト</summary>
internal sealed class GenericWebSiteAdapter : WebSiteAdapterBase
{
    public override string Name => "generic";

    public override bool CanHandle(string fqdn) => true;
}

