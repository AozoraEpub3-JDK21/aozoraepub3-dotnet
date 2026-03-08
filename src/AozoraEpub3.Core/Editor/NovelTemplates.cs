namespace AozoraEpub3.Core.Editor;

/// <summary>
/// 新規作成テンプレート。
/// 空白テキストの代わりに構造化されたサンプルを挿入する。
/// </summary>
public static class NovelTemplates
{
    public sealed record Template(string Id, string DisplayName, string Content);

    public static readonly Template Empty = new("empty", "空", "");

    public static readonly Template Novel = new("novel", "小説テンプレート", """
        # タイトルをここに

        著者名をここに

        ---

        ## はじめに

        ここにあらすじを書いてください。

        ---

        # 第一章　タイトルをここに

        　本文をここに書いてください。
        　「セリフはこのように」

        　段落の最初は全角スペースから始まります。
        　**強調**も使えます。

        ---

        # 第二章　続きのタイトル

        　この作品は続きます……

        """);

    public static readonly Template ShortStory = new("short", "短編テンプレート", """
        # タイトルをここに

        著者名をここに

        ---

        　物語の始まりをここに書いてください。

        　段落の先頭には全角スペースを入れます。
        　「セリフはこのように書きます」と語った。

        　|漢字《ルビ》のように読み仮名を振れます。
        　《《傍点》》で強調できます。

        """);

    public static readonly Template SeriesFirst = new("series", "連載第1話テンプレート", """
        # 作品タイトル

        著者名をここに

        ---

        # 第1話　タイトルをここに

        　第1話の本文をここに書いてください。

        　新しいシーンに入るときは、空行を入れます。

        　「セリフはこのように」

        ---

        　次回予告やあとがきをここに。

        """);

    /// <summary>全テンプレートのリスト</summary>
    public static readonly Template[] All = [Empty, Novel, ShortStory, SeriesFirst];

    /// <summary>IDからテンプレートを取得。見つからなければ空。</summary>
    public static Template GetById(string id)
        => Array.Find(All, t => t.Id == id) ?? Empty;
}
