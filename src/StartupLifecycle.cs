using System.Diagnostics;
using Microsoft.Win32;
namespace CodexTokenOverlay;
internal sealed partial class UsageCardContext
{
    private readonly EventWaitHandle _showSignal = new(false, EventResetMode.AutoReset, AppPaths.Name("CodexUsageCardShow"));
    private RegisteredWaitHandle? _showWait;
    private readonly System.Windows.Forms.Timer _lifecycle = new() { Interval = 10000 };
    private bool _following;
    private static string WatcherPath => Path.Combine(AppContext.BaseDirectory, "CodexRail.Watcher.exe");
    private void InitializeStartup()
    {
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showSignal, (_, _) =>
        { if (!_disposed) try { _form.BeginInvoke(() => { _hidden = false; _focusWindow = IntPtr.Zero; Synchronize(); PollData(); }); } catch (InvalidOperationException) { } }, null, Timeout.Infinite, false);
        if (!AppPaths.Development && IsStartupEnabled() && File.Exists(WatcherPath)) RegisterStartup();
        _lifecycle.Tick += async (_, _) =>
        {
            if (_following && !HasCodexProcess()) { ExitThread(); return; }
            await CheckUpdateAsync(false);
        };
        _lifecycle.Start();
    }
    private static bool HasCodexProcess()
    {
        foreach (var name in new[] { "Codex", "ChatGPT" }) foreach (var p in Process.GetProcessesByName(name))
            using (p) try { if (name == "Codex" || (p.MainModule?.FileName.Contains(@"\WindowsApps\OpenAI.Codex_", StringComparison.OrdinalIgnoreCase) ?? false)) return true; } catch (System.ComponentModel.Win32Exception) { } catch (InvalidOperationException) { }
        return false;
    }
    private static void StartWatcher()
    {
        if (!File.Exists(WatcherPath)) return;
        var start = new ProcessStartInfo(WatcherPath) { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("--watch"); start.ArgumentList.Add(Environment.ProcessPath!); Process.Start(start)?.Dispose();
    }
    private static void StopWatcher()
    {
        if (EventWaitHandle.TryOpenExisting(@"Local\CodexRailWatcherStop", out var signal)) using (signal) signal.Set();
    }
    private bool RegisterStartup()
    {
        if (AppPaths.Development) return false;
        if (!File.Exists(WatcherPath)) { MessageBox.Show(UiText.IsEnglish ? "The watcher is missing. Extract the complete ZIP before enabling startup." : "缺少启动发现器，请完整解压 ZIP 后启用自启动。"); return false; }
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            key.SetValue("CodexUsageCardLocal", $"\"{WatcherPath}\" --watch \"{Environment.ProcessPath}\""); StartWatcher(); return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.ComponentModel.Win32Exception or IOException)
        { MessageBox.Show((UiText.IsEnglish ? "Startup registration failed: " : "自启动登记失败：") + ex.Message); return false; }
    }
}
