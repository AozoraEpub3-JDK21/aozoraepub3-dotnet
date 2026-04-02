using System.Text;
using System.Text.RegularExpressions;

namespace AozoraEpub3.Core.Converter;

/// <summary>外字注記・前方参照注記変換ロジック</summary>
internal sealed class GaijiChukiService
{
    private readonly ConverterSettings _settings;
    private readonly ConverterState _state;

    public GaijiChukiService(ConverterSettings settings, ConverterState state)
    {
        _settings = settings;
        _state = state;
    }

    /// <summary>
    /// 外字注記を UTF-8 文字または代替文字列に変換する。
    /// <para>Java: convertGaijiChuki(line, escape, logged)</para>
    /// </summary>
    internal string ConvertGaijiChuki(string line, bool escape, bool logged = true)
    {
        Match match = AozoraEpub3Converter.GaijiChukiPattern.Match(line);
        if (!match.Success) return line;

        var buf = new StringBuilder();
        int begin = 0;

        do
        {
            string chuki = match.Value;
            int chukiStart = match.Index;

            buf.Append(line, begin, chukiStart - begin);

            if (chuki[0] == '※')
            {
                // 外字注記: ※［＃「...」, ...］
                string chukiInner = chuki[3..^1];
                string? gaiji = null;

                // U+のコードのみの注記
                if (chukiInner.StartsWith("U+", StringComparison.OrdinalIgnoreCase))
                {
                    gaiji = AozoraEpub3Converter._gaijiConverter.CodeToCharString(chukiInner);
                    if (gaiji != null)
                    {
                        buf.Append(gaiji);
                        begin = chukiStart + chuki.Length;
                        match = match.NextMatch();
                        continue;
                    }
                }

                // 、の後ろにコードがある場合
                string[] chukiValues = chukiInner.Split('、');

                // 注記文字グリフ or 代替文字変換
                gaiji = AozoraEpub3Converter._gaijiConverter.ToAlterString(chukiValues[0]);

                // 注記内なら注記タグは除外する
                if (gaiji != null && HasInnerChuki(line, match.Index))
                    gaiji = AozoraEpub3Converter.ChukiPattern.Replace(gaiji, "");

                // コード変換 (chukiValues[3])
                if (gaiji == null && chukiValues.Length > 3)
                    gaiji = AozoraEpub3Converter._gaijiConverter.CodeToCharString(chukiValues[3]);
                // コード変換 (chukiValues[2])
                if (gaiji == null && chukiValues.Length > 2)
                    gaiji = AozoraEpub3Converter._gaijiConverter.CodeToCharString(chukiValues[2]);
                // コード変換 (chukiValues[1])
                if (gaiji == null && chukiValues.Length > 1)
                    gaiji = AozoraEpub3Converter._gaijiConverter.CodeToCharString(chukiValues[1]);
                // 注記名称で変換
                if (gaiji == null)
                    gaiji = AozoraEpub3Converter._gaijiConverter.ToUtf(chukiValues[0]);

                if (gaiji != null)
                {
                    // 特殊文字は前に ※ を付けて文字出力時に例外処理
                    if (gaiji.Length == 1 && escape)
                    {
                        switch (gaiji[0])
                        {
                            case '※': buf.Append('※'); break;
                            case '》': buf.Append('※'); break;
                            case '《': buf.Append('※'); break;
                            case '｜': buf.Append('※'); break;
                            case '＃': buf.Append('※'); break;
                        }
                    }
                    buf.Append(gaiji);
                    begin = chukiStart + chuki.Length;
                    match = match.NextMatch();
                    continue;
                }

                // 変換不可
                if (HasInnerChuki(line, match.Index))
                {
                    gaiji = "〓";
                    LogAppender.Warn(_state.LineNum, "外字注記内に注記があります", chuki);
                }
                else
                {
                    // 画像指定付き外字なら画像注記に変更
                    int imageStartIdx = chuki.IndexOf('（', 2);
                    if (imageStartIdx > -1 && chuki.IndexOf('.', 2) != -1)
                    {
                        // ※を消して内部処理用画像注記に変更: ［＃（ファイル名）#GAIJI#］
                        gaiji = chuki[1..^1] + "#GAIJI#］";
                    }
                    else
                    {
                        if (logged) LogAppender.Warn(_state.LineNum, "外字未変換", chuki);
                        gaiji = "〓［＃行右小書き］（" + chukiValues[0] + "）［＃行右小書き終わり］";
                    }
                }
                buf.Append(gaiji);
            }
            else if (chuki[0] == '〔')
            {
                // 拡張ラテン文字変換: 〔e'tiquette〕
                string inner = chuki[1..^1];
                // 〔の次が半角でなければ〔の中を再度外字変換
                if (!CharUtils.IsHalfSpace(inner.ToCharArray()))
                    buf.Append('〔').Append(ConvertGaijiChuki(inner, true)).Append('〕');
                else
                    buf.Append(AozoraEpub3Converter._latinConverter.ToLatinString(inner));
            }
            else if (chuki[0] == '／')
            {
                // くの字点: ／＼ → 〳〵、 ／″＼ → 〴〵
                buf.Append(chuki[1] == '″' ? '〴' : '〳');
                buf.Append('〵');
            }

            begin = chukiStart + chuki.Length;
            match = match.NextMatch();
        }
        while (match.Success);

        buf.Append(line, begin, line.Length - begin);
        return buf.ToString();
    }

