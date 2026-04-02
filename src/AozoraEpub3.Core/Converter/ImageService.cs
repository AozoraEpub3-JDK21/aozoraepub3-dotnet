using System.Text;
using System.Text.RegularExpressions;
using AozoraEpub3.Core.Info;
using AozoraEpub3.Core.Io;

namespace AozoraEpub3.Core.Converter;

/// <summary>画像注記処理ロジック</summary>
internal sealed class ImageService
{
    private readonly ConverterSettings _settings;
    private readonly ConverterState _state;
    private readonly IEpub3Writer _writer;
    private readonly OutputBufferService _outputService;

    public ImageService(ConverterSettings settings, ConverterState state,
        IEpub3Writer writer, OutputBufferService outputService)
    {
        _settings = settings;
        _state = state;
        _writer = writer;
        _outputService = outputService;
    }

    /// <summary>改ページ処理があった場合、画像のみページの画像行をbookInfoに追加</summary>
    internal void CheckImageOnly(BookInfo bookInfo, string?[] preLines, string line, int lineNum)
    {
        if (preLines[0] == null) return;
        int bracketIdx = line.IndexOf('］');
        if (bracketIdx <= 3) return;

        string curChuki = line[2..bracketIdx];
        if (!AozoraEpub3Converter.ChukiFlagPageBreak.Contains(curChuki)) return;

        // 2行前の行末が改ページまたは現在行が先頭から2行目
        bool prevIsPageBreak = preLines[1] == null;
        if (!prevIsPageBreak)
        {
            int prev1BracketIdx = preLines[1]!.IndexOf('］');
            if (prev1BracketIdx > 3)
            {
                int sharpIdx = preLines[1]!.LastIndexOf('＃') + 1;
                if (sharpIdx > 0 && sharpIdx < preLines[1]!.Length - 1)
                {
                    string prev1Chuki = preLines[1]![sharpIdx..(preLines[1]!.Length - 1)];
                    prevIsPageBreak = AozoraEpub3Converter.ChukiFlagPageBreak.Contains(prev1Chuki);
                }
            }
        }
        if (!prevIsPageBreak) return;

        // 1行前が画像のみの行か確認（preLines[0] は先頭でnullチェック済み）
        string prev0 = preLines[0]!;
        string prev0Lower = prev0.ToLower();
        bool isImageLine =
            (prev0.StartsWith("［＃") &&
             Regex.IsMatch(prev0, @"^［＃.*（.+\..+") &&
             prev0.IndexOf('］') == prev0.Length - 1) ||
            (prev0Lower.StartsWith("<img") &&
             prev0.IndexOf('>') == prev0.Length - 1);

        if (!isImageLine) return;

        string? fileName;
        if (prev0Lower.StartsWith("<img"))
            fileName = GetTagAttr(prev0, "src");
        else
            fileName = GetImageChukiFileName(prev0, prev0.IndexOf('（'));

        bookInfo.AddImageSectionLine(lineNum - 1, fileName ?? "");
    }

    /// <summary>タグからattr属性値を取得</summary>
    internal string? GetTagAttr(string tag, string attr)
    {
        string lowerTag = tag.ToLower();
        int srcIdx = lowerTag.IndexOf(" " + attr + "=");
        if (srcIdx == -1) return null;
        int start = srcIdx + attr.Length + 2;
        if (start >= lowerTag.Length) return null;
        int end = -1;
        if (lowerTag[start] == '"') end = lowerTag.IndexOf('"', start + 1);
        else if (lowerTag[start] == '\'') end = lowerTag.IndexOf('\'', start + 1);
        if (end == -1) { end = lowerTag.IndexOf('>', start); start--; }
        if (end == -1) { end = lowerTag.IndexOf(' ', start); start--; }
        if (end != -1 && start + 1 <= end) return tag[(start + 1)..end].Trim();
        return null;
    }

    /// <summary>画像注記にキャプション付きの指定がある場合true</summary>
    internal bool HasImageCaption(string chukiTag) =>
        chukiTag.IndexOf("キャプション付き") > 0;

    /// <summary>画像注記からファイル名取得</summary>
    /// <param name="chukiTag">注記全体</param>
    /// <param name="startIdx">画像注記の'（'の位置</param>
    internal string? GetImageChukiFileName(string chukiTag, int startIdx)
    {
        if (startIdx < 0 || startIdx >= chukiTag.Length) return null;
        int endIdx = chukiTag.IndexOf('、', startIdx + 1);
        int closeIdx = chukiTag.IndexOf('）', startIdx + 1);
        if (closeIdx == -1)
        {
            // '）' なし: '、' のみ使用
        }
        else if (endIdx == -1)
        {
            endIdx = closeIdx;
        }
        else
        {
            endIdx = Math.Min(endIdx, closeIdx);
        }
        if (endIdx > startIdx && endIdx >= 0) return chukiTag[(startIdx + 1)..endIdx];
        return null;
    }

