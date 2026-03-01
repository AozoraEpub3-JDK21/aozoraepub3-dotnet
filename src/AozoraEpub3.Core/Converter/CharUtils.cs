using System.Text.RegularExpressions;

namespace AozoraEpub3.Core.Converter;

/// <summary>文字変換と判別関連の関数定義クラス</summary>
public static class CharUtils
{
    /// <summary>全角英数字を半角に変換</summary>
    public static string FullToHalf(string src)
    {
        var c = src.ToCharArray();
        for (int i = c.Length - 1; i >= 0; i--)
        {
            if (c[i] >= '０' && c[i] <= '９') c[i] = (char)(c[i] - '０' + '0');
            else if (c[i] >= 'Ａ' && c[i] <= 'ｚ') c[i] = (char)(c[i] - 'ａ' + 'a');
        }
        return new string(c);
    }

    /// <summary>すべて同じ文字かチェック</summary>
    public static bool IsSameChars(char[] ch, int begin, int end)
    {
        for (int i = begin + 1; i < end; i++)
            if (ch[begin] != ch[i]) return false;
        return true;
    }

    /// <summary>半角数字かチェック</summary>
    public static bool IsNum(char ch) => ch >= '0' && ch <= '9';

    /// <summary>英字かどうかをチェック 拡張ラテン文字含む 半角スペースは含まない</summary>
    public static bool IsHalf(char ch) => 0x21 <= ch && ch <= 0x02AF;

    public static bool IsHalf(char[] chars)
    {
        foreach (var ch in chars)
            if (!IsHalf(ch)) return false;
        return true;
    }

    /// <summary>英字かどうかをチェック 拡張ラテン文字含む 半角スペースを含む</summary>
    public static bool IsHalfSpace(char ch) => 0x20 <= ch && ch <= 0x02AF;

    public static bool IsHalfSpace(char[] chars)
    {
        foreach (var ch in chars)
            if (!IsHalfSpace(ch)) return false;
        return true;
    }

    public static bool IsFullAlpha(char ch) =>
        ('Ａ' <= ch && ch <= 'Ｚ') || ('ａ' <= ch && ch <= 'ｚ') || ('０' <= ch && ch <= '９') || ch == '＠' || ch == '＿';

    public static bool IsFullNum(char ch) => ch >= '０' && ch <= '９';

    /// <summary>ひらがなかチェック</summary>
    public static bool IsHiragana(char ch) =>
        ('ぁ' <= ch && ch <= 'ん') || ch == 'ゕ' || ch == 'ゖ' || ch == 'ー' || ch == 'ゝ' || ch == 'ゞ' || ch == 'ヽ' || ch == 'ヾ' || ch == '゛' || ch == '゜' || ch == 'ι';

    /// <summary>カタカナかチェック</summary>
    public static bool IsKatakana(char ch)
    {
        if ('ァ' <= ch && ch <= 'ヶ') return true;
        switch (ch)
        {
            case 'ァ': case 'ィ': case 'ゥ': case 'ェ': case 'ォ': case 'ヵ': case 'ㇰ': case 'ヶ': case 'ㇱ': case 'ㇲ': case 'ッ': case 'ㇳ': case 'ㇴ':
            case 'ㇵ': case 'ㇶ': case 'ㇷ': case 'ㇸ': case 'ㇹ': case 'ㇺ': case 'ャ': case 'ュ': case 'ョ': case 'ㇻ': case 'ㇼ': case 'ㇽ': case 'ㇾ': case 'ㇿ': case 'ヮ':
            case 'ー': case 'ゝ': case 'ゞ': case 'ヽ': case 'ヾ': case '゛': case '゜':
                return true;
        }
        return false;
    }

    public static bool IsSpace(string line)
    {
        foreach (char c in line)
            if (c != ' ' && c != '\u3000' && c != '\u00A0') return false;
        return true;
    }

    /// <summary>英字かどうかをチェック 拡張ラテン文字含む</summary>
    public static bool IsAlpha(char ch) =>
        ('A' <= ch && ch <= 'Z') || ('a' <= ch && ch <= 'z') || (0x80 <= ch && ch <= 0x02AF);

    /// <summary>漢字かどうかをチェック 4バイト文字対応</summary>
    public static bool IsKanji(char[] ch, int i)
    {
        switch (ch[i])
        {
            case '゛': case '゜':
                return i > 0 && ch[i - 1] == '〻';
        }
        return _IsKanji(ch, i);
    }

