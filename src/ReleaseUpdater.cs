using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
namespace CodexTokenOverlay;
internal sealed record ReleaseUpdate(Version Version, string Tag, Uri Download, string Digest);
internal static class ReleaseUpdater
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(3) };
    static ReleaseUpdater() { Client.DefaultRequestHeaders.UserAgent.ParseAdd("CodexRail/0.14.7"); }
    internal static ReleaseUpdate? Parse(string json, Version current)
    {
        using var doc = JsonDocument.Parse(json); var root = doc.RootElement;
        if (root.GetProperty("draft").GetBoolean() || root.GetProperty("prerelease").GetBoolean()) return null;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v'), out var version) || version <= new Version(current.Major, current.Minor, current.Build)) return null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != $"CodexRail-{tag}-win-x64.zip") continue;
            var url = new Uri(asset.GetProperty("browser_download_url").GetString()!);
            if (url.Scheme != "https" || url.Host != "github.com" || !url.AbsolutePath.StartsWith("/SeaserL/Codex-Rail/releases/download/", StringComparison.Ordinal)) throw new InvalidDataException("Unexpected release download location");
            var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
            if (digest is null || !digest.StartsWith("sha256:", StringComparison.Ordinal) || digest.Length != 71) throw new InvalidDataException("Release SHA256 digest is missing");
            return new(version, tag, url, digest);
        }
        return null;
    }
    internal static async Task<ReleaseUpdate?> CheckAsync() => Parse(await Client.GetStringAsync("https://api.github.com/repos/SeaserL/Codex-Rail/releases/latest"), new Version(Application.ProductVersion.Split('+')[0]));
    internal static void VerifyArchive(string zipPath, ReleaseUpdate release, string destination, bool standalone)
    {
        using (var stream = File.OpenRead(zipPath)) if ("sha256:" + Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant() != release.Digest) throw new InvalidDataException("ZIP checksum mismatch");
        using var zip = ZipFile.OpenRead(zipPath);
        var sums = zip.GetEntry("SHA256SUMS.txt") ?? throw new InvalidDataException("Checksums missing");
        using var reader = new StreamReader(sums.Open()); var lines = reader.ReadToEnd().Split('\n');
        var names = new[] { standalone ? "CodexRail-win-x64-standalone.exe" : "CodexRail-win-x64.exe", "CodexRail.Watcher.exe" };
        Directory.CreateDirectory(destination);
        foreach (var name in names)
        {
            if (zip.Entries.Count(e => e.FullName == name) != 1) throw new InvalidDataException("Missing/duplicate update binary: " + name);
            var entry = zip.GetEntry(name)!; if (entry.Length > 100 * 1024 * 1024) throw new InvalidDataException("Update binary too large");
            using (var stream = entry.Open())
            {
                var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                if (!lines.Any(line => line.Trim().Equals(hash + "  " + name, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Binary checksum mismatch: " + name);
            }
            entry.ExtractToFile(Path.Combine(destination, name), true);
        }
        var fileVersion = FileVersionInfo.GetVersionInfo(Path.Combine(destination, names[0])).FileVersion;
        if (!Version.TryParse(fileVersion, out var version) || new Version(version.Major, version.Minor, version.Build) != release.Version) throw new InvalidDataException("Binary version mismatch");
    }
    internal static async Task<string> StageAsync(ReleaseUpdate release, bool standalone)
    {
        var root = Path.Combine(AppPaths.DataDirectory, "updates", release.Tag); Directory.CreateDirectory(root); var zipPath = Path.Combine(root, "release.zip");
        using var response = await Client.GetAsync(release.Download, HttpCompletionOption.ResponseHeadersRead); response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 100 * 1024 * 1024) throw new InvalidDataException("Release too large");
        await using (var input = await response.Content.ReadAsStreamAsync()) await using (var output = File.Create(zipPath))
        { var buffer = new byte[65536]; long total = 0; int read; while ((read = await input.ReadAsync(buffer)) > 0) { total += read; if (total > 100 * 1024 * 1024) throw new InvalidDataException("Release too large"); await output.WriteAsync(buffer.AsMemory(0, read)); } }
        await Task.Run(() => VerifyArchive(zipPath, release, root, standalone)); return root;
    }
}
internal sealed partial class UsageCardContext
{
    private bool _updateBusy, _updateInstalling;
    private DateTimeOffset _nextUpdate = DateTimeOffset.UtcNow.AddSeconds(30);
    private ReleaseUpdate? _availableUpdate;
    private void AddUpdateMenu(ContextMenuStrip menu)
    {
        var check = menu.Items.Add("检查更新…", null, async (_, _) => await CheckUpdateAsync(true)); check.Enabled = !AppPaths.Development;
        var automatic = new ToolStripMenuItem("后台检查更新") { CheckOnClick = true, Checked = _settings.CheckUpdates, Enabled = !AppPaths.Development };
        automatic.CheckedChanged += (_, _) => { _settings.CheckUpdates = automatic.Checked; Save(); }; menu.Items.Add(automatic);
        _tray.BalloonTipClicked += async (_, _) => { if (_availableUpdate is { } update) await OfferUpdateAsync(update); };
    }
    private async Task CheckUpdateAsync(bool manual)
    {
        if (_updateBusy || _disposed || AppPaths.Development || (!manual && (!_settings.CheckUpdates || DateTimeOffset.UtcNow < _nextUpdate))) return;
        _updateBusy = true; _nextUpdate = DateTimeOffset.UtcNow.AddDays(1);
        try
        {
            var release = await ReleaseUpdater.CheckAsync(); if (_disposed) return; _availableUpdate = release;
            if (release is null) { if (manual) MessageBox.Show(UiText.IsEnglish ? "You are up to date." : "当前已是最新版本。"); return; }
            if (manual) await OfferUpdateAsync(release);
            else { _tray.BalloonTipTitle = "Codex Rail " + release.Tag; _tray.BalloonTipText = UiText.IsEnglish ? "An update is available. Click to review and install." : "发现新版，点击提示确认更新。"; _tray.ShowBalloonTip(10000); }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or JsonException or TaskCanceledException or InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        { if (manual) MessageBox.Show((UiText.IsEnglish ? "Update failed: " : "更新失败：") + ex.Message); }
        finally { _updateBusy = false; }
    }
    private async Task OfferUpdateAsync(ReleaseUpdate update)
    {
        if (_updateInstalling || _disposed) return;
        _updateInstalling = true;
        try
        {
            if (MessageBox.Show(UiText.IsEnglish ? $"Install {update.Tag} and restart? Your settings will be retained." : $"安装 {update.Tag} 并重启？设置将保留。", "Codex Rail", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            if (!File.Exists(WatcherPath)) throw new IOException("Missing updater helper; extract the complete ZIP.");
            var standalone = Path.GetFileName(Environment.ProcessPath!).Contains("standalone", StringComparison.OrdinalIgnoreCase) || new FileInfo(Environment.ProcessPath!).Length > 10 * 1024 * 1024;
            var staged = await ReleaseUpdater.StageAsync(update, standalone); if (_disposed) return;
            var source = Path.Combine(staged, standalone ? "CodexRail-win-x64-standalone.exe" : "CodexRail-win-x64.exe");
            var probe = Path.Combine(AppContext.BaseDirectory, ".update-write-check"); File.WriteAllText(probe, ""); File.Delete(probe);
            var start = new ProcessStartInfo(Path.Combine(staged, "CodexRail.Watcher.exe")) { UseShellExecute = false, CreateNoWindow = true };
            foreach (var arg in new[] { "--apply", source, Environment.ProcessPath!, Environment.ProcessId.ToString(), _following ? "follow" : "manual", IsStartupEnabled() ? "watch" : "none" }) start.ArgumentList.Add(arg);
            if (Process.Start(start) is not { } updater) throw new IOException("Updater did not start"); updater.Dispose(); ExitThread();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or JsonException or TaskCanceledException or InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception)
        { MessageBox.Show((UiText.IsEnglish ? "Update failed: " : "更新失败：") + ex.Message); }
        finally { _updateInstalling = false; }
    }
}