    /// <summary>画像タグを出力。単ページ出力なら true を返す。Java: printImageChuki</summary>
    internal bool PrintImageChuki(TextWriter? output, StringBuilder buf, string srcFileName,
        string dstFileName, bool hasCaption, int lineNum)
    {
        int imagePageType = _writer.GetImagePageType(srcFileName, _state.TagLevel, lineNum, hasCaption);
        double ratio = _writer.GetImageWidthRatio(srcFileName, hasCaption);

        if (imagePageType == PageBreakType.IMAGE_INLINE_W)
        {
            if (ratio <= 0) buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像横"][0], dstFileName);
            else buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像幅"][0], ratio, dstFileName);
        }
        else if (imagePageType == PageBreakType.IMAGE_INLINE_H)
        {
            if (ratio <= 0) buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像縦"][0], dstFileName);
            else buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像幅"][0], ratio, dstFileName);
        }
        else if (imagePageType == PageBreakType.IMAGE_INLINE_TOP_W)
        {
            if (ratio <= 0) buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像上横"][0], dstFileName);
            else buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像幅上"][0], ratio, dstFileName);
        }
        else if (imagePageType == PageBreakType.IMAGE_INLINE_BOTTOM_W)
        {
            if (ratio <= 0) buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像下横"][0], dstFileName);
            else buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像幅下"][0], ratio, dstFileName);
        }
        else if (imagePageType == PageBreakType.IMAGE_INLINE_TOP)
        {
            if (ratio <= 0) buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像上"][0], dstFileName);
            else buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像幅上"][0], ratio, dstFileName);
        }
        else if (imagePageType == PageBreakType.IMAGE_INLINE_BOTTOM)
        {
            if (ratio <= 0) buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像下"][0], dstFileName);
            else buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像幅下"][0], ratio, dstFileName);
        }
        else if (imagePageType != PageBreakType.IMAGE_PAGE_NONE)
        {
            // 単ページ
            if (ratio != -1 && _settings.ImageFloatPage)
            {
                if (imagePageType == PageBreakType.IMAGE_PAGE_W)
                    buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像単横浮"][0], dstFileName);
                else if (imagePageType == PageBreakType.IMAGE_PAGE_H)
                    buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像単縦浮"][0], dstFileName);
                else if (ratio <= 0) buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像単浮"][0], dstFileName);
                else buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像単幅浮"][0], ratio, dstFileName);
            }
            else
            {
                if (buf.Length > 0) _outputService.PrintLineBuffer(output, buf, lineNum, true);
                buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像"][0], dstFileName);
                buf.Append(AozoraEpub3Converter._chukiMap["画像終わり"][0]);
                PrintImagePage(output, buf, lineNum, srcFileName, dstFileName, imagePageType);
                return true;
            }
        }
        else
        {
            // 画像通常表示
            if (ratio != -1 && _settings.ImageFloatBlock)
            {
                if (ratio <= 0) buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像浮"][0], dstFileName);
                else buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像幅浮"][0], ratio, dstFileName);
            }
            else
            {
                if (ratio <= 0) buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像"][0], dstFileName);
                else buf.AppendFormat(AozoraEpub3Converter._chukiMap["画像幅"][0], ratio, dstFileName);
            }
        }

        if (hasCaption) { _state.InImageTag = true; _state.NextLineIsCaption = true; }
        else buf.Append(AozoraEpub3Converter._chukiMap["画像終わり"][0]);
        return false;
    }

    /// <summary>前後に改ページを入れて画像を出力。Java: printImagePage</summary>
    internal void PrintImagePage(TextWriter? output, StringBuilder buf, int lineNum,
        string srcFileName, string dstFileName, int imagePageType)
    {
        bool hasPageBreakTrigger = _state.PageBreakTrigger != null && !_state.PageBreakTrigger.NoChapter;

        switch (imagePageType)
        {
            case PageBreakType.IMAGE_PAGE_W:
                _outputService.SetPageBreakTrigger(AozoraEpub3Converter._pageBreakImageW);
                AozoraEpub3Converter._pageBreakImageW.SrcFileName = srcFileName;
                break;
            case PageBreakType.IMAGE_PAGE_H:
                _outputService.SetPageBreakTrigger(AozoraEpub3Converter._pageBreakImageH);
                AozoraEpub3Converter._pageBreakImageH.SrcFileName = srcFileName;
                break;
            case PageBreakType.IMAGE_PAGE_NOFIT:
                _outputService.SetPageBreakTrigger(AozoraEpub3Converter._pageBreakImageNoFit);
                AozoraEpub3Converter._pageBreakImageNoFit.SrcFileName = srcFileName;
                break;
            default:
                _outputService.SetPageBreakTrigger(AozoraEpub3Converter._pageBreakImageAuto);
                AozoraEpub3Converter._pageBreakImageAuto.SrcFileName = srcFileName;
                break;
        }
        _outputService.PrintLineBuffer(output, buf, lineNum, true);

        if (hasPageBreakTrigger) _outputService.SetPageBreakTrigger(AozoraEpub3Converter._pageBreakNormal);
        else _outputService.SetPageBreakTrigger(AozoraEpub3Converter._pageBreakNoChapter);
    }
}
