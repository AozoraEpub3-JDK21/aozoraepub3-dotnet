using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AozoraEpub3.Core.Editor;

namespace AozoraEpub3.Gui.ViewModels;

/// <summary>
/// コンテキスト依存型チートシートの ViewModel。
/// ユーザーの状態に応じて背中押し/操作ガイド/記法リファレンスを切り替える。
/// </summary>
public sealed partial class CheatSheetViewModel : ViewModelBase
{
    private readonly CheatSheetStateManager _stateManager = new();

    // ───── 表示状態 ──────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private CheatSheetMode _currentMode = CheatSheetMode.FirstTime;

    public bool IsMotivationVisible => CurrentMode is CheatSheetMode.FirstTime or CheatSheetMode.Stalled;
    public bool IsHowToVisible => CurrentMode == CheatSheetMode.FirstTime;
    public bool IsReferenceVisible => CurrentMode == CheatSheetMode.Writing;

    // ───── 記法リファレンスのタブ ────────────────────────────────────────

    [ObservableProperty]
    private int _referenceTabIndex;

    public string[] ReferenceTabs { get; } =
        ["基本記法", "傍点・強調", "見出し・構造", "ショートカット"];

    // ───── 統計情報（背中押しパネル用）────────────────────────────────────

    [ObservableProperty]
    private int _totalWordCount;

    [ObservableProperty]
    private int _todayWordCount;

    public string MotivationText => TotalWordCount switch
    {
        0 => "まず、1000字書いてみましょう。\n原稿用紙2.5枚。\nこのチートシートの文章くらいの量です。",
        < 1000 => $"あと{1000 - TotalWordCount}字で1000字です。\nもう少し！",
        < 5000 => "順調です！\n5000字を目指してみましょう。",
        < 10000 => "素晴らしい！\n1万字まであと少しです。",
        _ => "すごい！\nどんどん積み上がっています。"
    };

    // ───── 記法リファレンス内容 ──────────────────────────────────────────

    public string BasicSyntaxText => """
        ■ ルビ
          |漢字《かんじ》  → 漢字にルビが振られます
          ショートカット: Ctrl+R

        ■ 傍点（圏点）
          《《強調したい文字》》  → 傍点が付きます
          ショートカット: Ctrl+E

        ■ 太字
          **太字にしたい文字**  → 太字になります
          ショートカット: Ctrl+B
        """;

    public string EmphasisSyntaxText => """
        ■ 傍点
          《《文字》》  → ﹅文﹅字  (ゴマ点)

        ■ 太字
          **文字**  → 文字

        ■ 青空文庫記法（直接入力も可）
          ［＃傍点］文字［＃傍点終わり］
          ［＃太字］文字［＃太字終わり］
          ［＃斜体］文字［＃斜体終わり］
        """;

    public string HeadingSyntaxText => """
        ■ 見出し
          # 大見出し     → 章タイトル（h1）
          ## 中見出し    → 節タイトル（h2）
          ### 小見出し   → 項タイトル（h3）
          ショートカット: Ctrl+H

        ■ 改ページ
          ---  → 新しいページから始まります
          ショートカット: Ctrl+Shift+P

        ■ 字下げ
          > 引用文  → 1字下げブロック
        """;

    public string ShortcutText => """
        ■ ファイル操作
          Ctrl+N        新規作成
          Ctrl+O        ファイルを開く
          Ctrl+S        保存
          Ctrl+Shift+S  名前を付けて保存

        ■ 記法挿入
          Ctrl+R        ルビ
          Ctrl+E        傍点
          Ctrl+B        太字
          Ctrl+H        見出し
          Ctrl+Shift+P  改ページ

        ■ その他
          Ctrl+Shift+F  テキスト整形
          F1            チートシート表示/非表示
          F5            プレビュー表示/非表示
        """;

    public string HowToText => """
        ■ 使い方
          「+ 新しい話を追加」→ カードを追加
          カードをクリック    → 右側のエディタで編集
          EPUB変換ボタン     → 本にする

        ■ はじめの一歩
          サンプルカードの続きを書いてみましょう。
          または、新しいカードを追加して
          自分の話を書いてみましょう。
        """;

    // ───── コマンド ──────────────────────────────────────────────────────

    [RelayCommand]
    private void Toggle()
    {
        IsVisible = !IsVisible;
    }

    [RelayCommand]
    private void ShowReference()
    {
        CurrentMode = CheatSheetMode.Writing;
        IsVisible = true;
    }

    /// <summary>文字入力を通知して状態を更新する</summary>
    public void NotifyInput(int totalWordCount, int todayWordCount = 0)
    {
        _stateManager.NotifyInput();
        TotalWordCount = totalWordCount;
        TodayWordCount = todayWordCount;
        UpdateMode();
    }

    /// <summary>エディタが開かれたことを通知する</summary>
    public void NotifyEditorOpened()
    {
        _stateManager.NotifyEditorOpened();
        UpdateMode();
    }

    private void UpdateMode()
    {
        var newMode = _stateManager.CurrentMode;
        if (newMode != CurrentMode)
        {
            CurrentMode = newMode;
            OnPropertyChanged(nameof(IsMotivationVisible));
            OnPropertyChanged(nameof(IsHowToVisible));
            OnPropertyChanged(nameof(IsReferenceVisible));
            OnPropertyChanged(nameof(MotivationText));
        }
    }

    partial void OnCurrentModeChanged(CheatSheetMode value)
    {
        OnPropertyChanged(nameof(IsMotivationVisible));
        OnPropertyChanged(nameof(IsHowToVisible));
        OnPropertyChanged(nameof(IsReferenceVisible));
        OnPropertyChanged(nameof(MotivationText));
    }

    partial void OnTotalWordCountChanged(int value)
    {
        OnPropertyChanged(nameof(MotivationText));
    }
}
