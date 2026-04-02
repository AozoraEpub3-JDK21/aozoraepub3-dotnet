using System.Text;
using AozoraEpub3.Core.Io;

namespace AozoraEpub3.Core.Converter;

/// <summary>縦中横変換ロジック</summary>
internal sealed class TcyConversionService
{
    private readonly ConverterSettings _settings;
    private readonly ConverterState _state;
    private readonly CharacterConversionService _charService;
    private readonly IEpub3Writer _writer;

    public TcyConversionService(ConverterSettings settings, ConverterState state,
        CharacterConversionService charService, IEpub3Writer writer)
    {
        _settings = settings;
        _state = state;
        _charService = charService;
        _writer = writer;
    }

    /// <summary>ルビ変換 外部呼び出し用。Java: convertTcyText(String)</summary>
    public string ConvertTcyText(string text)
    {
        var buf = new StringBuilder();
        ConvertTcyText(buf, text.ToCharArray(), 0, text.Length, false);
        return buf.ToString();
    }

    /// <summary>1文字外字フォントタグを出力。Java: printGlyphFontTag</summary>
    private bool PrintGlyphFontTag(StringBuilder buf, string gaijiFileName, string className, char baseChar)
    {
        string fullPath = Path.Combine(_writer.GetGaijiFontPath(), gaijiFileName);
        if (!File.Exists(fullPath)) return false;
        _writer.AddGaijiFont(className, fullPath);
        buf.Append("<span class=\"glyph ").Append(className).Append("\">").Append(baseChar).Append("</span>");
        return true;
    }

