namespace AozoraEpub3.Core.Converter;

/// <summary>ログ出力ユーティリティ</summary>
public static class LogAppender
{
    public static Action<string> OutputAction { get; set; } = Console.Error.WriteLine;

    public static void Println(string msg) => OutputAction(msg);
    public static void Print(string msg) => OutputAction(msg);
    public static void Append(string msg) => OutputAction(msg);
    public static void Warn(int lineNum, string msg) => OutputAction($"[WARN] line {lineNum}: {msg}");
    public static void Warn(int lineNum, string msg, string detail) => OutputAction($"[WARN] line {lineNum}: {msg} {detail}");
    public static void Error(int lineNum, string msg) => OutputAction($"[ERROR] line {lineNum}: {msg}");
    public static void Error(int lineNum, string fileName, string line) => OutputAction($"[ERROR] {fileName} line {lineNum}: {line}");
}