    /// <summary>
    /// gaijiStart の位置より前で注記 ［＃ が閉じられていないかをチェック。
    /// 閉じていなければ true（= 外字が注記内にある）。
    /// </summary>
    internal bool HasInnerChuki(string line, int gaijiStart)
    {
        int chukiStartCount = 0;
        int end = gaijiStart;
        while (end > 0)
        {
            end = line.LastIndexOf("［＃", end - 1);
            if (end == -1) break;
            chukiStartCount++;
        }

        int chukiEndCount = 0;
        end = gaijiStart;
        while (end > 0)
        {
            end = line.LastIndexOf('］', end - 1);
            if (end == -1) break;
            chukiEndCount++;
        }

        return chukiStartCount > chukiEndCount;
    }

    /// <summary>
    /// 前方参照注記（「○○」の××）をインライン注記（前後タグ）に変換する。
    /// <para>Java: replaceChukiSufTag(line)</para>
    /// </summary>
    internal string ReplaceChukiSufTag(string line)
    {
        if (line.IndexOf("［＃「") == -1) return line;

        // --- 第1パス: 注記内注記を除外 ---
        var buf = new StringBuilder();
        int mTagEnd = 0;
        int innerTagLevel = 0;
        int innerTagStart = 0;

        foreach (Match mTag in AozoraEpub3Converter.InnerTagPattern.Matches(line))
        {
            if (innerTagLevel <= 1) buf.Append(line, mTagEnd, mTag.Index - mTagEnd);
            mTagEnd = mTag.Index + mTag.Length;
            string tag = mTag.Value;

            if (tag == "］")
            {
                if (innerTagLevel <= 1) buf.Append(tag);
                else if (innerTagLevel == 2)
                    LogAppender.Warn(_state.LineNum, "注記内に注記があります", line[innerTagStart..mTagEnd]);
                innerTagLevel--;
            }
            else
            {
                innerTagLevel++;
                if (innerTagLevel <= 1) buf.Append(tag);
                else if (innerTagLevel == 2) innerTagStart = mTag.Index;
            }
        }
        buf.Append(line, mTagEnd, line.Length - mTagEnd);
        line = buf.ToString();

        // --- 第2パス: ［＃「target」chuki］ → 前後タグ ---
        Match m = AozoraEpub3Converter.ChukiSufPattern.Match(line);
        if (!m.Success) return line;

        int chOffset = 0;
        buf = new StringBuilder(line);
        do
        {
            string target = m.Groups[1].Value;
            string chuki = m.Groups[2].Value;
            string[]? tags = AozoraEpub3Converter._sufChukiMap.TryGetValue(chuki, out var t) ? t : null;
            int chukiTagStart = m.Index;
            int chukiTagEnd = m.Index + m.Length;

            // 後ろにルビがあったら前に移動して位置を調整
            int bufChukiEnd = chukiTagEnd + chOffset;
            if (chukiTagEnd < line.Length && bufChukiEnd < buf.Length && buf[bufChukiEnd] == '《')
            {
                int rubyEnd = buf.ToString().IndexOf('》', bufChukiEnd + 2);
                if (rubyEnd != -1)
                {
                    string ruby = buf.ToString(bufChukiEnd, rubyEnd + 1 - bufChukiEnd);
                    buf.Remove(bufChukiEnd, ruby.Length);
                    buf.Insert(chukiTagStart + chOffset, ruby);
                    chukiTagStart += ruby.Length;
                    chukiTagEnd += ruby.Length;
                    LogAppender.Warn(_state.LineNum, "ルビが注記の後ろにあります", ruby);
                }
            }

            if (chuki.EndsWith("の注記付き終わり"))
            {
                // ［＃注記付き］○○［＃「××」の注記付き終わり］ の例外処理
                buf.Remove(chukiTagStart + chOffset, chukiTagEnd - chukiTagStart);
                buf.Insert(chukiTagStart + chOffset, "《" + target + "》");
                // 前にある ［＃注記付き］ を ｜ に置換
                int start = buf.ToString().LastIndexOf("［＃注記付き］", chukiTagStart + chOffset);
                if (start != -1)
                {
                    buf.Remove(start + 1, 6); // ［＃注記付き］(7chars) → ｜(1char): delete [+1..+7)
                    buf[start] = '｜';
                    chOffset -= 6;
                }
                chOffset += target.Length + 2 - (chukiTagEnd - chukiTagStart);
            }
            else if (tags != null)
            {
                // 前後タグに展開
                int targetStart = GetTargetStart(buf, chukiTagStart, chOffset, CharUtils.RemoveRuby(target).Length);
                buf.Remove(chukiTagStart + chOffset, chukiTagEnd - chukiTagStart);
                buf.Insert(chukiTagStart + chOffset, "［＃" + tags[1] + "］");
                buf.Insert(targetStart, "［＃" + tags[0] + "］");
                chOffset += tags[0].Length + tags[1].Length + 6 - (chukiTagEnd - chukiTagStart);
            }

            m = m.NextMatch();
        }
        while (m.Success);

        // --- 第3パス: ［＃「target」に「rt」のルビ/注記］ ---
        line = buf.ToString();
        m = AozoraEpub3Converter.ChukiSufPattern2.Match(line);
        if (!m.Success) return line;

        chOffset = 0;
        do
        {
            string target = m.Groups[1].Value;
            string chuki = m.Groups[2].Value;
            string[]? tags = AozoraEpub3Converter._sufChukiMap.TryGetValue(chuki, out var t2) ? t2 : null;
            int targetLength = target.Length;
            int chukiTagStart = m.Index;
            int chukiTagEnd = m.Index + m.Length;

            if (tags == null)
            {
                if (chuki.EndsWith("のルビ") || (_settings.ChukiRuby && chuki.EndsWith("の注記")))
                {
                    // ルビに変換（ママは除外）
                    if (chuki.StartsWith("に「") && !chuki.StartsWith("に「ママ"))
                    {
                        // ［＃「青空文庫」に「あおぞらぶんこ」のルビ］
                        int targetStart = GetTargetStart(buf, chukiTagStart, chOffset, targetLength);
                        buf.Remove(chukiTagStart + chOffset, chukiTagEnd - chukiTagStart);
                        string rt = chuki[(chuki.IndexOf('「') + 1)..chuki.IndexOf('」')];
                        buf.Insert(chukiTagStart + chOffset, "《" + rt + "》");
                        buf.Insert(targetStart, "｜");
                        chOffset += rt.Length + 3 - (chukiTagEnd - chukiTagStart);
                    }
                }
                else if (_settings.ChukiKogaki && chuki.EndsWith("の注記"))
                {
                    // 後ろに小書き表示（ママは除外）
                    if (chuki.StartsWith("に「") && !chuki.StartsWith("に「ママ"))
                    {
                        // ［＃「青空文庫」に「あおぞらぶんこ」の注記］
                        buf.Remove(chukiTagStart + chOffset, chukiTagEnd - chukiTagStart);
                        string kogaki = "［＃小書き］" +
                            chuki[(chuki.IndexOf('「') + 1)..chuki.IndexOf('」')] +
                            "［＃小書き終わり］";
                        buf.Insert(chukiTagStart + chOffset, kogaki);
                        chOffset += kogaki.Length - (chukiTagEnd - chukiTagStart);
                    }
                }
            }

            m = m.NextMatch();
        }
        while (m.Success);

        return buf.ToString();
    }

