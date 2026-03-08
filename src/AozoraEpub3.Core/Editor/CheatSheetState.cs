namespace AozoraEpub3.Core.Editor;

/// <summary>チートシートの表示状態</summary>
public enum CheatSheetMode
{
    /// <summary>初回起動時: 背中押し + 操作ガイド</summary>
    FirstTime,
    /// <summary>執筆中: 記法リファレンス</summary>
    Writing,
    /// <summary>停滞中: 背中押し</summary>
    Stalled,
    /// <summary>新機能紹介</summary>
    NewFeature
}

/// <summary>
/// チートシートの状態管理（ルールベース）。
/// ユーザーの操作パターンに応じて表示内容を切り替える。
/// </summary>
public sealed class CheatSheetStateManager
{
    private DateTime _lastInputTime = DateTime.MinValue;
    private bool _hasEverTyped;
    private bool _isFirstOpen = true;
    private readonly TimeSpan _stalledThreshold = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _writingThreshold = TimeSpan.FromMinutes(3);

    /// <summary>現在の状態を取得する</summary>
    public CheatSheetMode CurrentMode
    {
        get
        {
            if (_isFirstOpen && !_hasEverTyped)
                return CheatSheetMode.FirstTime;

            var elapsed = DateTime.Now - _lastInputTime;

            if (elapsed < _writingThreshold)
                return CheatSheetMode.Writing;

            if (_hasEverTyped && elapsed > _stalledThreshold)
                return CheatSheetMode.Stalled;

            return CheatSheetMode.Writing;
        }
    }

    /// <summary>文字入力があったことを通知する</summary>
    public void NotifyInput()
    {
        _lastInputTime = DateTime.Now;
        _hasEverTyped = true;
        _isFirstOpen = false;
    }

    /// <summary>エディタが開かれたことを通知する</summary>
    public void NotifyEditorOpened()
    {
        if (_hasEverTyped)
            _isFirstOpen = false;
    }

    /// <summary>初回フラグをリセットする（テスト用）</summary>
    public void Reset()
    {
        _lastInputTime = DateTime.MinValue;
        _hasEverTyped = false;
        _isFirstOpen = true;
    }
}
