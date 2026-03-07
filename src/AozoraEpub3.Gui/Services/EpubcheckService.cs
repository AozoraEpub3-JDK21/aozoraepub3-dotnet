using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AozoraEpub3.Gui.Services;

/// <summary>
/// epubcheck (JAR) を外部プロセスとして実行し、JSON 結果をパースする。
/// </summary>
public static class EpubcheckService
{
    /// <summary>
    /// epubcheck を実行して結果を返す。
    /// </summary>
    /// <param name="epubPath">検証対象 EPUB ファイルパス</param>
    /// <param name="jarPath">epubcheck.jar のパス</param>
    /// <param name="ct">キャンセルトークン</param>
    /// <returns>検証結果</returns>
    public static async Task<EpubcheckResult> RunAsync(string epubPath, string jarPath, CancellationToken ct = default)
    {
        if (!File.Exists(jarPath))
            return new EpubcheckResult { Summary = "epubcheck.jar not found" };

        if (!File.Exists(epubPath))
            return new EpubcheckResult { Summary = "EPUB file not found" };

        // java -jar epubcheck.jar --json - <epub>
        var jsonTempFile = Path.Combine(Path.GetTempPath(), $"epubcheck_{Guid.NewGuid():N}.json");
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "java",
                Arguments = $"-jar \"{jarPath}\" \"{epubPath}\" --json \"{jsonTempFile}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            // JSON ファイルをパース
            if (File.Exists(jsonTempFile))
            {
                var jsonText = await File.ReadAllTextAsync(jsonTempFile, ct);
                return ParseJsonResult(jsonText, process.ExitCode);
            }

            // JSON が生成されない場合は stderr からエラーを返す
            return new EpubcheckResult
            {
                ExitCode = process.ExitCode,
                Summary = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr
            };
        }
        catch (Exception ex)
        {
            return new EpubcheckResult { Summary = $"Failed to run epubcheck: {ex.Message}" };
        }
        finally
        {
            try { if (File.Exists(jsonTempFile)) File.Delete(jsonTempFile); } catch { }
        }
    }

    private static EpubcheckResult ParseJsonResult(string jsonText, int exitCode)
    {
        try
        {
            var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            var result = new EpubcheckResult { ExitCode = exitCode };

            // messages 配列
            if (root.TryGetProperty("messages", out var messages))
            {
                foreach (var msg in messages.EnumerateArray())
                {
                    var entry = new EpubcheckMessage
                    {
                        Severity = msg.TryGetProperty("severity", out var sev) ? sev.GetString() ?? "" : "",
                        MessageText = msg.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "",
                        MessageId = msg.TryGetProperty("ID", out var id) ? id.GetString() ?? "" : "",
                    };

                    // locations 配列
                    if (msg.TryGetProperty("locations", out var locs))
                    {
                        foreach (var loc in locs.EnumerateArray())
                        {
                            entry.FileName = loc.TryGetProperty("fileName", out var fn) ? fn.GetString() ?? "" : "";
                            entry.Line = loc.TryGetProperty("line", out var ln) ? ln.GetInt32() : -1;
                            entry.Column = loc.TryGetProperty("column", out var col) ? col.GetInt32() : -1;
                            break; // 最初のロケーションのみ使用
                        }
                    }

                    result.Messages.Add(entry);
                }
            }

            var errors = result.Messages.Count(m => m.Severity == "ERROR" || m.Severity == "FATAL");
            var warnings = result.Messages.Count(m => m.Severity == "WARNING");
            result.Summary = errors == 0 && warnings == 0
                ? "No errors — validation passed"
                : $"{errors} error(s), {warnings} warning(s)";

            return result;
        }
        catch (Exception ex)
        {
            return new EpubcheckResult
            {
                ExitCode = exitCode,
                Summary = $"Failed to parse epubcheck output: {ex.Message}"
            };
        }
    }
}

/// <summary>epubcheck 実行結果</summary>
public sealed class EpubcheckResult
{
    public int ExitCode { get; set; }
    public string Summary { get; set; } = "";
    public List<EpubcheckMessage> Messages { get; set; } = [];
}

/// <summary>epubcheck の1メッセージ</summary>
public sealed class EpubcheckMessage
{
    public string Severity { get; set; } = "";
    public string MessageId { get; set; } = "";
    public string MessageText { get; set; } = "";
    public string FileName { get; set; } = "";
    public int Line { get; set; } = -1;
    public int Column { get; set; } = -1;

    /// <summary>表示用文字列</summary>
    public string DisplayText => Line >= 0
        ? $"[{Severity}] {FileName}:{Line}:{Column} — {MessageText}"
        : $"[{Severity}] {MessageText}";

    /// <summary>エラーまたは FATAL か</summary>
    public bool IsError => Severity is "ERROR" or "FATAL";
}