    /// <summary>
    /// 前方参照注記の前タグ挿入位置を取得する。
    /// ルビ・注記タグをスキップしながら targetLength 文字分を後ろから数え、挿入インデックスを返す。
    /// <para>Java: getTargetStart(buf, chukiTagStart, chOffset, targetLength)</para>
    /// </summary>
    internal int GetTargetStart(StringBuilder buf, int chukiTagStart, int chOffset, int targetLength)
    {
        int idx = chukiTagStart - 1 + chOffset;
        bool hasRuby = false;
        int length = 0;

        while (targetLength > length && idx >= 0)
        {
            switch (buf[idx])
            {
                case '》':
                    idx--;
                    // エスケープ文字なら1文字としてカウント
                    if (idx >= 0 && CharUtils.IsEscapedChar(buf, idx))
                    {
                        length++;
                        break;
                    }
                    // ルビの《...》をスキップ
                    while (idx >= 0 && buf[idx] != '《' && !CharUtils.IsEscapedChar(buf, idx))
                        idx--;
                    hasRuby = true;
                    break;

                case '］':
                    idx--;
                    // エスケープ文字なら1文字としてカウント
                    if (idx >= 0 && CharUtils.IsEscapedChar(buf, idx))
                    {
                        length++;
                        break;
                    }
                    // 注記タグの ［...］ をスキップ
                    while (idx >= 0 && buf[idx] != '［' && !CharUtils.IsEscapedChar(buf, idx))
                        idx--;
                    break;

                case '｜':
                    // エスケープされた ｜ のみカウント（ルビ区切りの ｜ はスキップ）
                    if (CharUtils.IsEscapedChar(buf, idx))
                        length++;
                    break;

                default:
                    length++;
                    break;
            }
            idx--;
        }

        // ルビがあれば先頭の ｜ を含める
        if (hasRuby && idx >= 0 && buf[idx] == '｜') return idx;
        return idx + 1;
    }
}
