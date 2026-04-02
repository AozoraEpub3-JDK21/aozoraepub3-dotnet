using System.Text;

namespace AozoraEpub3.Core.Converter;

/// <summary>文字エスケープ・置換ロジック</summary>
internal sealed class CharacterConversionService
{
    private readonly ConverterSettings _settings;
    private readonly ConverterState _state;

    public CharacterConversionService(ConverterSettings settings, ConverterState state)
    {
        _settings = settings;
        _state = state;
    }

    /// <summary>注記で分割された文字列単位でエスケープ処理して buf に出力。
    /// Java: convertEscapedText</summary>
    internal void ConvertEscapedText(StringBuilder buf, char[] ch, int begin, int end)
    {
        if (begin >= end) return;

        // 先頭 IVS 除去
        if (begin < ch.Length)
        {
            if (ch[begin] == 0xDB40) { begin += 2; LogAppender.Warn(_state.LineNum, "先頭にあるIVSを除去します"); }
            else if (ch[begin] >= 0xFE00 && ch[begin] <= 0xFE0F) { begin++; LogAppender.Warn(_state.LineNum, "先頭にあるIVSを除去します"); }
        }
        if (begin >= end) return;

        // << >> ＜＜ ＞＞ → ※《 ※》
        for (int i = begin + 1; i < end; i++)
        {
            switch (ch[i])
            {
                case '<':
                    if (ch[i - 1] == '<' && (i == begin + 1 || ch[i - 2] != '<') && (end - 1 == i || ch[i + 1] != '<'))
                    { ch[i - 1] = '※'; ch[i] = '《'; }
                    break;
                case '>':
                    if (ch[i - 1] == '>' && (i == begin + 1 || ch[i - 2] != '>') && (end - 1 == i || ch[i + 1] != '>'))
                    { ch[i - 1] = '※'; ch[i] = '》'; }
                    break;
                case '＜':
                    if (ch[i - 1] == '＜' && (i == begin + 1 || ch[i - 2] != '＜') && (end - 1 == i || ch[i + 1] != '＜'))
                    { ch[i - 1] = '※'; ch[i] = '《'; }
                    break;
                case '＞':
                    if (ch[i - 1] == '＞' && (i == begin + 1 || ch[i - 2] != '＞') && (end - 1 == i || ch[i + 1] != '＞'))
                    { ch[i - 1] = '※'; ch[i] = '》'; }
                    break;
                case '゙': ch[i] = '゛'; break;
                case '゚': ch[i] = '゜'; break;
            }
        }

        // NULL 除去 + &<> エスケープ
        for (int idx = begin; idx < end; idx++)
        {
            switch (ch[idx])
            {
                case '\0': break;
                case '&': buf.Append("&amp;"); break;
                case '<': buf.Append("&lt;"); break;
                case '>': buf.Append("&gt;"); break;
                default: buf.Append(ch[idx]); break;
            }
        }
    }

