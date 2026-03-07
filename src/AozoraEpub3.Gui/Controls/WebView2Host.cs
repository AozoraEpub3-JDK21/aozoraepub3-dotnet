using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Microsoft.Web.WebView2.Core;

namespace AozoraEpub3.Gui.Controls;

/// <summary>
/// Avalonia の NativeControlHost 上で CoreWebView2Controller を直接ホストする。
/// WinForms/WPF への依存なし。Windows 専用。
/// </summary>
public sealed class WebView2Host : NativeControlHost, IDisposable
{
    private CoreWebView2Controller? _controller;
    private CoreWebView2? _coreWebView;
    private IntPtr _hwndHost;
    private string? _pendingUri;
    private string? _pendingHtml;
    private bool _isInitialized;

    private static readonly string UserDataFolder =
        Path.Combine(Path.GetTempPath(), "AozoraEpub3Preview", "WebView2Data");

    private string _currentFont = "ＭＳ 明朝";

    /// <summary>WebView2 の初期化完了後に発火する。</summary>
    public event EventHandler? WebViewReady;

    /// <summary>上下キーでの話移動を要求するイベント。引数: "prev" or "next"</summary>
    public event Action<string>? SpineNavigationRequested;

    /// <summary>初期化済みかどうか。</summary>
    public bool IsWebViewReady => _isInitialized;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        // NativeControlHost が管理する子ウィンドウを作成
        _hwndHost = CreateHostWindow(parent.Handle);

        // WebView2 の非同期初期化を開始
        _ = InitializeAsync(_hwndHost);

