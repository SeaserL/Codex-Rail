using System.Runtime.InteropServices;

namespace CodexTokenOverlay;

// A nonactivating overlay cannot rely on ToolStrip's in-process mouse filter.
// Listen only while the menu is open; outside clicks always continue to their target.
internal sealed class MenuDismissController : IDisposable
{
    private readonly ContextMenuStrip _menu;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 50 };
    private readonly HookProc _mouse, _keyboard;
    private IntPtr _mouseHook, _keyboardHook, _foreground;
    private bool _closing;
    private int _generation;
    internal bool Monitoring { get; private set; }
    internal bool HooksAttached => _mouseHook != IntPtr.Zero && _keyboardHook != IntPtr.Zero;

    internal MenuDismissController(ContextMenuStrip menu)
    {
        _menu = menu;
        _mouse = MouseHook; _keyboard = KeyboardHook;
        menu.AutoClose = true;
        menu.Opened += Opened; menu.Closed += Closed;
        _timer.Tick += (_, _) =>
        {
            var foreground = GetForegroundWindow();
            GetWindowThreadProcessId(foreground, out var pid);
            ObserveForeground(foreground, pid);
        };
    }
    private void Opened(object? sender, EventArgs e)
    {
        _generation++; _closing = false; Monitoring = true; _foreground = GetForegroundWindow();
        var module = GetModuleHandle(null);
        _mouseHook = SetWindowsHookEx(14, _mouse, module, 0);
        _keyboardHook = SetWindowsHookEx(13, _keyboard, module, 0);
        _timer.Start();
    }
    private void Closed(object? sender, ToolStripDropDownClosedEventArgs e) => Stop();
    internal void ObserveMouseDown(Point screenPoint)
    {
        if (Monitoring && !Contains(_menu, screenPoint)) RequestClose(ToolStripDropDownCloseReason.AppClicked);
    }
    private static bool Contains(ToolStripDropDown menu, Point point)
    {
        if (menu.Visible && menu.Bounds.Contains(point)) return true;
        return menu.Items.OfType<ToolStripDropDownItem>().Any(item => item.HasDropDownItems && Contains(item.DropDown, point));
    }
    internal void ObserveEscape() => RequestClose(ToolStripDropDownCloseReason.Keyboard);
    internal void ObserveForeground(IntPtr window, uint pid)
    {
        if (Monitoring && window != IntPtr.Zero && window != _foreground && pid != Environment.ProcessId)
            RequestClose(ToolStripDropDownCloseReason.AppFocusChange);
    }
    private void RequestClose(ToolStripDropDownCloseReason reason)
    {
        if (!Monitoring || _closing || _menu.IsDisposed) return;
        _closing = true;
        var generation = _generation;
        _menu.BeginInvoke(() => { if (generation == _generation && !_menu.IsDisposed && _menu.Visible) _menu.Close(reason); });
    }
    private IntPtr MouseHook(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && message.ToInt64() is 0x201 or 0x204 or 0x207 or 0x20B)
        {
            var point = Marshal.PtrToStructure<NativePoint>(data);
            ObserveMouseDown(new(point.X, point.Y));
        }
        return CallNextHookEx(IntPtr.Zero, code, message, data);
    }
    private IntPtr KeyboardHook(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && Monitoring && message.ToInt64() is 0x100 or 0x104 && Marshal.ReadInt32(data) == 0x1B)
        { ObserveEscape(); return new IntPtr(1); }
        return CallNextHookEx(IntPtr.Zero, code, message, data);
    }
    private void Stop()
    {
        _generation++; Monitoring = false; _closing = false; _timer.Stop();
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
        _mouseHook = _keyboardHook = IntPtr.Zero;
    }
    public void Dispose()
    {
        Stop(); _menu.Opened -= Opened; _menu.Closed -= Closed; _timer.Dispose();
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int type, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? module);
}