    /// <summary>縦中横変換して buf に出力。Java: convertTcyText(buf,ch,begin,end,noTcy)</summary>
    internal void ConvertTcyText(StringBuilder buf, char[] ch, int begin, int end, bool noTcy)
    {
        // ConvertRubyText のセグメント分割では noTcy 境界をまたぐ場合がある。
        // begin 位置での正確な noTcy 状態を復元する。
        if (_state.NoTcyStart.Count > 0 || _state.NoTcyEnd.Count > 0)
        {
            bool localNoTcy = false;
            for (int pos = 0; pos < begin; pos++)
            {
                if (!localNoTcy && _state.NoTcyStart.Contains(pos)) localNoTcy = true;
                else if (localNoTcy && _state.NoTcyEnd.Contains(pos)) localNoTcy = false;
            }
            noTcy = localNoTcy;
        }

        for (int i = begin; i < end; i++)
        {
            // セグメント内の noTcy 境界を文字単位でチェック
            if (!noTcy && _state.NoTcyStart.Contains(i)) noTcy = true;
            else if (noTcy && _state.NoTcyEnd.Contains(i)) noTcy = false;

            string? gaijiFileName = null;

            // 4バイト文字（サロゲートペア）
            if (i < end - 1 && char.IsHighSurrogate(ch[i]))
            {
                int code = char.ConvertToUtf32(ch[i], ch[i + 1]);

                // 4バイト文字 + IVS(U+E0100～)
                if (i < end - 3 && ch[i + 2] == 0xDB40)
                {
                    string ivsCode = char.ConvertToUtf32(ch[i + 2], ch[i + 3]).ToString("x");
                    if (AozoraEpub3Converter._ivs32FontMap != null)
                    {
                        string className = "u" + code.ToString("x") + "-u" + ivsCode;
                        gaijiFileName = AozoraEpub3Converter._ivs32FontMap.GetValueOrDefault(className);
                        if (gaijiFileName != null && PrintGlyphFontTag(buf, gaijiFileName, className, '〓'))
                        {
                            LogAppender.Warn(_state.LineNum, "外字フォント利用(IVS含む)", "" + ch[i] + ch[i + 1] + ch[i + 2] + ch[i + 3] + "(" + gaijiFileName + ")");
                            i += 3; continue;
                        }
                    }
                    if (AozoraEpub3Converter._utf32FontMap != null)
                    {
                        gaijiFileName = AozoraEpub3Converter._utf32FontMap.GetValueOrDefault(code);
                        if (gaijiFileName != null && PrintGlyphFontTag(buf, gaijiFileName, "u" + code.ToString("x"), '〓'))
                        {
                            LogAppender.Warn(_state.LineNum, "外字フォント利用(IVS除外)", "" + ch[i] + ch[i + 1] + "(" + gaijiFileName + ") -" + ivsCode);
                            i += 3; continue;
                        }
                    }
                    if (_settings.PrintIvsSSP)
                    {
                        if (_settings.Vertical) buf.Append(AozoraEpub3Converter._chukiMap["正立"][0]);
                        buf.Append(ch[i]); buf.Append(ch[i + 1]); buf.Append(ch[i + 2]); buf.Append(ch[i + 3]);
                        if (_settings.Vertical) buf.Append(AozoraEpub3Converter._chukiMap["正立終わり"][0]);
                        LogAppender.Warn(_state.LineNum, "拡張漢字＋IVSを出力します", "" + ch[i] + ch[i + 1] + ch[i + 2] + ch[i + 3] + "(u+" + code.ToString("x") + "+" + ivsCode + ")");
                    }
                    else
                    {
                        buf.Append(ch[i]); buf.Append(ch[i + 1]);
                        LogAppender.Warn(_state.LineNum, "拡張漢字出力(IVS除外)", "" + ch[i] + ch[i + 1] + "(u+" + code.ToString("x") + ") -" + ivsCode);
                    }
                    i += 3; continue;
                }

                // 4バイト文字 + IVS(U+FE00～)
                if (i < end - 2 && ch[i + 2] >= 0xFE00 && ch[i + 2] <= 0xFE0F)
                {
                    if (AozoraEpub3Converter._ivs32FontMap != null)
                    {
                        string className = "u" + code.ToString("x") + "-u" + ((int)ch[i + 2]).ToString("x");
                        gaijiFileName = AozoraEpub3Converter._ivs32FontMap.GetValueOrDefault(className);
                        if (gaijiFileName != null && PrintGlyphFontTag(buf, gaijiFileName, className, '〓'))
                        {
                            LogAppender.Warn(_state.LineNum, "外字フォント利用(IVS含む)", "" + ch[i] + ch[i + 1] + ch[i + 2] + "(" + gaijiFileName + ")");
                            i += 2; continue;
                        }
                    }
                    if (_settings.PrintIvsBMP)
                    {
                        if (_settings.Vertical) buf.Append(AozoraEpub3Converter._chukiMap["正立"][0]);
                        buf.Append(ch[i]); buf.Append(ch[i + 1]); buf.Append(ch[i + 2]);
                        if (_settings.Vertical) buf.Append(AozoraEpub3Converter._chukiMap["正立終わり"][0]);
                        LogAppender.Warn(_state.LineNum, "拡張漢字＋IVSを出力します", "" + ch[i] + ch[i + 1] + ch[i + 2] + "(u+" + code.ToString("x") + "+" + ((int)ch[i + 2]).ToString("x") + ")");
                    }
                    else
                    {
                        buf.Append(ch[i]); buf.Append(ch[i + 1]);
                        LogAppender.Warn(_state.LineNum, "拡張漢字出力(IVS除外)", "" + ch[i] + ch[i + 1] + "(u+" + code.ToString("x") + ") -" + ((int)ch[i + 2]).ToString("x") + ")");
                    }
                    i += 2; continue;
                }

                // IVS無し1文字フォントあり
                if (AozoraEpub3Converter._utf32FontMap != null)
                {
                    gaijiFileName = AozoraEpub3Converter._utf32FontMap.GetValueOrDefault(code);
                    if (gaijiFileName != null && PrintGlyphFontTag(buf, gaijiFileName, "u" + code.ToString("x"), '〓'))
                    {
                        LogAppender.Warn(_state.LineNum, "外字フォント利用", "" + ch[i] + ch[i + 1] + "(" + gaijiFileName + ")");
                        i++; continue;
                    }
                }

                // 通常4バイト文字
                buf.Append(ch[i]); buf.Append(ch[i + 1]);
                LogAppender.Warn(_state.LineNum, "拡張漢字出力", "" + ch[i] + ch[i + 1] + "(u+" + code.ToString("x") + ")");
                i++; continue;
            }

            // 2バイト文字(U+FFFF以下)

            // 2バイト文字 + IVS(U+E0100～)
            if (i < end - 2 && ch[i + 1] == 0xDB40)
            {
                string ivsCode = char.ConvertToUtf32(ch[i + 1], ch[i + 2]).ToString("x");
                if (AozoraEpub3Converter._ivs16FontMap != null)
                {
                    string className = "u" + ((int)ch[i]).ToString("x") + "-u" + ivsCode;
                    gaijiFileName = AozoraEpub3Converter._ivs16FontMap.GetValueOrDefault(className);
                    if (gaijiFileName != null && PrintGlyphFontTag(buf, gaijiFileName, className, '〓'))
                    {
                        LogAppender.Warn(_state.LineNum, "外字フォント利用(IVS含む)", "" + ch[i] + ch[i + 1] + ch[i + 2] + "(" + gaijiFileName + ")");
                        i += 2; continue;
                    }
                }
                if (AozoraEpub3Converter._utf16FontMap != null && AozoraEpub3Converter._utf16FontMap.ContainsKey((int)ch[i]))
                {
                    gaijiFileName = AozoraEpub3Converter._utf16FontMap[(int)ch[i]];
                    if (gaijiFileName != null && PrintGlyphFontTag(buf, gaijiFileName, "u" + ((int)ch[i]).ToString("x"), '〓'))
                    {
                        LogAppender.Warn(_state.LineNum, "外字フォント利用(IVS除外)", "" + ch[i] + "(" + gaijiFileName + ") -" + ivsCode);
                        i += 2; continue;
                    }
                }
                if (_settings.PrintIvsSSP)
                {
                    if (_settings.Vertical) buf.Append(AozoraEpub3Converter._chukiMap["正立"][0]);
                    buf.Append(ch[i]); buf.Append(ch[i + 1]); buf.Append(ch[i + 2]);
                    if (_settings.Vertical) buf.Append(AozoraEpub3Converter._chukiMap["正立終わり"][0]);
                    LogAppender.Warn(_state.LineNum, "IVSを出力します", "" + ch[i] + ch[i + 1] + ch[i + 2] + "(u+" + ((int)ch[i]).ToString("x") + "+" + ivsCode + ")");
                }
                else
                {
                    buf.Append(ch[i]);
                    LogAppender.Warn(_state.LineNum, "IVS除外", ch[i] + "(u+" + ((int)ch[i]).ToString("x") + ") -" + ivsCode);
                }
                i += 2; continue;
            }

            // 2バイト文字 + IVS(U+FE00～)
            if (i < end - 1 && ch[i + 1] >= 0xFE00 && ch[i + 1] <= 0xFE0F)
            {
                if (AozoraEpub3Converter._ivs32FontMap != null)
                {
                    string className = "u" + ((int)ch[i]).ToString("x") + "-u" + ((int)ch[i + 1]).ToString("x");
                    gaijiFileName = AozoraEpub3Converter._ivs32FontMap.GetValueOrDefault(className);
                    if (gaijiFileName != null && PrintGlyphFontTag(buf, gaijiFileName, className, '〓'))
                    {
                        LogAppender.Warn(_state.LineNum, "外字フォント利用(IVS含む)", "" + ch[i] + "(" + gaijiFileName + ")");
                        i++; continue;
                    }
                }
                if (AozoraEpub3Converter._utf16FontMap != null && AozoraEpub3Converter._utf16FontMap.ContainsKey((int)ch[i]))
                {
                    gaijiFileName = AozoraEpub3Converter._utf16FontMap[(int)ch[i]];
                    if (gaijiFileName != null && PrintGlyphFontTag(buf, gaijiFileName, "u" + ((int)ch[i]).ToString("x"), '〓'))
                    {
                        LogAppender.Warn(_state.LineNum, "外字フォント利用(IVS除外)", "" + ch[i] + "(" + gaijiFileName + ") -" + ((int)ch[i + 1]).ToString("x"));
                        i++; continue;
                    }
                }
                if (_settings.PrintIvsBMP)
                {
                    buf.Append(ch[i]); buf.Append(ch[i + 1]);
                    LogAppender.Warn(_state.LineNum, "IVSを出力します", "" + ch[i] + ch[i + 1] + "(u+" + ((int)ch[i]).ToString("x") + "+" + ((int)ch[i + 1]).ToString("x") + ")");
                }
                else
                {
                    buf.Append(ch[i]);
                    LogAppender.Warn(_state.LineNum, "IVS除外", ch[i] + "(u+" + ((int)ch[i]).ToString("x") + ") -" + ((int)ch[i + 1]).ToString("x"));
                }
                i++; continue;
            }

            // IVS無し1文字フォント
            if (AozoraEpub3Converter._utf16FontMap != null && AozoraEpub3Converter._utf16FontMap.ContainsKey((int)ch[i]))
            {
                gaijiFileName = AozoraEpub3Converter._utf16FontMap[(int)ch[i]];
                if (gaijiFileName != null && PrintGlyphFontTag(buf, gaijiFileName, "u" + ((int)ch[i]).ToString("x"), '〓'))
                {
                    LogAppender.Warn(_state.LineNum, "外字フォント利用", "" + ch[i] + "(" + gaijiFileName + ")");
                    continue;
                }
            }

            // 自動縦中横
            if (_settings.Vertical && !(_state.InYoko || noTcy))
            {
                switch (ch[i])
                {
                    case '0': case '1': case '2': case '3': case '4':
                    case '5': case '6': case '7': case '8': case '9':
                        if (_settings.AutoYoko)
                        {
                            if (_settings.AutoYokoNum3 && i + 2 < end && CharUtils.IsNum(ch[i + 1]) && CharUtils.IsNum(ch[i + 2]))
                            {
                                if (!CheckTcyPrev(ch, i - 1)) break;
                                if (!CheckTcyNext(ch, i + 3)) break;
                                buf.Append(AozoraEpub3Converter._chukiMap["縦中横"][0]); buf.Append(ch[i]); buf.Append(ch[i + 1]); buf.Append(ch[i + 2]);
                                buf.Append(AozoraEpub3Converter._chukiMap["縦中横終わり"][0]); i += 2; continue;
                            }
                            else if (i + 1 < end && CharUtils.IsNum(ch[i + 1]))
                            {
                                if (!CheckTcyPrev(ch, i - 1)) break;
                                if (!CheckTcyNext(ch, i + 2)) break;
                                buf.Append(AozoraEpub3Converter._chukiMap["縦中横"][0]); buf.Append(ch[i]); buf.Append(ch[i + 1]);
                                buf.Append(AozoraEpub3Converter._chukiMap["縦中横終わり"][0]); i++; continue;
                            }
                            else if (_settings.AutoYokoNum1 && (i == 0 || !CharUtils.IsNum(ch[i - 1])) && (i + 1 == end || !CharUtils.IsNum(ch[i + 1])))
                            {
                                if (!CheckTcyPrev(ch, i - 1)) break;
                                if (!CheckTcyNext(ch, i + 1)) break;
                                buf.Append(AozoraEpub3Converter._chukiMap["縦中横"][0]); buf.Append(ch[i]); buf.Append(AozoraEpub3Converter._chukiMap["縦中横終わり"][0]); continue;
                            }
                            // 1月1日 パターン
                            if (i + 3 < ch.Length && ch[i + 1] == '月' && '0' <= ch[i + 2] && ch[i + 2] <= '9' &&
                                (ch[i + 3] == '日' || (i + 4 < ch.Length && '0' <= ch[i + 3] && ch[i + 3] <= '9' && ch[i + 4] == '日')))
                            {
                                buf.Append(AozoraEpub3Converter._chukiMap["縦中横"][0]); buf.Append(ch[i]); buf.Append(AozoraEpub3Converter._chukiMap["縦中横終わり"][0]); continue;
                            }
                            if (i > 1 && i + 1 < ch.Length &&
                                (ch[i - 1] == '年' && ch[i + 1] == '月' || ch[i - 1] == '月' && ch[i + 1] == '日' ||
                                 ch[i - 1] == '第' && (ch[i + 1] == '刷' || ch[i + 1] == '版' || ch[i + 1] == '巻')))
                            {
                                buf.Append(AozoraEpub3Converter._chukiMap["縦中横"][0]); buf.Append(ch[i]); buf.Append(AozoraEpub3Converter._chukiMap["縦中横終わり"][0]); continue;
                            }
                            if (i > 2 && (ch[i - 2] == '明' && ch[i - 1] == '治' || ch[i - 2] == '大' && ch[i - 1] == '正' ||
                                          ch[i - 2] == '昭' && ch[i - 1] == '和' || ch[i - 2] == '平' && ch[i - 1] == '成'))
                            {
                                buf.Append(AozoraEpub3Converter._chukiMap["縦中横"][0]); buf.Append(ch[i]); buf.Append(AozoraEpub3Converter._chukiMap["縦中横終わり"][0]); continue;
                            }
                        }
                        break;

                    case '!': case '?':
                        if (_settings.AutoYoko)
                        {
                            if (_settings.AutoYokoEQ3 && i + 2 < end && (ch[i + 1] == '!' || ch[i + 1] == '?') && (ch[i + 2] == '!' || ch[i + 2] == '?'))
                            {
                                if (!CheckTcyPrev(ch, i - 1)) break;
                                if (!CheckTcyNext(ch, i + 3)) break;
                                buf.Append(AozoraEpub3Converter._chukiMap["縦中横"][0]); buf.Append(ch[i]); buf.Append(ch[i + 1]); buf.Append(ch[i + 2]);
                                buf.Append(AozoraEpub3Converter._chukiMap["縦中横終わり"][0]); i += 2; continue;
                            }
                            else if (i + 1 < end && (ch[i + 1] == '!' || ch[i + 1] == '?'))
                            {
                                if (!CheckTcyPrev(ch, i - 1)) break;
                                if (!CheckTcyNext(ch, i + 2)) break;
                                buf.Append(AozoraEpub3Converter._chukiMap["縦中横"][0]); buf.Append(ch[i]); buf.Append(ch[i + 1]);
                                buf.Append(AozoraEpub3Converter._chukiMap["縦中横終わり"][0]); i++; continue;
                            }
                            else if (_settings.AutoYokoEQ1 && (i == 0 || !CharUtils.IsNum(ch[i - 1])) && (i + 1 == end || !CharUtils.IsNum(ch[i + 1])))
                            {
                                if (!CheckTcyPrev(ch, i - 1)) break;
                                if (!CheckTcyNext(ch, i + 1)) break;
                                buf.Append(AozoraEpub3Converter._chukiMap["縦中横"][0]); buf.Append(ch[i]); buf.Append(AozoraEpub3Converter._chukiMap["縦中横終わり"][0]); continue;
                            }
                        }
                        break;
                }
            }

            // ひらがな/カタカナ + 濁点/半濁点（縦書き時）
            if (_settings.Vertical && i + 1 < end && (ch[i + 1] == '゛' || ch[i + 1] == '゜'))
            {
                if (CharUtils.IsHiragana(ch[i]) || CharUtils.IsKatakana(ch[i]) || ch[i] == '〻')
                {
                    if (ch[i + 1] == '゛')
                    {
                        if ('ッ' != ch[i] && (('か' <= ch[i] && ch[i] <= 'と') || ('カ' <= ch[i] && ch[i] <= 'ト')))
                        { ch[i] = (char)(ch[i] + 1); buf.Append(ch[i]); i++; continue; }
                        if (ch[i] == 'ウ') { buf.Append('ヴ'); i++; continue; }
                        if (ch[i] == 'ワ') { buf.Append('ヷ'); i++; continue; }
                        if (ch[i] == 'ヲ') { buf.Append('ヺ'); i++; continue; }
                        if (ch[i] == 'う') { buf.Append('ゔ'); i++; continue; }
                        if (ch[i] == 'ゝ') { buf.Append('ゞ'); i++; continue; }
                        if (ch[i] == 'ヽ') { buf.Append('ヾ'); i++; continue; }
                    }
                    if (('は' <= ch[i] && ch[i] <= 'ほ') || ('ハ' <= ch[i] && ch[i] <= 'ホ'))
                    {
                        buf.Append(ch[i + 1] == '゛' ? (char)(ch[i] + 1) : (char)(ch[i] + 2));
                        i++; continue;
                    }
                    if (_settings.DakutenType == 1 && !(_state.InYoko || noTcy))
                    {
                        buf.Append("<span class=\"dakuten\">").Append(ch[i]).Append("<span>");
                        buf.Append(ch[i + 1] == '゛' ? "゛" : "゜");
                        buf.Append("</span></span>");
                        i++; continue;
                    }
                    else if (_settings.DakutenType == 2)
                    {
                        string cname = "u" + ((int)ch[i]).ToString("x");
                        if (ch[i + 1] == '゛') cname += "-u3099"; else cname += "-u309a";
                        if (PrintGlyphFontTag(buf, "dakuten/" + cname + ".ttf", cname, ch[i]))
                        {
                            LogAppender.Warn(_state.LineNum, "濁点フォント利用", "" + ch[i] + ch[i + 1]);
                            i++; continue;
                        }
                    }
                }
            }

            _charService.ConvertReplacedChar(buf, ch, i, noTcy);
        }
    }

    /// <summary>自動縦中横の前の半角チェック。Java: checkTcyPrev</summary>
    internal bool CheckTcyPrev(char[] ch, int i)
    {
        while (i >= 0)
        {
            if (ch[i] == '>') { do { i--; } while (i >= 0 && ch[i] != '<'); i--; continue; }
            if (ch[i] == ' ') { i--; continue; }
            if (CharUtils.IsHalf(ch[i])) return false;
            return true;
        }
        return true;
    }

    /// <summary>自動縦中横の後ろ半角チェック。Java: checkTcyNext</summary>
    internal bool CheckTcyNext(char[] ch, int i)
    {
        while (i < ch.Length)
        {
            if (ch[i] == '<') { do { i++; } while (i < ch.Length && ch[i] != '>'); i++; continue; }
            if (ch[i] == ' ') { i++; continue; }
            if (CharUtils.IsHalf(ch[i])) return false;
            return true;
        }
        return true;
    }
}
