using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

namespace CodexTokenOverlay;

internal sealed partial class UsageCardContext : ApplicationContext
{
    private static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUsageCardLocal");
    private readonly UsageCardForm _form = new();
    private readonly QuotaClient _quota = new();
    private readonly CodexIpcActiveThreadMonitor _route = new();
    private readonly TokenLogMonitor _tokens;
    private readonly SessionVitalsMonitor _vitals = new();
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 2000 };
    private readonly System.Windows.Forms.Timer _eventTimer = new() { Interval = 16 };
    private readonly System.Windows.Forms.Timer _fade = new() { Interval = 16 };
    private UsageCardSettings _settings;
    private readonly System.Windows.Forms.Timer _expand = new() { Interval = 16 };
    private double _expandFrom, _expandTarget;
    private int _expandStart;
    private readonly System.Windows.Forms.Timer _hover = new() { Interval = 100 };
    private DateTimeOffset _hoverSince;
    private DateTimeOffset _leaveSince;
    private bool _settingsOpen, _menuOpen, _livePreview;
    private int _railWidthPx;
    private int _clientLeft;
    private IntPtr _railHost;
    private int _themeStart;
    private UsageCardSettings ActiveSettings => _form.Settings;
    private UsageSettingsDialog? _settingsDialog;
    private readonly EventWaitHandle _settingsSignal = new(false, EventResetMode.AutoReset, @"Local\CodexUsageCardSettings");
    private RegisteredWaitHandle? _settingsWait;
    private readonly WinEvent _callback;
    private readonly List<IntPtr> _hooks = new();
    private CodexWindowTarget? _target;
    private DateTimeOffset _nextQuota;
    private DateTimeOffset _hiddenSince = DateTimeOffset.Now;
    private long _minute;
    private uint _dpi;
    private Action? _translateMenu;
    private Point _position;
    private int _viewportTop,_viewportBottom;
    private bool _busy, _hidden, _disposed;
    private bool _quotaBusy, _sessionRefreshPending;
    private readonly System.Windows.Forms.Timer _sessionTimer = new() { Interval = 500 };
    private int _eventPending;
    private int _fadeStart;
    private byte _alpha = 255;
    private readonly System.Windows.Forms.Timer _focus = new() { Interval = 150 };
    private IntPtr _focusWindow;
    private uint _hostPid;
    private bool _focusAllowed, _codexActive;

    public UsageCardContext(string sessionRoot)
    {
        Directory.CreateDirectory(DataDir);
        try { _settings = JsonSerializer.Deserialize<UsageCardSettings>(File.ReadAllText(Path.Combine(DataDir, "settings.json"))) ?? new(); }
        catch (Exception ex) when (ex is IOException or JsonException) { _settings = new(); }
        if (!double.IsFinite(_settings.BottomDip)) _settings = new();
        _settings.BottomDip = Math.Clamp(_settings.BottomDip, 0, 2000);
        _settings.ExpandAlignments();
        if (_settings.Validate() is not null) _settings = new();
        var configPath = Path.Combine(DataDir, "settings.json");
        if (File.Exists(configPath))
        {
            try
            {
                using var previous = JsonDocument.Parse(File.ReadAllText(configPath));
                if (!previous.RootElement.TryGetProperty("Version", out var version) || !version.TryGetInt32(out var number) || number < 3)
                {
                    File.Copy(configPath, Path.Combine(DataDir, "settings-before-v03.json"), true);
                    _settings = new();
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException) { }
        }
        if (_settings.Version < 4)
        {
            File.Copy(configPath, Path.Combine(DataDir, "settings-before-v04.json"), true);
            _settings.Version = 4;
            foreach (var module in _settings.Modules.Where(m => m.Metric == MetricKind.聊天Token)) module.Style = GaugeStyle.数值;
        }
        if (_settings.Version < 5) { File.Copy(configPath, Path.Combine(DataDir, "settings-before-v05.json"), true); _settings.Version = 5; _settings.Reflow(); }
        if (_settings.Version < 6)
        {
            File.Copy(configPath, Path.Combine(DataDir, "settings-before-v06.json"), true);
            _settings.Version = 6; _settings.AllowDrag = false; _settings.BoldText = true;
            _settings.Modules = _settings.Modules.GroupBy(m => m.Metric).Select(group => group.FirstOrDefault(m => m.Enabled) ?? group.First()).ToList();
        }
        if (_settings.Version < 7)
        {
            File.Copy(configPath, Path.Combine(DataDir, "settings-before-v07.json"), true);
            _settings.Version = 7; _settings.Opacity = 0; _settings.AutoText = true; _settings.RingDiameter = 24; _settings.HorizontalOffset = 0; _settings.SidebarWidth = 56; _settings.BottomDip = 110;
        }
        if (_settings.Version < 8) { File.Copy(configPath, Path.Combine(DataDir, "settings-before-v08.json"), true); _settings.Version = 8; }
        if (_settings.Version < 9) { File.Copy(configPath, Path.Combine(DataDir, "settings-before-v09.json"), true); _settings.Version = 9; }
        if (_settings.Version < 10) { File.Copy(configPath, Path.Combine(DataDir, "settings-before-v10.json"), true); _settings.Version = 10; _settings.ApplyDefaultLayout(); }
        if (_settings.Version < 11) { File.Copy(configPath, Path.Combine(DataDir, "settings-before-v11.json"), true); _settings.Version = 11; _settings.RingDiameter = 36; _settings.LivePreview = true; _settings.ExpandMs = 150; }
        if (_settings.Version < 12) { File.Copy(configPath, Path.Combine(DataDir, "settings-before-v12.json"), true); _settings.Version = 12; _settings.ThemeId = _settings.Material ? 5 : _settings.AccentHex == "#EAB879" ? 2 : _settings.AccentHex == "#77DCBC" ? 3 : _settings.AccentHex == "#4268BD" ? 4 : 1; }
        if (_settings.Version < 13) { File.Copy(configPath, Path.Combine(DataDir, "settings-before-v13.json"), true); _settings.Version = 13; var fourth = _settings.Modules.FirstOrDefault(m => m.Enabled && m.Row == 3 && m.Column == 0); var five = _settings.Modules.FirstOrDefault(m => m.Metric == MetricKind.五小时剩余); if (_settings.Rows >= 4 && (fourth is null || fourth.Metric == MetricKind.平均输出速度) && five is { Enabled: false }) { if (fourth is not null) fourth.Enabled = false; five.Enabled = true; five.Unplaced = false; five.Row = 3; five.Column = 0; five.Style = GaugeStyle.环形; five.RingValue = RingValuePosition.环内; } }
        foreach (var metric in Enum.GetValues<MetricKind>())
            if (!_settings.Modules.Any(m => m.Metric == metric)) _settings.Modules.Add(new() { Metric = metric, Enabled = false, Style = GaugeStyle.数值, Reference = metric == MetricKind.平均输出速度 ? 100 : metric == MetricKind.首Token等待 ? 10 : 1_000_000 });
        if (_settings.Version < 14) { File.Copy(configPath, Path.Combine(DataDir, "settings-before-v14.json"), true); _settings.Version = 14; }
        UiText.Language = _settings.Language;
        Save();
        _form.Settings = _settings; _form.Details = false;
        _tokens = new TokenLogMonitor(sessionRoot);
        _ = _form.Handle;
        var menu = new ContextMenuStrip();
        menu.Items.Add("立即刷新额度", null, (_, _) => { _nextQuota = DateTimeOffset.MinValue; PollData(); });
        menu.Items.Add("控制面板…", null, (_, _) => _form.BeginInvoke(() => OpenSettings()));
        menu.Opening += (_, _) => { _menuOpen = true; _expand.Stop(); _fade.Stop(); };
        menu.Closed += (_, _) => { _menuOpen = false; _hoverSince = _leaveSince = default; _alpha = 255; if (!_settingsOpen && Math.Abs(_form.Expansion - _expandTarget) > .001) AnimateDetails(_expandTarget > 0); };
        _settingsWait = ThreadPool.RegisterWaitForSingleObject(_settingsSignal, (_, _) => { if (!_disposed) try { _form.BeginInvoke(() => OpenSettings()); } catch (InvalidOperationException) { } }, null, Timeout.Infinite, false);

        menu.Items.Add("重置到窄栏", null, (_, _) => { _settings.HorizontalOffset = 0; _settings.BottomDip = 110; Save(); Synchronize(); });
        menu.Items.Add("暂时隐藏 / 恢复", null, (_, _) => { _hidden = !_hidden; Synchronize(); });
        var startup = new ToolStripMenuItem("随 Windows 启动（等待 Codex）") { CheckOnClick = true, Checked = IsStartupEnabled() };
        startup.CheckedChanged += (_, _) =>
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (startup.Checked) key.SetValue("CodexUsageCardLocal", $"\"{Environment.ProcessPath}\"");
            else key.DeleteValue("CodexUsageCardLocal", false);
        };
        menu.Items.Add(startup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitThread());
        _tray = new NotifyIcon { Icon = SystemIcons.Information, Text = "Codex 剩余额度 · 本地修改版", Visible = true, ContextMenuStrip = menu };
        _form.ContextMenuStrip = menu;
        void TranslateMenu(){foreach(ToolStripItem item in menu.Items){if(item.Tag is not string original){original=item.Text??"";item.Tag=original;}item.Text=UiText.T(original);}_tray.Text=UiText.T("Codex 剩余额度 · 本地修改版");}
        _translateMenu=TranslateMenu; TranslateMenu();
        _form.PageChanged += () => { _form.Rebuild(_dpi == 0 ? 96 : _dpi); Synchronize(); if (_form.Visible) _form.Present(_position, _alpha); };

        _form.ConstrainDrag = point => _target is null ? _position : new Point(_position.X, Math.Clamp(point.Y, _viewportTop, Math.Max(_viewportTop, _viewportBottom - _form.Height)));
        _form.DragStarted += () => { _fade.Stop(); _expand.Stop(); _alpha = 255; _form.Present(_position, 255); };
        _form.DragMoved += point => _position = point;
        _form.DragFinished += point =>
        {
            if (_target is null) return;
            var bounds = _target.HostWindow.ExtendedFrameBounds;
            var factor = _target.HostWindow.Dpi / 96d;

            _settings.BottomDip = (bounds.Bottom - point.Y) / factor - _form.HeightDip;
            Save(); Synchronize();
        };
        _timer.Tick += (_, _) =>
        {
            Synchronize();
            if (_form.Visible) PollData();
            else if (DateTimeOffset.Now - _hiddenSince > TimeSpan.FromSeconds(15) && !_quotaBusy) _quota.Stop();
            var minute = DateTimeOffset.Now.ToUnixTimeSeconds() / 60;
            if (minute != _minute) { _minute = minute; Redraw(); }
        };
        _eventTimer.Tick += (_, _) => { _eventTimer.Stop(); Synchronize(); if (_form.Visible) PollData(); };
        _fade.Tick += (_, _) =>
        {
            var progress = Math.Clamp((Environment.TickCount - _fadeStart) / (double)_settings.FadeMs, 0, 1);
            _alpha = (byte)Math.Round(255 * (progress * progress * (3 - 2 * progress)));
            if (_form.Visible) _form.Present(_position, _alpha);
            if (progress >= 1) _fade.Stop();
        };
        _callback = HandleEvent;
        // Out-of-process hooks, no DLL injection. Foreground is global; other notifications filtered by HWND.
        foreach (var range in new[] { (3u, 3u), (0x16u, 0x17u), (0x8001u, 0x8003u), (0x800Bu, 0x800Bu) })
        {
            var hook = SetWinEventHook(range.Item1, range.Item2, IntPtr.Zero, _callback, 0, 0, 2);
            if (hook != IntPtr.Zero) _hooks.Add(hook);
        }
        _hover.Tick += (_, _) =>
{
    if (!_form.Visible || _form.Capture || _settingsOpen || _menuOpen || !_settings.HoverDetails) return;
    var inside = _form.Bounds.Contains(Cursor.Position);
    if (inside)
    {
        _leaveSince = default;
        if (_hoverSince == default) _hoverSince = DateTimeOffset.Now;
        if (_expandTarget < 1 && DateTimeOffset.Now - _hoverSince > TimeSpan.FromMilliseconds(320)) AnimateDetails(true);
    }
    else
    {
        _hoverSince = default;
        if (_leaveSince == default) _leaveSince = DateTimeOffset.Now;
        if (_expandTarget > 0 && DateTimeOffset.Now - _leaveSince > TimeSpan.FromMilliseconds(250)) AnimateDetails(false);
    }
};
        _expand.Tick += (_, _) =>
{
    var progress = Math.Clamp((Environment.TickCount - _expandStart) / (double)_settings.ExpandMs, 0, 1);
    var eased = progress * progress * (3 - 2 * progress);
    _form.Expansion = _expandFrom + (_expandTarget - _expandFrom) * eased;
    OnDetails(); if (progress >= 1) _expand.Stop();
};
        _focus.Tick += (_, _) => { if (_disposed) return; if (!CheckFocus()) Hide(true); else if (_form.Visible && !_form.Capture && !_menuOpen && Environment.TickCount - _themeStart >= 150) { _themeStart = Environment.TickCount; RefreshTheme(); } }; _focus.Start();
        _route.Changed += RouteChanged;
        _sessionTimer.Tick += (_, _) => PollData();
        _sessionTimer.Start();
        _hover.Start(); _timer.Start(); Synchronize(); PollData();
    }
    private void RouteChanged()
    {
        if (_disposed) return;
        try { _form.BeginInvoke(() =>
        {
            if (_disposed) return;
            _sessionRefreshPending = true;
            // Clear immediately: the previous conversation must not remain on screen.
            _form.Tokens = null;
            _form.Vitals = new("未知", "", "等待数据", null, null, null);
            Redraw();
            PollData();
        }); }
        catch (InvalidOperationException) { }
    }
    private void HandleEvent(IntPtr hook, uint ev, IntPtr hwnd, int objectId, int childId, uint thread, uint time)
    {
        if (_disposed || hwnd == _form.Handle || (ev != 3 && (hwnd != _target?.HostWindow.Handle || objectId != 0))) return;
        if (ev == 3) { try { _form.BeginInvoke(() => { if (!CheckFocus()) Hide(true); else Synchronize(); }); } catch (InvalidOperationException) { } return; }
        if (Interlocked.Exchange(ref _eventPending, 1) != 0) return;
        try { _form.BeginInvoke(() => { Interlocked.Exchange(ref _eventPending, 0); if (!_disposed) _eventTimer.Start(); }); }
        catch (InvalidOperationException) { Interlocked.Exchange(ref _eventPending, 0); }
    }
    private bool CheckFocus()
    {
        var foreground = GetForegroundWindow();
        var hostOkay = _target is not null && IsWindowVisible(_target.HostWindow.Handle) && !IsIconic(_target.HostWindow.Handle);
        if (foreground == IntPtr.Zero) return _focusAllowed = _codexActive && hostOkay;
        if (foreground == _focusWindow && _target is not null) return _focusAllowed && hostOkay;
        _focusWindow = foreground;
        GetWindowThreadProcessId(foreground, out var pid);
        if (_target is not null && pid == _hostPid)
        { _codexActive = true; return _focusAllowed = hostOkay; }
        if (pid == Environment.ProcessId) return _focusAllowed = _codexActive && hostOkay;
        if (IsShellSurface(foreground, pid)) return _focusAllowed = _codexActive && hostOkay;
        if (CodexWindowLocator.TryGetForegroundCodexTarget(out var target))
        { _target = target; GetWindowThreadProcessId(target.HostWindow.Handle, out _hostPid); _codexActive = true; return _focusAllowed = true; }
        _codexActive = false; return _focusAllowed = false;
    }
    private static bool IsShellSurface(IntPtr hwnd, uint pid)
    {
        var name = new System.Text.StringBuilder(128); GetClassName(hwnd, name, name.Capacity);
        if (name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or "NotifyIconOverflowWindow") return true;
        if (name.ToString() is not ("Windows.UI.Core.CoreWindow" or "XamlExplorerHostIslandWindow" or "#32768")) return false;
        try { using var process = Process.GetProcessById((int)pid); return process.ProcessName is "StartMenuExperienceHost" or "ShellExperienceHost" or "SearchApp" or "SearchHost" or "explorer"; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }
    private void Synchronize()
    {
        if (_disposed) return;
        if (!CheckFocus()) { Hide(true); return; }
        if (_form.Capture || _menuOpen) return;
        if (_hidden) { Hide(true); return; }
        if (_target is null || !CodexWindowLocator.TryRefreshKnownCodexTarget(_target, out var target)) { Hide(true); return; }
        _target = target;
        var host = _target.HostWindow;
        var bounds = host.ExtendedFrameBounds;
        var scale = host.Dpi / 96d;
        var origin = new Point(0, 0); ClientToScreen(host.Handle, ref origin); _clientLeft = origin.X;
        if(!GetClientRect(host.Handle,out var client)){Hide();return;}
        if (_railWidthPx == 0 || _dpi != host.Dpi || _railHost != host.Handle) { _railWidthPx = MeasureRail(host.Handle, host.Dpi); _railHost = host.Handle; }
        var viewport=AdaptiveGaugeLayout.RailViewport(client.Bottom,scale);
        var workArea=Screen.FromHandle(host.Handle).WorkingArea;
        _viewportTop=Math.Max(origin.Y+viewport.Top,workArea.Top);
        _viewportBottom=Math.Min(origin.Y+viewport.Bottom,workArea.Bottom);
        var available=(int)Math.Floor((_viewportBottom-_viewportTop)/scale);
        if(available<100){Hide(true);return;}
        var adapted=_form.ConfigureViewport(available,(int)Math.Round(_railWidthPx/scale));
        var w=(int)Math.Round(_form.WidthDip*scale);var h=(int)Math.Round(_form.HeightDip*scale);
        if(bounds.Width<w+30*scale){Hide();return;}
        var compactPx = (int)Math.Round((_form.WidthDip-(int)Math.Round(266*_form.Expansion)) * scale);
        var position = new Point(
            PlaceHorizontally(_clientLeft, _railWidthPx, compactPx, ActiveSettings.HorizontalOffset),
            Math.Clamp(bounds.Bottom - h - (int)Math.Round(ActiveSettings.BottomDip * scale), _viewportTop, Math.Max(_viewportTop,_viewportBottom-h)));
        var light = ReadHostLight(host.Handle, host.Dpi, _form.Light);
        var rebuild = adapted || _dpi != host.Dpi || light != _form.Light;
        _form.Light = light;
        if (rebuild) { _dpi = host.Dpi; _form.Rebuild(_dpi); }
        var moved = position != _position;
        _position = position;
        if (!_form.Visible)
        {
            _alpha = 0;
            _form.Rebuild(_dpi);
            _form.Present(position, 0); // Fully rendered transparent frame before native Show; no white flash.
            _form.Show();
            ShowWindow(_form.Handle, 4);
            _fadeStart = Environment.TickCount; _fade.Start();
        }
        else if (moved || rebuild) _form.Present(position, _alpha);
    }
    private void Hide(bool force = false)
    {
        if (!_form.Visible || (!force && (_menuOpen || _settingsOpen))) return;
        _form.Capture = false; _fade.Stop(); _expand.Stop(); _expandTarget = 0; _form.Hide(); _form.Details = false; _hoverSince = _leaveSince = default; _hiddenSince = DateTimeOffset.Now;
    }
    private void AnimateDetails(bool expanded)
    {
        _expandFrom = _form.Expansion; _expandTarget = expanded ? 1 : 0;
        _expandStart = Environment.TickCount; _expand.Start();
    }
    private void OnDetails() { _form.Rebuild(_dpi == 0 ? 96 : _dpi); Synchronize(); if (_form.Visible) _form.Present(_position, _alpha); }
    private void Save() => File.WriteAllText(Path.Combine(DataDir, "settings.json"), JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
    private void Redraw()
    {
        if (!_form.Visible || _form.Capture || _menuOpen || (_settingsOpen && !_livePreview)) return;
        _form.Rebuild(_dpi); _form.Present(_position, _alpha);
    }
    private async void PollData()
    {
        if (_disposed || !_form.Visible) return;
        PollQuota();
        if (_busy) { _sessionRefreshPending = true; return; }
        _busy = true;
        _sessionRefreshPending = false;
        try
        {
            var route = _route.GetStatus();
            _tokens.PreferredThreadId = route.ThreadId;
            var candidates = _route.GetLocalThreadIds();
            _tokens.AllowedThreadIds = candidates.Length == 0 ? null : candidates.ToHashSet(StringComparer.OrdinalIgnoreCase);
            TokenSnapshot? token;
            try { token = await Task.Run(() => _tokens.Poll()); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { token = null; }
            if (_disposed) return;
            if (_route.GetStatus().Version != route.Version) { _sessionRefreshPending = true; return; }
            SessionVitals vitals;
            try { vitals = await Task.Run(() => _vitals.Poll(token?.LogPath)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { vitals = new("未知", "", "数据暂不可用", null, null, null); }
            var sessionSource = route.ThreadId is not null ? "IPC 订阅会话" : "最近活动会话（自动匹配）";
            if (_disposed) return;
            if (_route.GetStatus().Version != route.Version) { _sessionRefreshPending = true; return; }
            if (_form.Tokens != token || _form.Vitals != vitals || _form.SessionSource != sessionSource)
            { _form.Tokens = token; _form.Vitals = vitals; _form.SessionSource = sessionSource; Redraw(); }
        }
        finally { _busy = false; if (_sessionRefreshPending && !_disposed) PollData(); }
    }
    private async void PollQuota()
    {
        if (_quotaBusy || _disposed || DateTimeOffset.Now < _nextQuota) return;
        _quotaBusy = true;
        _nextQuota = DateTimeOffset.Now.AddSeconds(60);
        try
        {
            try { var quota = await _quota.ReadAsync(); if (_disposed) return; _form.Quota = quota; _form.QuotaFailed = false; _form.Status = ""; }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or JsonException or System.ComponentModel.Win32Exception)
            {
                if (_disposed) return;
                _form.QuotaFailed = true; _form.Status = "额度不可用 · 检查登录 / 网络";
            }
            if (!_disposed) Redraw();
        }
        finally { _quotaBusy = false; }
    }
    private void OpenSettings()
    {
        if (_disposed) return;
        if (_settingsDialog is { IsDisposed: false }) { _settingsDialog.Show(); ShowWindow(_settingsDialog.Handle, 5); _settingsDialog.Activate(); return; }
        _settingsOpen = true; _form.DragLocked = true;
        _expand.Stop();
        try
        {
            _settings.AvailableHeightDip = _target is null ? 980 : Math.Max(48, (int)(_target.HostWindow.ExtendedFrameBounds.Height / (_target.HostWindow.Dpi / 96d)) - 420);
            var dialog = new UsageSettingsDialog(_settings) { TopMost = true };
            _settingsDialog = dialog; dialog.HostLight = _form.Light;
            dialog.LanguageChanged += () => { _translateMenu?.Invoke(); _form.Rebuild(_dpi==0?96:_dpi); if(_form.Visible)_form.Present(_position,_alpha); };
            dialog.PreviewChanged += preview =>
            {
                _livePreview = preview is not null; _form.Settings = preview ?? _settings; _form.Rebuild(_dpi == 0 ? 96 : _dpi);
                Synchronize(); if (_form.Visible) _form.Present(_position, _alpha);
                if (dialog.IsHandleCreated) SetWindowPos(dialog.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x1 | 0x2 | 0x10);
            };
            dialog.FormClosed += (_, _) =>
            {
                if (dialog.DialogResult == DialogResult.OK)
                {
                    _settings = dialog.Result; _form.Settings = _settings; Save();
                    _form.Rebuild(_dpi == 0 ? 96 : _dpi);
                }
                UiText.Language=_settings.Language;_translateMenu?.Invoke();
                _form.Settings = _settings; _form.Rebuild(_dpi == 0 ? 96 : _dpi); _livePreview = false;
                _settingsDialog = null; _settingsOpen = false; _form.DragLocked = false; _hoverSince = _leaveSince = default;
                dialog.Dispose(); if (!_disposed) Synchronize();
            };
            dialog.Show(); ShowWindow(dialog.Handle, 5); dialog.Activate();
            if (dialog.Result.LivePreview) { _livePreview = true; _form.Settings = dialog.Result; }
        }
        catch (Exception ex)
        {
            _settingsOpen = false; _form.DragLocked = false; _settingsDialog?.Dispose(); _settingsDialog = null;
            File.WriteAllText(Path.Combine(DataDir, "settings-error.log"), ex.ToString());
            MessageBox.Show("控制面板打开失败，诊断已保存至 CodexUsageCardLocal/settings-error.log。", "Codex Usage Card");
        }
    }
    private void RefreshTheme()
    {
        if (_target is null) return; var light = ReadHostLight(_target.HostWindow.Handle, _dpi == 0 ? 96 : _dpi, _form.Light);
        if (light == _form.Light) return; _form.Light = light;
        if (_settingsDialog is { IsDisposed: false } dialog) dialog.HostLight = light;
        _form.Rebuild(_dpi == 0 ? 96 : _dpi); if (_form.Visible) _form.Present(_position, _alpha);
    }
    internal static int PlaceHorizontally(int left, int rail, int compact, int offset) => left + (int)Math.Round(Math.Max(0, rail - compact) * (Math.Clamp(offset, -100, 100) + 100) / 200d, MidpointRounding.AwayFromZero);
    // Find a vertical rail edge agreed upon by several empty-row scans. Fall back to 56 DIP if the surfaces have identical colors.
    private static int MeasureRail(IntPtr hwnd, uint dpi)
    {
        var fallback = (int)Math.Round(56 * dpi / 96d); if (!GetClientRect(hwnd, out var rect) || rect.Bottom < 200) return fallback;
        var dc = GetDC(hwnd); if (dc == IntPtr.Zero) return fallback;
        try
        {
            var edges = new List<int>();
            foreach (var fraction in new[] { .32, .44, .56, .68, .8 })
            {
                var y = (int)(rect.Bottom * fraction); var baseline = GetPixel(dc, (int)Math.Round(24 * dpi / 96d), y); if (baseline == 0xffffffff) continue;
                for (var x = (int)Math.Round(40 * dpi / 96d); x <= (int)Math.Round(72 * dpi / 96d); x++)
                {
                    var color = GetPixel(dc, x, y); if (color == 0xffffffff) break;
                    static int Difference(uint a, uint b) => Math.Max(Math.Abs((int)(a & 255) - (int)(b & 255)), Math.Max(Math.Abs((int)((a >> 8) & 255) - (int)((b >> 8) & 255)), Math.Abs((int)((a >> 16) & 255) - (int)((b >> 16) & 255))));
                    if (Difference(color, baseline) > 0) { edges.Add(x); break; }
                }
            }
            var consensus = edges.GroupBy(x => x).OrderByDescending(g => g.Count()).FirstOrDefault(); return consensus is not null && consensus.Count() >= 3 ? consensus.Key : fallback;
        }
        finally { ReleaseDC(hwnd, dc); }
    }
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd, ref Point point);
    // Read three pixels of the host rail's outer edge; never sample our overlay or infer from our background.
    private static bool ReadHostLight(IntPtr hwnd, uint dpi, bool fallback)
    {
        if (!GetClientRect(hwnd, out var rect) || rect.Bottom < 100) return fallback;
        var dc = GetDC(hwnd); if (dc == IntPtr.Zero) return fallback;
        try
        {
            var values = new List<double>();
            foreach (var fraction in new[] { .35, .55, .75 })
            {
                var color = GetPixel(dc, (int)Math.Round(55 * dpi / 96d), (int)(rect.Bottom * fraction));
                if (color == 0xffffffff) continue;
                values.Add(.2126 * (color & 255) + .7152 * ((color >> 8) & 255) + .0722 * ((color >> 16) & 255));
            }
            if (values.Count == 0) return fallback; values.Sort(); return values[values.Count / 2] > 140;
        }
        finally { ReleaseDC(hwnd, dc); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct ClientRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out ClientRect rect);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern uint GetPixel(IntPtr dc, int x, int y);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue("CodexUsageCardLocal") is string;
    }
    protected override void ExitThreadCore()
    {
        _route.Changed -= RouteChanged;
        _sessionTimer.Dispose();
        _disposed = true; _settingsWait?.Unregister(null); _settingsSignal.Dispose(); _settingsDialog?.Close();
        foreach (var hook in _hooks) UnhookWinEvent(hook);
        _timer.Dispose(); _eventTimer.Dispose(); _fade.Dispose(); _hover.Dispose(); _expand.Dispose(); _focus.Dispose();
        _quota.Dispose(); _route.Dispose(); _tokens.Dispose();
        _tray.Visible = false; _tray.Dispose(); _form.Dispose();
        base.ExitThreadCore();
    }
    private delegate void WinEvent(IntPtr hook, uint ev, IntPtr hwnd, int objectId, int childId, uint thread, uint time);
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, WinEvent callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder name, int max);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint process);
}
