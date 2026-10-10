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
        AppPaths.Configure(args);
        var sessionRoot = SessionPathResolver.Resolve(args);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);


        using var singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            name: AppPaths.Name("CodexUsageCardLocal"),
            createdNew: out var createdNew);
        if (!createdNew)
        {
            if (EventWaitHandle.TryOpenExisting(AppPaths.Name(args.Contains("--settings") ? "CodexUsageCardSettings" : "CodexUsageCardShow"), out var signal))
            { using (signal) signal.Set(); }
            return;
        }

        var context = new UsageCardContext(sessionRoot, args.Contains("--follow"));
        if (args.Contains("--settings"))
        { using var signal = EventWaitHandle.OpenExisting(AppPaths.Name("CodexUsageCardSettings")); signal.Set(); }
        Application.Run(context);
        GC.KeepAlive(singleInstanceMutex);
    }
}
