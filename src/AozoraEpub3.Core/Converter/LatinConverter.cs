using System.Reflection;
using System.Text;

namespace AozoraEpub3.Core.Converter;

/// <summary>「基本ラテン文字のみによる拡張ラテン文字Aの分解表記」の変換クラス</summary>
public class LatinConverter
{
    /// <summary>分解表記文字列→拡張ラテン文字</summary>
    private readonly Dictionary<string, char> _latinMap = new();

    public LatinConverter()
    {
        var asm = Assembly.GetExecutingAssembly();
        string? resourceName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("chuki_latin.txt", StringComparison.OrdinalIgnoreCase));
        if (resourceName == null) return;

        using var stream = asm.GetManifestResourceStream(resourceName)!;
        Load(stream, "chuki_latin.txt");
    }

    public LatinConverter(string filePath)
    {
        if (File.Exists(filePath))
            using (var stream = File.OpenRead(filePath))
                Load(stream, filePath);
    }

    private void Load(Stream inputStream, string fileName)
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
                var values = line.Split('\t');
                if (values.Length < 2) continue;
                char ch = values[1][0];
                if (values[0].Length > 0)
                    _latinMap[values[0]] = ch;
            }
            catch
            {
                LogAppender.Error(lineNum, fileName, line);
            }
        }
    }

    /// <summary>分解表記を含む英字文字列をUTF-8文字列に変換</summary>
    public string ToLatinString(string separated)
    {
        char[] ch = separated.ToCharArray();
        char[] output = new char[ch.Length];
        int outIdx = 0;

        for (int i = 0; i < ch.Length; i++)
        {
            // 3文字マッチ
            if (i < ch.Length - 2 && _latinMap.TryGetValue(new string(ch, i, 3), out char latin3))
            {
                output[outIdx++] = latin3;
                i += 2;
                continue;
            }
            // 2文字マッチ
            if (i < ch.Length - 1 && _latinMap.TryGetValue(new string(ch, i, 2), out char latin2))
            {
                output[outIdx++] = latin2;
                i++;
                continue;
            }
            output[outIdx++] = ch[i];
        }

        return new string(output, 0, outIdx);
    }
}
