namespace AozoraEpub3.Core.Web;

/// <summary>
/// narou.rb互換のフォーマット設定を管理するクラス。
/// INI形式の設定ファイル(setting_narourb.ini)を読み書きする。
/// </summary>
public class NarouFormatSettings
{
    /// <summary>前書き・後書きスタイル: "css" | "simple" | "plain"</summary>
    public string AuthorCommentStyle { get; private set; } = "css";

    /// <summary>横書きモード</summary>
    public bool EnableYokogaki { get; set; } = false;

    /// <summary>本の終了マーカー表示</summary>
    public bool EnableDisplayEndOfBook { get; set; } = true;

    /// <summary>章中表紙で「ページの左右中央」を使用</summary>
    public bool ChapterUseCenterPage { get; set; } = true;

    /// <summary>章中表紙で「柱」にタイトルを表示</summary>
    public bool ChapterUseHashira { get; set; } = true;

    /// <summary>あらすじを表紙ページに含める</summary>
    public bool IncludeStory { get; set; } = true;

    /// <summary>掲載URLを表紙ページに含める</summary>
    public bool IncludeTocUrl { get; set; } = true;

    /// <summary>更新日時を各話に表示</summary>
    public bool ShowPostDate { get; set; } = false;

    /// <summary>初回公開日を各話に表示</summary>
    public bool ShowPublishDate { get; set; } = false;

    /// <summary>行頭のかぎ括弧に二分アキを挿入</summary>
    public bool EnableHalfIndentBracket { get; set; } = true;

    /// <summary>数字の漢数字化</summary>
    public bool EnableConvertNumToKanji { get; set; } = true;

    /// <summary>漢数字の単位化</summary>
    public bool EnableKanjiNumWithUnits { get; set; } = true;

    /// <summary>単位化する際の下位桁ゼロ数</summary>
    public int KanjiNumWithUnitsLowerDigitZero { get; set; } = 3;

    /// <summary>英字の全角化</summary>
    public bool EnableAlphabetForceZenkaku { get; set; } = false;

    /// <summary>8文字未満の英単語を全角にしない</summary>
    public bool DisableAlphabetWordToZenkaku { get; set; } = false;

    /// <summary>空行を圧縮する</summary>
    public bool EnablePackBlankLine { get; set; } = true;

    /// <summary>記号の全角化</summary>
    public bool EnableConvertSymbolsToZenkaku { get; set; } = false;

    /// <summary>かぎ括弧内の自動連結</summary>
    public bool EnableAutoJoinInBrackets { get; set; } = false;

    /// <summary>行末読点での自動連結</summary>
    public bool EnableAutoJoinLine { get; set; } = false;

    /// <summary>前書き・後書きの自動検出</summary>
    public bool EnableAuthorComments { get; set; } = true;

    /// <summary>自動行頭字下げ</summary>
    public bool EnableAutoIndent { get; set; } = true;

    /// <summary>改ページ直後の見出し化</summary>
    public bool EnableEnchantMidashi { get; set; } = true;

    /// <summary>かぎ括弧の開閉チェック（警告のみ）</summary>
    public bool EnableInspectInvalidOpenCloseBrackets { get; set; } = true;

    /// <summary>ファイナライズ処理の最大ファイルサイズ（MB単位）</summary>
    public int MaxFinalizableFileSizeMB { get; set; } = 100;

    /// <summary>分数変換</summary>
    public bool EnableTransformFraction { get; set; } = false;

    /// <summary>日付変換</summary>
    public bool EnableTransformDate { get; set; } = false;

    /// <summary>日付フォーマット</summary>
    public string DateFormat { get; set; } = "%Y年%m月%d日";

    /// <summary>三点リーダー変換</summary>
    public bool EnableConvertHorizontalEllipsis { get; set; } = false;

    /// <summary>濁点フォント処理</summary>
    public bool EnableDakutenFont { get; set; } = false;

    /// <summary>長音記号の変換</summary>
    public bool EnableProlongedSoundMarkToDash { get; set; } = false;

    /// <summary>なろう独自タグの処理</summary>
    public bool EnableNarouTag { get; set; } = true;

    /// <summary>replace.txt の置換パターン（検索文字列→置換文字列のペア）</summary>
    public List<string[]> TextReplacePatterns { get; } = new();

    /// <summary>字下げの文字数を返す</summary>
    public string Indent => EnableYokogaki ? "１" : "３";

    public void SetAuthorCommentStyle(string style)
    {
        if (style is "css" or "simple" or "plain")
            AuthorCommentStyle = style;
    }

