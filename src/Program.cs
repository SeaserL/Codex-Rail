using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace CodexTokenOverlay;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var sessionRoot = SessionPathResolver.Resolve(args);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);


        using var singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            name: "Local\\CodexUsageCardLocal",
            createdNew: out var createdNew);
        if (!createdNew)
        {
            if (args.Contains("--settings") && EventWaitHandle.TryOpenExisting(@"Local\CodexUsageCardSettings", out var signal))
            { using (signal) signal.Set(); }
            return;
        }

        var context = new UsageCardContext(sessionRoot);
        if (args.Contains("--settings"))
        { using var signal = EventWaitHandle.OpenExisting(@"Local\CodexUsageCardSettings"); signal.Set(); }
        Application.Run(context);
        GC.KeepAlive(singleInstanceMutex);
    }
}
