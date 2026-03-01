using System.Text.RegularExpressions;

namespace AozoraEpub3.Core.Web;

public enum ExtractId
{
    COOKIE, PAGE_REGEX,
    SERIES, TITLE, AUTHOR, DESCRIPTION, COVER_IMG, COVER_HREF,
    PAGE_NUM, PAGE_URL,
    CHILD_NODE, HREF, HREF_REGEX, UPDATE, SUB_UPDATE,
    CONTENT_UPDATE_LIST, CONTENT_PUBLISH_LIST, SUBTITLE_LIST,
    CONTENT_CHAPTER, CONTENT_SUBTITLE, CONTENT_IMG, CONTENT_UPDATE,
    CONTENT_ARTICLE, CONTENT_PREAMBLE, CONTENT_APPENDIX,
    CONTENT_ARTICLE_SEPARATOR,
    CONTENT_ARTICLE_START, CONTENT_ARTICLE_END,
    CONTENT_PREAMBLE_START, CONTENT_PREAMBLE_END,
    CONTENT_APPENDIX_START, CONTENT_APPENDIX_END
}

/// <summary>
/// CSSクエリと抽出パラメータを保持するクラス。
/// extract.txt の各行を解析して生成する。
/// </summary>
public class ExtractInfo
{
    /// <summary>JsoupのDocumentでselectするクエリ</summary>
    public string Query { get; }

    /// <summary>利用するelementsの位置（nullなら全要素）</summary>
    public int[]? Idx { get; }

    private readonly Regex? _pattern;
    private readonly string? _replacement;

    public bool HasPattern => _pattern != null;
    public bool IsReplaceable => _pattern != null && _replacement != null;

    /// <summary>
    /// queryString を "query[:idx1[:idx2]]" 形式でパースする。
    /// 末尾の連続する整数値インデックスを Idx に、残りを Query に設定。
    /// CSS疑似クラス (:not(), :first-child 等) との区別のため末尾から走査する。
    /// </summary>
    public ExtractInfo(string queryString, Regex? pattern, string? replacement)
    {
        string[] values = queryString.Split(':');

        // 末尾から数値インデックスを収集
        int idxCount = 0;
        for (int i = values.Length - 1; i >= 1; i--)
        {
            if (int.TryParse(values[i], out _)) idxCount++;
            else break;
        }

        if (idxCount > 0)
        {
            Query = string.Join(":", values[..^idxCount]);
            Idx = new int[idxCount];
            for (int i = 0; i < idxCount; i++)
                Idx[i] = int.Parse(values[values.Length - idxCount + i]);
        }
        else
        {
            Query = queryString;
            Idx = null;
        }

        _pattern = pattern;
        _replacement = replacement?.Replace("\\n", "\n");
    }

    public string Replace(string str)
    {
        if (_pattern == null || _replacement == null) return str;
        return _pattern.Replace(str, _replacement);
    }

    public bool Matches(string str)
    {
        if (_pattern == null) return false;
        return _pattern.IsMatch(str);
    }
}
