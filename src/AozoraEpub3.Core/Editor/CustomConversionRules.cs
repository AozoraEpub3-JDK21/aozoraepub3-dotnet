using System.Text.Json;
using System.Text.RegularExpressions;

namespace AozoraEpub3.Core.Editor;

/// <summary>
/// ユーザー定義の変換ルール1件。
/// テキスト変換パイプラインに組み込まれる。
/// </summary>
public sealed class CustomConversionRule
{
    /// <summary>ルール名（表示用）</summary>
    public string Name { get; set; } = "";

    /// <summary>検索パターン（正規表現）</summary>
    public string Pattern { get; set; } = "";

    /// <summary>置換文字列（正規表現の $1 等が使用可能）</summary>
    public string Replacement { get; set; } = "";

    /// <summary>有効/無効フラグ</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>大文字小文字を無視するか</summary>
    public bool IgnoreCase { get; set; }
}

/// <summary>
/// ユーザー定義変換ルールファイル（JSON）のルートモデル。
/// </summary>
public sealed class CustomConversionRuleFile
{
    /// <summary>ルールセット名</summary>
    public string Name { get; set; } = "";

    /// <summary>変換ルールのリスト</summary>
    public List<CustomConversionRule> Rules { get; set; } = [];
}

/// <summary>
/// カスタム変換ルールサービス（E6-2）。
/// ユーザー定義の正規表現置換ルールを JSON から読み込み、
/// テキスト変換パイプラインに適用する。
/// </summary>
public sealed class CustomConversionRuleService
{
    private readonly List<CustomConversionRule> _rules = [];
    private readonly List<(Regex regex, string replacement, string name)> _compiled = [];

    /// <summary>読み込まれた全ルール</summary>
    public IReadOnlyList<CustomConversionRule> Rules => _rules;

    /// <summary>JSON ファイルからルールを読み込む。</summary>
    public void LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return;
        var json = File.ReadAllText(filePath);
        LoadFromJson(json);
    }

    /// <summary>JSON 文字列からルールを読み込む。</summary>
    public void LoadFromJson(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var file = JsonSerializer.Deserialize<CustomConversionRuleFile>(json, options);
        if (file?.Rules == null) return;

        foreach (var rule in file.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Pattern)) continue;
            _rules.Add(rule);

            if (rule.Enabled)
            {
                try
                {
                    var regexOptions = RegexOptions.Compiled;
                    if (rule.IgnoreCase) regexOptions |= RegexOptions.IgnoreCase;
                    var regex = new Regex(rule.Pattern, regexOptions, TimeSpan.FromSeconds(1));
                    _compiled.Add((regex, rule.Replacement, rule.Name));
                }
                catch (RegexParseException)
                {
                    // 不正な正規表現は無視
                }
            }
        }
    }

    /// <summary>全ルールをクリア。</summary>
    public void Clear()
    {
        _rules.Clear();
        _compiled.Clear();
    }

    /// <summary>テキストに全ルールを適用して変換する。</summary>
    public string Apply(string text)
    {
        if (string.IsNullOrEmpty(text) || _compiled.Count == 0) return text;

        var result = text;
        foreach (var (regex, replacement, _) in _compiled)
        {
            try
            {
                result = regex.Replace(result, replacement);
            }
            catch (RegexMatchTimeoutException)
            {
                // タイムアウトしたルールはスキップ
            }
        }
        return result;
    }

    /// <summary>テキストに対するルール適用結果をプレビューする（変更箇所のリスト）。</summary>
    public IReadOnlyList<RuleMatchResult> Preview(string text)
    {
        if (string.IsNullOrEmpty(text) || _compiled.Count == 0) return [];

        var results = new List<RuleMatchResult>();
        foreach (var (regex, replacement, name) in _compiled)
        {
            try
            {
                foreach (Match m in regex.Matches(text))
                {
                    var replaced = m.Result(replacement);
                    if (m.Value != replaced)
                    {
                        results.Add(new RuleMatchResult
                        {
                            RuleName = name,
                            Original = m.Value,
                            Replaced = replaced,
                            Index = m.Index,
                            Length = m.Length
                        });
                    }
                }
            }
            catch (RegexMatchTimeoutException) { }
        }
        return results;
    }

    /// <summary>デフォルトのルールファイルパスを返す。</summary>
    public static string GetDefaultRulesPath()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AozoraEpub3");
        return Path.Combine(appData, "custom_rules.json");
    }

    /// <summary>サンプルルール JSON を生成する。</summary>
    public static string GenerateSampleJson()
    {
        var sample = new CustomConversionRuleFile
        {
            Name = "マイルール",
            Rules =
            [
                new CustomConversionRule
                {
                    Name = "全角数字→半角",
                    Pattern = "[０-９]",
                    Replacement = "$0" // 実際にはマッチした文字を半角に変換する必要あり
                },
                new CustomConversionRule
                {
                    Name = "二重感嘆符統一",
                    Pattern = "！！+",
                    Replacement = "！！"
                },
                new CustomConversionRule
                {
                    Name = "固有名詞ルビ自動付与",
                    Pattern = "鉄腕アトム",
                    Replacement = "｜鉄腕アトム《てつわんあとむ》"
                }
            ]
        };

        return JsonSerializer.Serialize(sample, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }
}

/// <summary>カスタムルール適用結果の1件</summary>
public sealed class RuleMatchResult
{
    public required string RuleName { get; init; }
    public required string Original { get; init; }
    public required string Replaced { get; init; }
    public required int Index { get; init; }
    public required int Length { get; init; }
}
