namespace AozoraEpub3.Core.Editor;

/// <summary>括弧ペアリングの結果</summary>
public sealed class BracketPairResult
{
    public required string TextToInsert { get; init; }
    public required int CursorOffset { get; init; }
    public bool ShouldSkip { get; init; }
    public bool ShouldDeletePair { get; init; }
}

/// <summary>ルビ入力アシストの結果</summary>
public sealed class RubyAssistResult
{
    public required string TextToInsert { get; init; }
    public required int CursorOffset { get; init; }
}

/// <summary>スニペット挿入の結果</summary>
public sealed class SnippetResult
{
    public required string TextToInsert { get; init; }
    public required int CursorOffset { get; init; }
    public bool IsLineLevel { get; init; }
    public bool InsertNewlineAfter { get; init; }
}

/// <summary>
/// エディタ上の入力支援を統合管理するサービス。
/// - 括弧自動ペアリング
/// - 注記補完（［＃ トリガー）
/// - ルビ入力アシスト
/// - ツールバースニペット挿入
///
/// すべての操作は「書く手を止めない」ことを最優先に設計。
/// IME 変換中はペアリングを無効化すること（呼び出し側の責務）。
/// </summary>
public sealed class EditorSuggestService
{
    private static readonly Dictionary<char, char> BracketPairs = new()
    {
        ['「'] = '」', ['『'] = '』', ['（'] = '）', ['《'] = '》',
        ['［'] = '］', ['｛'] = '｝', ['('] = ')', ['['] = ']',
        ['{'] = '}',
    };

    private static readonly HashSet<char> CloseBrackets = [.. BracketPairs.Values];

    private readonly ChukiDictionary _chukiDictionary;

    public EditorSuggestService(ChukiDictionary? chukiDictionary = null)
    {
        _chukiDictionary = chukiDictionary ?? new ChukiDictionary();
    }

    // ════════════════════════════════════════════════════════
    // 括弧ペアリング
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// 文字入力時の括弧ペアリング処理。
    /// IME 変換中はこのメソッドを呼ばないこと。
    /// </summary>
    public BracketPairResult? HandleBracketInput(
        char inputChar,
        char? charAfterCursor,
        char? charBeforeCursor,
        string? selectedText)
    {
        // 開き括弧の場合
        if (BracketPairs.TryGetValue(inputChar, out var closeBracket))
        {
            // 選択テキストがある場合 → 囲む
            if (!string.IsNullOrEmpty(selectedText))
            {
                return new BracketPairResult
                {
                    TextToInsert = $"{inputChar}{selectedText}{closeBracket}",
                    CursorOffset = selectedText.Length + 2
                };
            }

            // 通常 → ペア挿入
            return new BracketPairResult
            {
                TextToInsert = $"{inputChar}{closeBracket}",
                CursorOffset = 1
            };
        }

        // 閉じ括弧の場合 → スキップ判定
        if (CloseBrackets.Contains(inputChar) && charAfterCursor == inputChar)
        {
            return new BracketPairResult
            {
                ShouldSkip = true,
                TextToInsert = "",
                CursorOffset = 1
            };
        }

        return null;
    }

    /// <summary>
    /// バックスペース時のペア削除判定。
    /// カーソルが空の括弧ペアの間にある場合、ペアごと削除する。
    /// </summary>
    public BracketPairResult? HandleBackspace(char? charBeforeCursor, char? charAfterCursor)
    {
        if (charBeforeCursor.HasValue && charAfterCursor.HasValue)
        {
            if (BracketPairs.TryGetValue(charBeforeCursor.Value, out var expected)
                && expected == charAfterCursor.Value)
            {
                return new BracketPairResult
                {
                    ShouldDeletePair = true,
                    TextToInsert = "",
                    CursorOffset = 0
                };
            }
        }
        return null;
    }

    // ════════════════════════════════════════════════════════
    // 注記補完
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// カーソル前のテキストから注記補完のトリガーを検出する。
    /// 「［＃」の後に続く文字列を返す。トリガーでなければ null。
    /// </summary>
    public string? DetectAnnotationTrigger(string textBeforeCursor)
    {
        // 「［＃」を後方検索
        var idx = textBeforeCursor.LastIndexOf("［＃", StringComparison.Ordinal);
        if (idx < 0) return null;

        var afterTrigger = textBeforeCursor[(idx + 2)..];

        // 閉じ括弧「］」があれば補完済み
        if (afterTrigger.Contains('］')) return null;

        return afterTrigger;
    }

