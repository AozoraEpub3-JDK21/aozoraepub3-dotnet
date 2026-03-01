using System.Reflection;
using System.Text;

namespace AozoraEpub3.Core.Converter;

/// <summary>青空文庫注記外字をUTF-8・代替文字に変換</summary>
public class AozoraGaijiConverter
{
    /// <summary>青空文庫注記外字をUTF-8に変換 key=注記名, value=UTF文字列</summary>
    public Dictionary<string, string> ChukiUtfMap { get; } = new();

    /// <summary>青空文庫注記外字を代替文字に変換 key=注記名, value=代替文字列</summary>
    public Dictionary<string, string> ChukiAltMap { get; } = new();

    public AozoraGaijiConverter()
    {
        // 埋め込みリソースから読み込み
        LoadResource("chuki_ivs.txt", ChukiUtfMap);
        LoadResource("chuki_utf.txt", ChukiUtfMap);
        LoadResource("chuki_alt.txt", ChukiAltMap);
    }

    public AozoraGaijiConverter(string resourceDir)
    {
        TryLoadFile(Path.Combine(resourceDir, "chuki_ivs.txt"), ChukiUtfMap);
        TryLoadFile(Path.Combine(resourceDir, "chuki_utf.txt"), ChukiUtfMap);
        TryLoadFile(Path.Combine(resourceDir, "chuki_alt.txt"), ChukiAltMap);
    }

    private static void TryLoadFile(string path, Dictionary<string, string> map)
    {
        if (File.Exists(path))
            LoadChukiFile(File.OpenRead(path), Path.GetFileName(path), map);
        // 存在しない場合は埋め込みリソースからフォールバック
        else
            LoadResource(Path.GetFileName(path), map);
    }

    private static void LoadResource(string fileName, Dictionary<string, string> map)
    {
        var asm = Assembly.GetExecutingAssembly();
        // リソース名を検索
        string? resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        if (resourceName == null) return;

        using var stream = asm.GetManifestResourceStream(resourceName)!;
        LoadChukiFile(stream, fileName, map);
    }

    private static void LoadChukiFile(Stream inputStream, string fileName, Dictionary<string, string> map)
    {
        using var reader = new StreamReader(inputStream, Encoding.UTF8);
        string? line;
        int lineNum = 0;
        while ((line = reader.ReadLine()) != null)
        {
            lineNum++;
            if (line.Length == 0 || line[0] == '#') continue;
            try
            {
                // タブ区切り: col0=unicode, col1=..., col2=utf_char, col3=chuki_key(※［＃...）
                int charStart = line.IndexOf('\t');
                if (charStart < 0) continue;
                charStart = line.IndexOf('\t', charStart + 1);
                if (charStart < 0) continue;
                charStart++;

                int chukiStart = line.IndexOf('\t', charStart);
                if (chukiStart < 0) continue;
                chukiStart++;

                // ※［＃ で始まる列を探す
                if (line.IndexOf("※［＃", chukiStart, StringComparison.Ordinal) != chukiStart) continue;

                int chukiEnd = line.IndexOf('\t', chukiStart);
                int chukiCode = line.IndexOf('、', chukiStart);
                // 注記内に「、」がある場合のスキップ
                if (chukiCode != -1 && chukiStart + 1 < line.Length && line[chukiCode + 1] == '「')
                    chukiCode = line.IndexOf('、', chukiCode + 1);
                if (chukiCode != -1 && (chukiEnd < 0 || chukiCode < chukiEnd))
                    chukiEnd = chukiCode + 1;
                if (chukiEnd < 0) chukiEnd = line.Length;

                string utfChar = line.Substring(charStart, chukiStart - 1 - charStart);
                string chuki = line.Substring(chukiStart + 3, chukiEnd - 1 - (chukiStart + 3));

                if (!map.ContainsKey(chuki))
                    map[chuki] = utfChar;
            }
            catch
            {
                LogAppender.Error(lineNum, fileName, line);
            }
        }
    }

    /// <summary>注記をUTF-8文字に変換</summary>
    public string? ToUtf(string chuki) => ChukiUtfMap.TryGetValue(chuki, out var v) ? v : null;

    /// <summary>注記を代替文字列に変換</summary>
    public string? ToAlterString(string chuki) => ChukiAltMap.TryGetValue(chuki, out var v) ? v : null;

    /// <summary>コード表記をUTF-8文字に変換</summary>
    public string? CodeToCharString(string code)
    {
        try
        {
            if (code.StartsWith("U+", StringComparison.OrdinalIgnoreCase))
            {
                int idx = code.IndexOf('-');
                if (idx < 0)
                    return CodeToCharString(Convert.ToInt32(code.Substring(2), 16));
                else
                {
                    string ivs = code.Substring(idx + 1);
                    if (ivs.StartsWith("U+", StringComparison.OrdinalIgnoreCase)) ivs = ivs.Substring(2);
                    return CodeToCharString(Convert.ToInt32(code.Substring(2, idx - 2), 16))
                         + CodeToCharString(Convert.ToInt32(ivs, 16));
                }
            }
            else if (code.StartsWith("UCS-", StringComparison.OrdinalIgnoreCase))
                return CodeToCharString(Convert.ToInt32(code.Substring(4), 16));
            else if (code.StartsWith("unicode", StringComparison.OrdinalIgnoreCase))
                return CodeToCharString(Convert.ToInt32(code.Substring(7), 16));
            else
            {
                // JISコード変換 (第3水準/第4水準)
                string codeStr = code;
                if (codeStr.StartsWith("第3水準")) codeStr = codeStr.Substring(4);
                else if (codeStr.StartsWith("第4水準")) codeStr = codeStr.Substring(4);
                string[] codes = codeStr.Split('-');
                if (codes.Length >= 3)
                    return JisConverter.GetConverter().ToCharString(
                        int.Parse(codes[0]), int.Parse(codes[1]), int.Parse(codes[2]));
            }
        }
        catch { }
        return null;
    }

    public static string? CodeToCharString(int unicode)
    {
        if (unicode == 0) return null;
        return char.ConvertFromUtf32(unicode);
    }
}