    /// <summary>1文字をbufに出力。エスケープ・置換処理あり。Java: convertReplacedChar</summary>
    internal void ConvertReplacedChar(StringBuilder buf, char[] ch, int idx, bool noTcy)
    {
        if (ch[idx] == '\0') return;

        int length = buf.Length;

        // エスケープ文字処理
        bool escaped = false;
        if (idx > 0)
        {
            switch (ch[idx])
            {
                case '》': case '《': case '｜': case '＃': case '※':
                    if (ch[idx - 1] == '※')
                    {
                        buf.Length = length - 1;
                        escaped = true;
                    }
                    break;
            }
        }

        if (AozoraEpub3Converter._replaceMap != null && AozoraEpub3Converter._replaceMap.TryGetValue(ch[idx], out string? replaced1))
        {
            buf.Append(replaced1); return;
        }

        if (idx > 1 || (idx == 1 && !escaped))
        {
            if (AozoraEpub3Converter._replace2Map != null)
            {
                string key = "" + ch[idx - (escaped ? 2 : 1)] + ch[idx];
                if (AozoraEpub3Converter._replace2Map.TryGetValue(key, out string? replaced2))
                {
                    buf.Length = length - 1;
                    buf.Append(replaced2); return;
                }
            }
        }

        if (escaped) { buf.Append(ch[idx]); ch[idx] = '　'; return; }

        // 全角スペース禁則
        if (!(_state.InYoko || noTcy))
        {
            switch (_settings.SpaceHyphenation)
            {
                case 1:
                    if (idx > 20 && ch[idx] == '　' && buf.Length > 0 && buf[buf.Length - 1] != '　' &&
                        (idx - 1 == ch.Length || idx + 1 < ch.Length && ch[idx + 1] != '　'))
                    { buf.Append("<span class=\"fullsp\"> </span>"); return; }
                    break;
                case 2:
                    if (idx > 20 && ch[idx] == '　' && buf.Length > 0 && buf[buf.Length - 1] != '　' &&
                        (idx - 1 == ch.Length || idx + 1 < ch.Length && ch[idx + 1] != '　'))
                    { buf.Append((char)0x2000).Append((char)0x2000); return; }
                    break;
            }
        }

        // 縦書き固有の文字変換
        if (_settings.Vertical && !_state.InYoko)
        {
            switch (ch[idx])
            {
                case '≪': buf.Append("《"); return;
                case '≫': buf.Append("》"); return;
                case '\u201C': buf.Append("〝"); return;   // "
                case '\u201D': buf.Append("〟"); return;   // "
                case '―': buf.Append("─"); return;
                case '÷': case '±': case '∞': case '∴': case '∵':
                case 'Ⅰ': case 'Ⅱ': case 'Ⅲ': case 'Ⅳ': case 'Ⅴ': case 'Ⅵ':
                case 'Ⅶ': case 'Ⅷ': case 'Ⅸ': case 'Ⅹ': case 'Ⅺ': case 'Ⅻ':
                case 'ⅰ': case 'ⅱ': case 'ⅲ': case 'ⅳ': case 'ⅴ': case 'ⅵ':
                case 'ⅶ': case 'ⅷ': case 'ⅸ': case 'ⅹ': case 'ⅺ': case 'ⅻ':
                case '⓪': case '①': case '②': case '③': case '④': case '⑤':
                case '⑥': case '⑦': case '⑧': case '⑨': case '⑩':
                case '⑪': case '⑫': case '⑬': case '⑭': case '⑮': case '⑯':
                case '⑰': case '⑱': case '⑲': case '⑳':
                case '㉑': case '㉒': case '㉓': case '㉔': case '㉕': case '㉖':
                case '㉗': case '㉘': case '㉙': case '㉚': case '㉛': case '㉜':
                case '㉝': case '㉞': case '㉟': case '㊱': case '㊲': case '㊳':
                case '㊴': case '㊵': case '㊶': case '㊷': case '㊸': case '㊹':
                case '㊺': case '㊻': case '㊼': case '㊽': case '㊾': case '㊿':
                case '△': case '▽': case '▲': case '▼': case '☆': case '★':
                case '♂': case '♀': case '♪': case '♭': case '§': case '†': case '‡':
                case '‼': case '⁇': case '⁉': case '⁈':
                case '©': case '®': case '⁑': case '⁂':
                case '◐': case '◑': case '◒': case '◓': case '▷': case '▶': case '◁': case '◀':
                case '♤': case '♠': case '♢': case '♦': case '♡': case '♥': case '♧': case '♣': case '❤':
                case '☖': case '☗': case '☎': case '☁': case '☂': case '☃': case '♨': case '▱': case '⊿': case '✿':
                case '☹': case '☺': case '☻':
                case '✓': case '✔': case '␣': case '⏎': case '♩': case '♮': case '♫': case '♬':
                case 'ℓ': case '№': case '℡': case 'ℵ': case 'ℏ': case '℧':
                    if (!noTcy) { buf.Append(AozoraEpub3Converter._chukiMap["正立"][0]); buf.Append(ch[idx]); buf.Append(AozoraEpub3Converter._chukiMap["正立終わり"][0]); }
                    else buf.Append(ch[idx]);
                    return;
                default: buf.Append(ch[idx]); return;
            }
        }
        else
        {
            switch (ch[idx])
            {
                case '≪': buf.Append("《"); return;
                case '≫': buf.Append("》"); return;
                case '―': buf.Append("─"); return;
                default: buf.Append(ch[idx]); return;
            }
        }
    }
}