    /// <summary>注記補完の候補を取得する</summary>
    public IReadOnlyList<ChukiSuggestItem> GetAnnotationSuggestions(string filterText)
        => _chukiDictionary.Search(filterText);

    /// <summary>
    /// 注記候補を選択したときの挿入テキストを生成する。
    /// すでに入力されている「［＃filterText」は置換対象。
    /// </summary>
    public SnippetResult ApplyAnnotationSuggestion(ChukiSuggestItem item, string currentFilterText)
    {
        // 「［＃filterText」を item.InsertText に置換
        // InsertText は「［＃xxx］」形式なので、先頭の「［＃」を除いたテキストが挿入対象
        // currentFilterText 部分はすでに入力済みなので、残りを補完する

        var fullText = item.InsertText;
        var alreadyTyped = "［＃" + currentFilterText;
        var remaining = fullText[alreadyTyped.Length..];

        // カーソルオフセットも調整
        var cursorOffset = item.CursorOffset - alreadyTyped.Length;

        return new SnippetResult
        {
            TextToInsert = remaining,
            CursorOffset = cursorOffset
        };
    }

    // ════════════════════════════════════════════════════════
    // ルビ入力アシスト
    // ════════════════════════════════════════════════════════

    /// <summary>Ctrl+R またはツールバー「ルビ」ボタン</summary>
    public RubyAssistResult InsertRuby(string? selectedText)
    {
        if (string.IsNullOrEmpty(selectedText))
        {
            return new RubyAssistResult
            {
                TextToInsert = "|《》",
                CursorOffset = 1
            };
        }

        return new RubyAssistResult
        {
            TextToInsert = $"|{selectedText}《》",
            CursorOffset = selectedText.Length + 2
        };
    }

    // ════════════════════════════════════════════════════════
    // ツールバースニペット
    // ════════════════════════════════════════════════════════

    /// <summary>スニペットIDと選択テキストから挿入テキストを生成</summary>
    public SnippetResult GetSnippet(string snippetId, string? selectedText)
    {
        return snippetId switch
        {
            "ruby" => ToSnippet(InsertRuby(selectedText)),
            "emphasis" => new SnippetResult
            {
                TextToInsert = string.IsNullOrEmpty(selectedText) ? "《《》》" : $"《《{selectedText}》》",
                CursorOffset = string.IsNullOrEmpty(selectedText) ? 2 : selectedText.Length + 2
            },
            "bold" => new SnippetResult
            {
                TextToInsert = string.IsNullOrEmpty(selectedText) ? "****" : $"**{selectedText}**",
                CursorOffset = string.IsNullOrEmpty(selectedText) ? 2 : selectedText.Length + 2
            },
            "heading" => new SnippetResult
            {
                TextToInsert = string.IsNullOrEmpty(selectedText) ? "# " : $"# {selectedText}",
                CursorOffset = string.IsNullOrEmpty(selectedText) ? 2 : selectedText.Length + 2,
                IsLineLevel = true
            },
            "pagebreak" => new SnippetResult
            {
                TextToInsert = "---",
                CursorOffset = 3,
                IsLineLevel = true,
                InsertNewlineAfter = true
            },
            "indent_start" => new SnippetResult
            {
                TextToInsert = "［＃ここから１字下げ］",
                CursorOffset = 11,
                IsLineLevel = true
            },
            "indent_end" => new SnippetResult
            {
                TextToInsert = "［＃ここで字下げ終わり］",
                CursorOffset = 12,
                IsLineLevel = true
            },
            _ => throw new ArgumentException($"Unknown snippet: {snippetId}")
        };
    }

    private static SnippetResult ToSnippet(RubyAssistResult ruby)
        => new() { TextToInsert = ruby.TextToInsert, CursorOffset = ruby.CursorOffset };

    // ════════════════════════════════════════════════════════
    // ユーティリティ
    // ════════════════════════════════════════════════════════

    /// <summary>指定文字が開き括弧か</summary>
    public static bool IsOpenBracket(char c) => BracketPairs.ContainsKey(c);

    /// <summary>指定文字が閉じ括弧か</summary>
    public static bool IsCloseBracket(char c) => CloseBrackets.Contains(c);

    /// <summary>開き括弧に対応する閉じ括弧を取得</summary>
    public static char? GetCloseBracket(char openBracket)
        => BracketPairs.TryGetValue(openBracket, out var close) ? close : null;
}
