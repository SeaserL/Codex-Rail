using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
namespace CodexTokenOverlay;
internal static class StartupUpdateSuite
{
    internal static bool Run(string[] args)
    {
        if (args.Length != 2 || args[0] != "--startup-update") return false;
        Directory.CreateDirectory(args[1]);
        if (!AppPaths.Development || !AssetStore.Root.StartsWith(AppPaths.DataDirectory) || !AppPaths.InstanceSuffix.StartsWith("-dev-")) throw new Exception("development isolation failed");
        string Json(string tag, bool preview = false, string host = "github.com") => JsonSerializer.Serialize(new { tag_name = tag, draft = false, prerelease = preview, assets = new[] { new { name = $"CodexRail-{tag}-win-x64.zip", browser_download_url = $"https://{host}/SeaserL/Codex-Rail/releases/download/{tag}/CodexRail-{tag}-win-x64.zip", digest = "sha256:" + new string('0', 64) } } });
        if (ReleaseUpdater.Parse(Json("v0.14.7"), new Version(0, 14, 7, 0)) != null) throw new Exception("same release offered");
        if (ReleaseUpdater.Parse(Json("v0.14.6"), new Version(0, 14, 7)) != null) throw new Exception("downgrade offered");
        if (ReleaseUpdater.Parse(Json("v0.15.0", true), new Version(0, 14, 7)) != null) throw new Exception("preview offered");
        if (ReleaseUpdater.Parse(Json("v0.15.0"), new Version(0, 14, 7))?.Version != new Version(0, 15, 0)) throw new Exception("new release missed");
        if (ReleaseUpdater.Parse(Json("v0.15.0").Replace("/Codex-Rail/", "/Codex-Monitor/"), new Version(0, 14, 7))?.Version != new Version(0, 15, 0)) throw new Exception("renamed repository rejected");
        try { ReleaseUpdater.Parse(Json("v0.15.0", host: "invalid.test"), new Version(0, 14, 7)); throw new Exception("external asset accepted"); } catch (InvalidDataException) { }
        // Malformed/corrupt archives must not overwrite any live executable.
        var path = Path.Combine(args[1], "corrupt.zip"); File.WriteAllText(path, "invalid");
        var update = new ReleaseUpdate(new Version(0, 15, 0), "v0.15.0", new Uri("https://github.com/"), "sha256:" + new string('0', 64));
        try { ReleaseUpdater.VerifyArchive(path, update, Path.Combine(args[1], "staging"), false); throw new Exception("corrupt package accepted"); } catch (InvalidDataException) { }
        var bundle = Path.Combine(Directory.GetCurrentDirectory(), "dist", "CodexRail-v0.14.7-win-x64.zip");
        if (File.Exists(bundle))
        {
            using var stream = File.OpenRead(bundle);
            var valid = new ReleaseUpdate(new Version(0, 14, 7), "v0.14.7", new Uri("https://github.com/"), "sha256:" + Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant());
            ReleaseUpdater.VerifyArchive(bundle, valid, Path.Combine(args[1], "verified-small"), false);
            ReleaseUpdater.VerifyArchive(bundle, valid, Path.Combine(args[1], "verified-standalone"), true);
        }
        UsageCardContext.StartupSignalRegression(args[1]);
        Console.WriteLine("PASS: development isolation, release ordering/host/digest and instance restore signal."); return true;
    }
}
internal sealed partial class UsageCardContext
{
    internal static void StartupSignalRegression(string root)
    {
        using var context = new UsageCardContext(Path.Combine(root, "missing-sessions"));
        context._hidden = true;
        using var signal = EventWaitHandle.OpenExisting(AppPaths.Name("CodexUsageCardShow")); signal.Set();
        var watch = Stopwatch.StartNew();
        while (context._hidden && watch.ElapsedMilliseconds < 2000) { Application.DoEvents(); Thread.Sleep(10); }
        if (context._hidden) throw new Exception("second-launch restore signal did not clear hidden state");
        context.ExitThread();
    }
}