        return new PlatformHandle(_hwndHost, "HWND");
    }

    private async Task InitializeAsync(IntPtr hwnd)
    {
        try
        {
            Directory.CreateDirectory(UserDataFolder);
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: UserDataFolder);
            _controller = await env.CreateCoreWebView2ControllerAsync(hwnd);
            _coreWebView = _controller.CoreWebView2;
            _isInitialized = true;

            // セキュリティ: ローカルファイル表示専用
            _coreWebView.Settings.AreDefaultContextMenusEnabled = false;
            _coreWebView.Settings.AreDevToolsEnabled = false;
            _coreWebView.Settings.IsStatusBarEnabled = false;
            _coreWebView.Settings.IsWebMessageEnabled = true;

            // ナビゲーション完了時にスクロール補助スクリプトを注入
            _coreWebView.NavigationCompleted += OnNavigationCompleted;
            _coreWebView.WebMessageReceived += OnWebMessageReceived;

            // ホストウィンドウのサイズに合わせる
            UpdateControllerBounds();

            WebViewReady?.Invoke(this, EventArgs.Empty);

            // 初期化前にリクエストされたナビゲーションを実行
            if (_pendingUri != null)
            {
                _coreWebView.Navigate(_pendingUri);
                _pendingUri = null;
            }
            else if (_pendingHtml != null)
            {
                _coreWebView.NavigateToString(_pendingHtml);
                _pendingHtml = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"WebView2 init failed: {ex.Message}");
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        UpdateControllerBounds();
        return result;
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateControllerBounds();
    }

    private void UpdateControllerBounds()
    {
        if (_controller == null || _hwndHost == IntPtr.Zero) return;

        // Avalonia のレイアウトサイズから DPI を考慮してピクセルサイズを計算
        var scaling = VisualRoot is TopLevel tl ? tl.RenderScaling : 1.0;
        var w = (int)(Bounds.Width * scaling);
        var h = (int)(Bounds.Height * scaling);

        if (w > 0 && h > 0)
        {
            // HWND のサイズを明示的に更新
            MoveWindow(_hwndHost, 0, 0, w, h, true);
            _controller.Bounds = new System.Drawing.Rectangle(0, 0, w, h);
            _controller.IsVisible = true;
        }
    }

    // ───── ナビゲーション完了時のスクリプト注入 ─────────────────────

    private async void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess || _coreWebView == null) return;
        await _coreWebView.ExecuteScriptAsync(ScrollHelperScript);
        await ApplyFontAsync(_currentFont);
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var msg = e.TryGetWebMessageAsString();
        if (msg == "spine:prev" || msg == "spine:next")
            SpineNavigationRequested?.Invoke(msg == "spine:prev" ? "prev" : "next");
    }

    /// <summary>プレビュー用フォントを変更する。</summary>
    public async void SetFont(string fontFamily)
    {
        _currentFont = fontFamily;
        if (_isInitialized && _coreWebView != null)
            await ApplyFontAsync(fontFamily);
    }

    /// <summary>WebView2 内を横スクロールする。正=右、負=左。</summary>
    public async void ScrollHorizontal(int delta)
    {
        if (_isInitialized && _coreWebView != null)
            await _coreWebView.ExecuteScriptAsync($"document.documentElement.scrollLeft += {delta};");
    }

    /// <summary>WebView2 内をページ幅分横スクロールする。正=右、負=左。</summary>
    public async void ScrollPage(int direction)
    {
        if (_isInitialized && _coreWebView != null)
            await _coreWebView.ExecuteScriptAsync(
                $"document.documentElement.scrollLeft += document.documentElement.clientWidth * {direction};");
    }

    private async Task ApplyFontAsync(string fontFamily)
    {
        if (_coreWebView == null) return;
        var escaped = fontFamily.Replace("'", "\\'");
        // フォント + 余白（上下3em、左右2em）を一括適用
        var script = $$"""
            (function() {
                var b = document.body;
                b.style.fontFamily = "'{{escaped}}', serif, sans-serif";
                b.style.paddingTop = "3em";
                b.style.paddingBottom = "3em";
                b.style.paddingLeft = "2em";
                b.style.paddingRight = "2em";
            })();
            """;
        await _coreWebView.ExecuteScriptAsync(script);
    }

    /// <summary>
    /// 縦書きEPUB表示補助スクリプト。
    /// - ホイールスクロール → 横スクロールに変換
    /// - 左右矢印キー → 横スクロール
    /// - PageUp/PageDown → ページ幅分スクロール
    /// </summary>
    private const string ScrollHelperScript = """
        (function() {
            if (window.__scrollHelperInstalled) return;
            window.__scrollHelperInstalled = true;

            var root = document.documentElement;
            var isVertical = getComputedStyle(root).writingMode.indexOf('vertical') >= 0;

            // フォント修正: @付き縦書きフォント名を通常のフォント名に置換
            // WebView2 では writing-mode: vertical-rl で自動的に縦書きグリフを使うため @不要
            var sheets = document.styleSheets;
            for (var i = 0; i < sheets.length; i++) {
                try {
                    var rules = sheets[i].cssRules;
                    for (var j = 0; j < rules.length; j++) {
                        var r = rules[j];
                        if (r.style && r.style.fontFamily) {
                            r.style.fontFamily = r.style.fontFamily.replace(/@/g, '');
                        }
                        // @font-face ルールも修正
                        if (r instanceof CSSFontFaceRule && r.style.fontFamily) {
                            r.style.fontFamily = r.style.fontFamily.replace(/@/g, '');
                        }
                    }
                } catch(e) { /* cross-origin stylesheet */ }
            }

            // ホイール → 横スクロール（縦書き時）
            document.addEventListener('wheel', function(e) {
                if (!isVertical) return;
                if (e.deltaY !== 0) {
                    // 縦書きでは右→左に読むので deltaY > 0（下スクロール）→ 左にスクロール
                    root.scrollLeft -= e.deltaY;
                    e.preventDefault();
                }
            }, { passive: false });

            // キーボード操作
            document.addEventListener('keydown', function(e) {
                if (!isVertical) return;
                var pageW = root.clientWidth;
                var step = 80;

                switch(e.key) {
                    case 'ArrowLeft':
                        root.scrollLeft -= step;
                        e.preventDefault();
                        break;
                    case 'ArrowRight':
                        root.scrollLeft += step;
                        e.preventDefault();
                        break;
                    case 'PageUp':
                        root.scrollLeft += pageW;
                        e.preventDefault();
                        break;
                    case 'PageDown':
                        root.scrollLeft -= pageW;
                        e.preventDefault();
                        break;
                    case 'ArrowUp':
                        window.chrome.webview.postMessage('spine:prev');
                        e.preventDefault();
                        break;
                    case 'ArrowDown':
                        window.chrome.webview.postMessage('spine:next');
                        e.preventDefault();
                        break;
                    case 'Home':
                        root.scrollLeft = 0;
                        e.preventDefault();
                        break;
                    case 'End':
                        root.scrollLeft = root.scrollWidth;
                        e.preventDefault();
                        break;
                }
            });
        })();
        """;

    /// <summary>展開済み EPUB の CSS ファイルを上書きして WebView2 をリロードする。</summary>
    public async void InjectCss(string cssText, string cssFilePath)
    {
        if (!_isInitialized || _coreWebView == null) return;

        // CSS ファイルを直接上書き（展開済み一時ディレクトリ内）
        try
        {
            File.WriteAllText(cssFilePath, cssText);
        }
        catch { return; }

        // ページをリロード
        _coreWebView.Reload();
    }

    /// <summary>指定 URI にナビゲートする。</summary>
    public void Navigate(string uri)
    {
        if (_isInitialized && _coreWebView != null)
            _coreWebView.Navigate(uri);
        else
        {
            _pendingUri = uri;
            _pendingHtml = null;
        }
    }

    /// <summary>指定 URI にナビゲートする。</summary>
    public void Navigate(Uri uri) => Navigate(uri.AbsoluteUri);

    /// <summary>HTML 文字列を直接表示する。</summary>
    public void NavigateToString(string html)
    {
        if (_isInitialized && _coreWebView != null)
            _coreWebView.NavigateToString(html);
        else
        {
            _pendingHtml = html;
            _pendingUri = null;
        }
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        CleanUp();
    }

    private void CleanUp()
    {
        _isInitialized = false;
        _coreWebView = null;

        if (_controller != null)
        {
            try { _controller.Close(); } catch { }
            _controller = null;
        }

        if (_hwndHost != IntPtr.Zero)
        {
            DestroyWindow(_hwndHost);
            _hwndHost = IntPtr.Zero;
        }
    }

    public void Dispose() => CleanUp();

    // ───── Win32 interop ──────────────────────────────────────────────

    private static IntPtr CreateHostWindow(IntPtr parentHwnd)
    {
        const int WS_CHILD = 0x40000000;
        const int WS_VISIBLE = 0x10000000;
        const int WS_CLIPCHILDREN = 0x02000000;

        return CreateWindowEx(
            0,
            "Static",
            "",
            WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN,
            0, 0, 1, 1,
            parentHwnd,
            IntPtr.Zero,
            GetModuleHandle(null),
            IntPtr.Zero);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(
        int dwExStyle, string lpClassName, string lpWindowName, int dwStyle,
        int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
