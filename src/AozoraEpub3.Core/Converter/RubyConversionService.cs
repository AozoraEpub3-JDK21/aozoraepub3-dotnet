using System.Text;

namespace AozoraEpub3.Core.Converter;

internal enum RubyCharType { Null, Alpha, FullAlpha, Kanji, Hiragana, Katakana }

/// <summary>ルビ変換ロジック</summary>
internal sealed class RubyConversionService
{
    private readonly ConverterState _state;
    private readonly TcyConversionService _tcyService;
    private readonly CharacterConversionService _charService;

    public RubyConversionService(ConverterState state,
        TcyConversionService tcyService, CharacterConversionService charService)
    {
        _state = state;
        _tcyService = tcyService;
        _charService = charService;
    }

    /// <summary>ルビタグに変換して出力。Java: convertRubyText</summary>
    internal StringBuilder ConvertRubyText(string line)
    {
        var buf = new StringBuilder();
        char[] ch = line.ToCharArray();
        int begin = 0, end = ch.Length;

        int rubyStart = -1;
        int rubyTopStart = -1;
        bool inRuby = false;
        RubyCharType rubyCharType = RubyCharType.Null;

        string rubyStartChuki = AozoraEpub3Converter._chukiMap.TryGetValue("ルビ開始", out var rsv) ? rsv[0] : "<ruby>";
        string rubyEndChuki = AozoraEpub3Converter._chukiMap.TryGetValue("ルビ終了", out var rev) ? rev[0] : "</ruby>";

        bool noTcy = false;
        for (int i = begin; i < end; i++)
        {
            if (!noTcy && _state.NoTcyStart.Contains(i)) noTcy = true;
            else if (noTcy && _state.NoTcyEnd.Contains(i)) noTcy = false;

            switch (ch[i])
            {
                case '｜':
                    if (!CharUtils.IsEscapedChar(ch, i))
                    {
                        if (rubyStart != -1) _tcyService.ConvertTcyText(buf, ch, rubyStart, i, noTcy);
                        rubyStart = i + 1;
                        inRuby = true;
                    }
                    break;
                case '《':
                    if (!CharUtils.IsEscapedChar(ch, i))
                    {
                        inRuby = true;
                        rubyTopStart = i;
                    }
                    break;
            }

            if (inRuby)
            {
                if (ch[i] == '》' && !CharUtils.IsEscapedChar(ch, i))
                {
                    if (rubyStart != -1 && rubyTopStart != -1)
                    {
                        // 同じ長さで同じ文字なら1文字ずつルビ
                        if (rubyTopStart - rubyStart == i - rubyTopStart - 1 &&
                            CharUtils.IsSameChars(ch, rubyTopStart + 1, i))
                        {
                            if (!rubyEndChuki.Equals(buf.Length >= rubyEndChuki.Length
                                ? buf.ToString(buf.Length - rubyEndChuki.Length, rubyEndChuki.Length) : ""))
                                buf.Append(rubyStartChuki);
                            else
                                buf.Length -= rubyEndChuki.Length;
                            for (int j = 0; j < rubyTopStart - rubyStart; j++)
                            {
                                _charService.ConvertReplacedChar(buf, ch, rubyStart + j, noTcy);
                                buf.Append(AozoraEpub3Converter._chukiMap.TryGetValue("ルビ前", out var rbf) ? rbf[0] : "<rt>");
                                _charService.ConvertReplacedChar(buf, ch, rubyTopStart + 1 + j, true);
                                buf.Append(AozoraEpub3Converter._chukiMap.TryGetValue("ルビ後", out var rba) ? rba[0] : "</rt>");
                            }
                            buf.Append(rubyEndChuki);
                        }
                        else
                        {
                            if (!rubyEndChuki.Equals(buf.Length >= rubyEndChuki.Length
                                ? buf.ToString(buf.Length - rubyEndChuki.Length, rubyEndChuki.Length) : ""))
                                buf.Append(rubyStartChuki);
                            else
                                buf.Length -= rubyEndChuki.Length;
                            _tcyService.ConvertTcyText(buf, ch, rubyStart, rubyTopStart, noTcy);
                            buf.Append(AozoraEpub3Converter._chukiMap.TryGetValue("ルビ前", out var rbf2) ? rbf2[0] : "<rt>");
                            _tcyService.ConvertTcyText(buf, ch, rubyTopStart + 1, i, true);
                            buf.Append(AozoraEpub3Converter._chukiMap.TryGetValue("ルビ後", out var rba2) ? rba2[0] : "</rt>");
                            buf.Append(rubyEndChuki);
                        }
                    }
                    if (rubyStart == -1)
                        LogAppender.Warn(_state.LineNum, "ルビ開始文字無し");
                    inRuby = false;
                    rubyStart = -1;
                    rubyTopStart = -1;
                }
            }
            else
            {
                // ルビ開始位置チェック
                if (rubyStart != -1)
                {
                    bool charTypeChanged = rubyCharType switch
                    {
                        RubyCharType.Alpha => !CharUtils.IsHalfSpace(ch[i]) || ch[i] == '>',
                        RubyCharType.FullAlpha => !(CharUtils.IsFullAlpha(ch[i]) || CharUtils.IsFullNum(ch[i])),
                        RubyCharType.Kanji => !CharUtils.IsKanji(ch, i),
                        RubyCharType.Hiragana => !CharUtils.IsHiragana(ch[i]),
                        RubyCharType.Katakana => !CharUtils.IsKatakana(ch[i]),
                        _ => false
                    };
                    if (charTypeChanged)
                    {
                        _tcyService.ConvertTcyText(buf, ch, rubyStart, i, noTcy);
                        rubyStart = -1; rubyCharType = RubyCharType.Null;
                    }
                }
                if (rubyStart == -1)
                {
                    if (CharUtils.IsKanji(ch, i)) { rubyStart = i; rubyCharType = RubyCharType.Kanji; }
                    else if (CharUtils.IsHiragana(ch[i])) { rubyStart = i; rubyCharType = RubyCharType.Hiragana; }
                    else if (CharUtils.IsKatakana(ch[i])) { rubyStart = i; rubyCharType = RubyCharType.Katakana; }
                    else if (CharUtils.IsHalfSpace(ch[i]) && ch[i] != '>') { rubyStart = i; rubyCharType = RubyCharType.Alpha; }
                    else if (CharUtils.IsFullAlpha(ch[i]) || CharUtils.IsFullNum(ch[i])) { rubyStart = i; rubyCharType = RubyCharType.FullAlpha; }
                    else { _charService.ConvertReplacedChar(buf, ch, i, noTcy); rubyCharType = RubyCharType.Null; }
                }
            }
        }
        if (rubyStart != -1)
            _tcyService.ConvertTcyText(buf, ch, rubyStart, end, noTcy);

        return buf;
    }
}
