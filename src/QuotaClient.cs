using System.Diagnostics;
using System.Text.Json;

namespace CodexTokenOverlay;

internal sealed record QuotaWindow(double UsedPercent, int Minutes, long? ResetsAt)
{
    public double Remaining => Math.Clamp(100 - UsedPercent, 0, 100);
}

internal sealed record QuotaData(QuotaWindow? FiveHour, QuotaWindow? Weekly, DateTimeOffset ReadAt)
{
    public static QuotaData Parse(JsonElement result)
    {
        JsonElement bucket;
        // An explicitly present multi-bucket view is authoritative. Do not guess another plan bucket.
        if (result.TryGetProperty("rateLimitsByLimitId", out var buckets) && buckets.ValueKind == JsonValueKind.Object)
        {
            if (!buckets.TryGetProperty("codex", out bucket))
                return new(null, null, DateTimeOffset.Now);
        }
        else if (!result.TryGetProperty("rateLimits", out bucket))
            return new(null, null, DateTimeOffset.Now);
        if (bucket.ValueKind != JsonValueKind.Object) return new(null, null, DateTimeOffset.Now);
        QuotaWindow? five = null, week = null;
        foreach (var name in new[] { "primary", "secondary" })
        {
            if (!bucket.TryGetProperty(name, out var window) || window.ValueKind != JsonValueKind.Object) continue;
            if (!window.TryGetProperty("usedPercent", out var used) || !used.TryGetDouble(out var percent)
                || !double.IsFinite(percent) || !window.TryGetProperty("windowDurationMins", out var minutes)
                || !minutes.TryGetInt32(out var duration)) continue;
            long? reset = window.TryGetProperty("resetsAt", out var resetValue) && resetValue.TryGetInt64(out var timestamp)
                && timestamp is >= 0 and <= 253402300799 ? timestamp : null;
            var parsed = new QuotaWindow(Math.Clamp(percent, 0, 100), duration, reset);
            if (duration == 300) five = parsed;
            if (duration == 10080) week = parsed;
        }
        return new(five, week, DateTimeOffset.Now);
    }
}

// Reads only account rate limits. Never creates a thread/turn or reads authentication files.
internal sealed class QuotaClient : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private Process? _process;
    private Task? _errorDrain;
    private int _id;
    public int? ProcessId => _process is { HasExited: false } p ? p.Id : null;

    public static string FindCodex()
    {
        var configured = Environment.GetEnvironmentVariable("CODEX_USAGE_CODEX");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return Path.GetFullPath(configured);
        var bin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        if (Directory.Exists(bin))
        {
            var candidate = Directory.GetDirectories(bin).Select(dir => Path.Combine(dir, "codex.exe"))
                .Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (candidate is not null) return candidate;
        }
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var candidate = Path.Combine(dir.Trim('"'), "codex.exe");
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException("未找到 Codex 运行时");
    }

    public async Task<QuotaData> ReadAsync()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        var ct = timeout.Token;
        await _gate.WaitAsync(ct);
        try
        {
            if (_process is null || _process.HasExited)
            {
                Stop();
                var start = new ProcessStartInfo(FindCodex())
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                start.ArgumentList.Add("app-server");
                _process = Process.Start(start) ?? throw new IOException("额度服务启动失败");
                var process = _process;
                _errorDrain = Task.Run(async () =>
                {
                    try { while (await process.StandardError.ReadLineAsync(_shutdown.Token) is not null) { } }
                    catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException) { }
                });
                await RequestAsync("initialize", new { clientInfo = new { name = "codex_rail", title = "Codex Rail", version = "0.14.4" } }, ct);
                await _process.StandardInput.WriteLineAsync("{\"method\":\"initialized\",\"params\":{}}".AsMemory(), ct);
                await _process.StandardInput.FlushAsync(ct);
            }
            return QuotaData.Parse(await RequestAsync("account/rateLimits/read", null, ct));
        }
        // A snapshot is sufficient for this card; avoid retaining the app-server between reads.
        finally { Stop(); _gate.Release(); }
    }

    private async Task<JsonElement> RequestAsync(string method, object? parameters, CancellationToken ct)
    {
        var id = ++_id;
        var process = _process ?? throw new IOException("服务未运行");
        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { id, method, @params = parameters }).AsMemory(), ct);
        await process.StandardInput.FlushAsync(ct);
        while (await process.StandardOutput.ReadLineAsync(ct) is { } line)
        {
            using var message = JsonDocument.Parse(line);
            var root = message.RootElement;
            if (!root.TryGetProperty("id", out var responseId) || !responseId.TryGetInt32(out var number) || number != id) continue;
            if (root.TryGetProperty("error", out _)) throw new IOException("额度接口返回错误（请检查登录状态）");
            if (root.TryGetProperty("result", out var result)) return result.Clone();
        }
        throw new IOException("额度服务连接中断");
    }

    public void Stop()
    {
        var process = _process; _process = null;
        if (process is null) return;
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        process.Dispose();
    }
    public void Dispose() { _shutdown.Cancel(); Stop(); }
}