    private static bool _IsKanji(char[] ch, int i)
    {
        int pre = i == 0 ? -1 : ch[i - 1];
        int c = ch[i];
        int suf = i + 1 >= ch.Length ? -1 : ch[i + 1];
        switch (c)
        {
            case '〓': case '〆': case '々': case '〻':
                return true;
        }
        if (0x4E00 <= c && c <= 0x9FFF) return true;
        if (0xF900 <= c && c <= 0xFAFF) return true;
        if (0xFE00 <= c && c <= 0xFE0D) return true;
        if (pre >= 0)
        {
            if (0xDB40 == pre && 0xDD00 <= c && c <= 0xDDEF) return true;
            if (0xD87E == pre && 0xDC00 <= c && c <= 0xDE1F) return true;
            long code = ((long)pre << 16) | (c & 0xFFFFL);
            if (0xD840DC00L <= code && code <= 0xD869DEDFL) return true;
            if (0xD869DF00L <= code && code <= 0xD86EDC1FL) return true;
        }
        if (suf >= 0)
        {
            if (0xDB40 == c && 0xDD00 <= suf && suf <= 0xDDEF) return true;
            if (0xD87E == c && 0xDC00 <= suf && suf <= 0xDE1F) return true;
            long code = ((long)c << 16) | (suf & 0xFFFFL);
            if (0xD840DC00L <= code && code <= 0xD869DEDFL) return true;
            if (0xD869DF00L <= code && code <= 0xD86EDC1FL) return true;
        }
        return false;
    }

    /// <summary>ファイル名に使えない文字を'_'に置換</summary>
    public static string EscapeUrlToFile(string str) =>
        Regex.Replace(Regex.Replace(str, @"[?&]", "/"), @"[:\\*|<>""\|]", "_");

    /// <summary>前後の空白を除外</summary>
    public static string RemoveSpace(string text) =>
        Regex.Replace(Regex.Replace(text, @"^[ |　]+", ""), @"[ |　]+$", "");

    /// <summary>タグを除外</summary>
    public static string RemoveTag(string text) =>
        Regex.Replace(Regex.Replace(text, @"［＃.+?］", ""), @"<[^>]+>", "");

    /// <summary>ルビを除去</summary>
    public static string RemoveRuby(string text)
    {
        var buf = new System.Text.StringBuilder();
        var ch = text.ToCharArray();
        bool inRuby = false;
        for (int i = 0; i < ch.Length; i++)
        {
            if (inRuby)
            {
                if (ch[i] == '》' && !IsEscapedChar(ch, i)) inRuby = false;
            }
            else
            {
                switch (ch[i])
                {
                    case '｜':
                        if (IsEscapedChar(ch, i)) buf.Append(ch[i]);
                        break;
                    case '《':
                        if (IsEscapedChar(ch, i)) buf.Append(ch[i]);
                        else inRuby = true;
                        break;
                    default:
                        if (!inRuby) buf.Append(ch[i]);
                        break;
                }
            }
        }
        return buf.ToString();
    }

    /// <summary>文字がエスケープされた特殊文字ならtrue</summary>
    public static bool IsEscapedChar(char[] ch, int idx)
    {
        bool escaped = false;
        for (int i = idx - 1; i >= 0; i--)
        {
            if (ch[i] == '※') escaped = !escaped;
            else return escaped;
        }
        return escaped;
    }

    /// <summary>文字がエスケープされた特殊文字ならtrue (StringBuilder用)</summary>
    public static bool IsEscapedChar(System.Text.StringBuilder ch, int idx)
    {
        bool escaped = false;
        for (int i = idx - 1; i >= 0; i--)
        {
            if (ch[i] == '※') escaped = !escaped;
            else return escaped;
        }
        return escaped;
    }

    /// <summary>HTML特殊文字をエスケープ</summary>
    public static string EscapeHtml(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static readonly Regex _chapterTagOpenPattern = new(@"< *(img|a) [^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex _chapterTagClosePattern = new(@"< */ *(img|a)(>| [^>]*>)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>目次やタイトル用の文字列を取得</summary>
    public static string GetChapterName(string line, int maxLength, bool reduce = true)
    {
        string name = Regex.Replace(line, @"［＃.+?］", "")
            .Replace("\t", " ");
        name = Regex.Replace(name, @"^[ |　]+", "");
        name = Regex.Replace(name, @"[ |　]+$", "");
        name = Regex.Replace(name, @"※(※|《|》|［|］|〔|〕|｜)", "$1");
        if (reduce) name = Regex.Replace(name, @"(=|＝|-|―|─)+", "$1");
        name = _chapterTagOpenPattern.Replace(name, "");
        name = _chapterTagClosePattern.Replace(name, "");
        if (maxLength == 0) return name;
        return name.Length > maxLength ? name.Substring(0, maxLength) + "..." : name;
    }

    /// <summary>指定されたタグを削除</summary>
    public static string RemoveTagByName(string str, string? single, string? open, string? close)
    {
        if (str.IndexOf('<') == -1) return str;
        if (single != null) str = Regex.Replace(str, $"< *({single}) */? *>", "", RegexOptions.IgnoreCase);
        if (open != null) str = Regex.Replace(str, $"< *({open}) [^>]*>", "", RegexOptions.IgnoreCase);
        if (close != null) str = Regex.Replace(str, $"< */ *({close})(>| [^>]*>)", "", RegexOptions.IgnoreCase);
        return str;
    }

    /// <summary>BOMが文字列の先頭にある場合は除去</summary>
    public static string? RemoveBOM(string? str)
    {
        if (str != null && str.Length > 0 && str[0] == 0xFEFF)
            return str.Substring(1);
        return str;
    }
}