    /// <summary>INI形式の設定ファイルから読み込み</summary>
    public void Load(string filePath)
    {
        if (!File.Exists(filePath)) return;
        foreach (string rawLine in File.ReadLines(filePath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            string key = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();
            if (value.StartsWith('"') && value.EndsWith('"'))
                value = value[1..^1];
            ApplyKey(key, value);
        }
    }

    private void ApplyKey(string key, string value)
    {
        switch (key)
        {
            case "author_comment_style": SetAuthorCommentStyle(value); break;
            case "enable_yokogaki": EnableYokogaki = ToBool(value); break;
            case "enable_display_end_of_book": EnableDisplayEndOfBook = ToBool(value); break;
            case "chapter_use_center_page": ChapterUseCenterPage = ToBool(value); break;
            case "chapter_use_hashira": ChapterUseHashira = ToBool(value); break;
            case "include_story": IncludeStory = ToBool(value); break;
            case "include_toc_url": IncludeTocUrl = ToBool(value); break;
            case "show_post_date": ShowPostDate = ToBool(value); break;
            case "show_publish_date": ShowPublishDate = ToBool(value); break;
            case "enable_half_indent_bracket": EnableHalfIndentBracket = ToBool(value); break;
            case "enable_convert_num_to_kanji": EnableConvertNumToKanji = ToBool(value); break;
            case "enable_kanji_num_with_units": EnableKanjiNumWithUnits = ToBool(value); break;
            case "kanji_num_with_units_lower_digit_zero":
                if (int.TryParse(value, out int v1)) KanjiNumWithUnitsLowerDigitZero = v1; break;
            case "enable_convert_symbols_to_zenkaku": EnableConvertSymbolsToZenkaku = ToBool(value); break;
            case "enable_alphabet_force_zenkaku": EnableAlphabetForceZenkaku = ToBool(value); break;
            case "disable_alphabet_word_to_zenkaku": DisableAlphabetWordToZenkaku = ToBool(value); break;
            case "enable_pack_blank_line": EnablePackBlankLine = ToBool(value); break;
            case "enable_auto_join_in_brackets": EnableAutoJoinInBrackets = ToBool(value); break;
            case "enable_auto_join_line": EnableAutoJoinLine = ToBool(value); break;
            case "enable_author_comments": EnableAuthorComments = ToBool(value); break;
            case "enable_auto_indent": EnableAutoIndent = ToBool(value); break;
            case "enable_enchant_midashi": EnableEnchantMidashi = ToBool(value); break;
            case "enable_inspect_invalid_openclose_brackets": EnableInspectInvalidOpenCloseBrackets = ToBool(value); break;
            case "max_finalizable_file_size_mb":
                if (int.TryParse(value, out int v2)) MaxFinalizableFileSizeMB = v2; break;
            case "enable_transform_fraction": EnableTransformFraction = ToBool(value); break;
            case "enable_transform_date": EnableTransformDate = ToBool(value); break;
            case "date_format": DateFormat = value; break;
            case "enable_convert_horizontal_ellipsis": EnableConvertHorizontalEllipsis = ToBool(value); break;
            case "enable_dakuten_font": EnableDakutenFont = ToBool(value); break;
            case "enable_prolonged_sound_mark_to_dash": EnableProlongedSoundMarkToDash = ToBool(value); break;
            case "enable_narou_tag": EnableNarouTag = ToBool(value); break;
        }
    }

    private static bool ToBool(string value) =>
        value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";

    /// <summary>replace.txt（タブ区切りの置換ルール）を読み込む</summary>
    public void LoadReplacePatterns(string filePath)
    {
        TextReplacePatterns.Clear();
        if (!File.Exists(filePath)) return;
        foreach (string line in File.ReadLines(filePath))
        {
            if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
            string[] pair = line.Split('\t', 2);
            if (pair.Length == 2 && pair[0].Length > 0)
                TextReplacePatterns.Add([pair[0], pair[1]]);
        }
    }

    /// <summary>デフォルト設定ファイルを生成（存在しない場合のみ）</summary>
    public static void GenerateDefaultIfMissing(string filePath)
    {
        if (File.Exists(filePath)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, """
            ; AozoraEpub3 narou.rb互換フォーマット設定
            ; author_comment_style: css / simple / plain
            author_comment_style = "css"
            enable_yokogaki = false
            enable_display_end_of_book = true
            chapter_use_center_page = true
            chapter_use_hashira = true
            include_story = true
            include_toc_url = true
            show_post_date = false
            show_publish_date = false
            enable_half_indent_bracket = true
            enable_convert_num_to_kanji = true
            enable_kanji_num_with_units = true
            kanji_num_with_units_lower_digit_zero = 3
            enable_alphabet_force_zenkaku = false
            disable_alphabet_word_to_zenkaku = false
            enable_pack_blank_line = true
            enable_convert_symbols_to_zenkaku = false
            enable_auto_join_in_brackets = false
            enable_auto_join_line = false
            enable_author_comments = true
            enable_auto_indent = true
            enable_enchant_midashi = true
            enable_inspect_invalid_openclose_brackets = true
            enable_narou_tag = true
            """, System.Text.Encoding.UTF8);
    }
}
