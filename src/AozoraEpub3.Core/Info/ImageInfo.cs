namespace AozoraEpub3.Core.Info;

/// <summary>画像情報</summary>
public class ImageInfo
{
    /// <summary>ファイルのID 0001</summary>
    public string? Id { get; set; }
    /// <summary>出力ファイル名 拡張子付き 0001.png</summary>
    public string? OutFileName { get; set; }
    /// <summary>画像フォーマット png jpg gif</summary>
    public string Ext { get; set; }

    /// <summary>画像幅</summary>
    public int Width { get; set; } = -1;
    /// <summary>画像高さ</summary>
    public int Height { get; set; } = -1;

    /// <summary>出力画像幅</summary>
    public int OutWidth { get; set; } = -1;
    /// <summary>出力画像高さ</summary>
    public int OutHeight { get; set; } = -1;

    /// <summary>Zip内ファイルentryの位置</summary>
    public int ZipIndex { get; set; } = -1;

    /// <summary>カバー画像ならtrue</summary>
    public bool IsCover { get; set; } = false;

    /// <summary>回転角度 右 90 左 -90</summary>
    public int RotateAngle { get; set; } = 0;

    public ImageInfo(string ext, int width, int height, int zipIndex)
    {
        Ext = ext.ToLowerInvariant();
        Width = width;
        Height = height;
        ZipIndex = zipIndex;
    }

    /// <summary>mime形式(image/png)の形式フォーマット文字列を返却</summary>
    public string Format => "image/" + (Ext == "jpg" ? "jpeg" : Ext);

    /// <summary>ストリームから画像情報を生成（サイズのみ読み取り）</summary>
    public static ImageInfo? GetImageInfo(Stream stream, int zipIndex = -1)
    {
        try
        {
            var bytes = new byte[16];
            int read = stream.Read(bytes, 0, bytes.Length);
            if (read < 8) return null;

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            {
                // Read IHDR chunk for width/height (bytes 16-23)
                stream.Seek(0, SeekOrigin.Begin);
                var buf = new byte[24];
                stream.Read(buf, 0, buf.Length);
                int w = (buf[16] << 24) | (buf[17] << 16) | (buf[18] << 8) | buf[19];
                int h = (buf[20] << 24) | (buf[21] << 16) | (buf[22] << 8) | buf[23];
                return new ImageInfo("png", w, h, zipIndex);
            }
            // JPEG: FF D8
            else if (bytes[0] == 0xFF && bytes[1] == 0xD8)
            {
                return GetJpegSize(stream, zipIndex);
            }
            // GIF: 47 49 46
            else if (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
            {
                int w = bytes[6] | (bytes[7] << 8);
                int h = bytes[8] | (bytes[9] << 8);
                return new ImageInfo("gif", w, h, zipIndex);
            }
        }
        catch { }
        return null;
    }

    private static ImageInfo? GetJpegSize(Stream stream, int zipIndex)
    {
        try
        {
            stream.Seek(2, SeekOrigin.Begin);
            var buf = new byte[4];
            while (true)
            {
                if (stream.Read(buf, 0, 2) != 2) break;
                if (buf[0] != 0xFF) break;
                byte marker = buf[1];
                if (stream.Read(buf, 0, 2) != 2) break;
                int length = (buf[0] << 8) | buf[1];
                // SOF markers: C0-C3, C5-C7, C9-CB, CD-CF
                if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                {
                    var sof = new byte[5];
                    if (stream.Read(sof, 0, 5) != 5) break;
                    int h = (sof[1] << 8) | sof[2];
                    int w = (sof[3] << 8) | sof[4];
                    return new ImageInfo("jpg", w, h, zipIndex);
                }
                stream.Seek(length - 2, SeekOrigin.Current);
            }
        }
        catch { }
        return null;
    }

    public static ImageInfo? GetImageInfoFromFile(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        return GetImageInfo(fs, -1);
    }
}
