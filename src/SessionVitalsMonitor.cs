using System.Globalization;
using System.Text;
using System.Text.Json;
namespace CodexTokenOverlay;

internal sealed record SessionVitals(string Model, string Effort, string State, double? AverageOutputTps, double? FirstTokenMs, double? TurnSeconds);
internal sealed class SessionVitalsMonitor
{
    private static readonly string[] MetadataKinds = ["\"turn_context\"", "\"task_started\"", "\"task_complete\"", "\"token_count\"", "\"task_aborted\"", "\"turn_aborted\"", "\"task_interrupted\""];
    private string? _path;
    private long _offset;
    private string _model = "未知", _effort = "", _state = "等待数据";
    private string? _turn;
    private DateTimeOffset? _start;
    private long? _lastOutput, _baseline, _turnOutput;
    private double? _duration, _firstToken;
    private bool _active;
    internal SessionVitals Poll(string? path)
    {
        if (path != _path) { _path = path; _offset = 0; _model = "未知"; _effort = ""; _state = "等待数据"; _turn = null; _start = null; _lastOutput = _baseline = _turnOutput = null; _duration = _firstToken = null; _active = false; }
        if (path is not null)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length < _offset) { _offset = 0; _baseline = _lastOutput = _turnOutput = null; _start = null; _duration = _firstToken = null; _active = false; _state = "等待数据"; }
            if (stream.Length > _offset)
            {
                const int limit = 4 * 1024 * 1024; var truncated = stream.Length - _offset > limit;
                if (truncated) { _offset = stream.Length - limit; _baseline = _lastOutput = _turnOutput = null; _start = null; _duration = _firstToken = null; _active = false; _state = "等待数据"; }
                stream.Position = _offset; var bytes = new byte[(int)Math.Min(limit, stream.Length - _offset)]; var read = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
                if (read == 0) return Snapshot(DateTimeOffset.UtcNow);
                var end = Array.LastIndexOf(bytes, (byte)'\n', read - 1, read);
                if (end >= 0) { var text = Encoding.UTF8.GetString(bytes, 0, end + 1); if (truncated) { var first = text.IndexOf('\n'); text = first >= 0 ? text[(first + 1)..] : ""; } foreach (var line in text.Split('\n')) Accept(line); _offset += end + 1; }
            }
        }
        return Snapshot(DateTimeOffset.UtcNow);
    }
    internal SessionVitals Snapshot(DateTimeOffset now)
    {
        var seconds = _active && _start is { } start ? Math.Max(0, (now - start).TotalSeconds) : _duration;
        var speed = seconds is > 0 && _turnOutput is >= 0 ? _turnOutput.Value / seconds : null as double?;
        return new(_model, _effort, _state, speed, _firstToken, seconds);
    }
    internal void Accept(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        // Ignore transcript content entirely; parse only metadata events relevant to this monitor.
        if (!MetadataKinds.Any(line.Contains)) return;
        try
        {
            using var doc = JsonDocument.Parse(line); var root = doc.RootElement;
            if (!root.TryGetProperty("payload", out var p) || p.ValueKind != JsonValueKind.Object) return;
            var outer = String(root, "type");
            if (outer == "turn_context") { if (String(p, "model") is { } model) _model = model; if (String(p, "effort") is { } effort) _effort = effort; return; }
            if (outer != "event_msg") return;
            switch (String(p, "type"))
            {
                case "task_started":
                    _turn = String(p, "turn_id"); _start = Time(root, "timestamp"); _baseline = _lastOutput; _turnOutput = null; _duration = _firstToken = null; _active = true; _state = "进行中"; break;
                case "token_count":
                    if (!p.TryGetProperty("info", out var info) || info.ValueKind != JsonValueKind.Object || !info.TryGetProperty("total_token_usage", out var total)) break;
                    var output = Long(total, "output_tokens"); if (output is null) break;
                    if (_active && _start is not null)
                    {
                        if (_baseline is null && info.TryGetProperty("last_token_usage", out var last) && Long(last, "output_tokens") is { } lastOutput) _baseline = Math.Max(0, output.Value - lastOutput);
                        _turnOutput = _baseline is { } baseline && output >= baseline ? output - baseline : null;
                        if (_lastOutput is { } previous && output < previous) { _baseline = null; _turnOutput = null; }
                    }
                    _lastOutput = output; break;
                case "task_complete":
                    if (_turn is null || String(p, "turn_id") != _turn) break;
                    _duration = Long(p, "duration_ms") is { } ms && ms > 0 ? ms / 1000d : _start is { } start && Time(root, "timestamp") is { } end && end > start ? (end - start).TotalSeconds : null;
                    _firstToken = Long(p, "time_to_first_token_ms") is { } first && first >= 0 ? first : null; _active = false; _state = "已完成"; break;
                case "task_aborted":
                case "turn_aborted":
                case "task_interrupted":
                    if (String(p, "turn_id") is { } turn && _turn != turn) break;
                    _active = false; _state = "已中止"; _duration = null; _turnOutput = null; break;
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { }
    }
    private static string? String(JsonElement obj, string name) => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    private static long? Long(JsonElement obj, string name) => obj.TryGetProperty(name, out var v) && v.TryGetInt64(out var n) ? n : null;
    private static DateTimeOffset? Time(JsonElement obj, string name) => DateTimeOffset.TryParse(String(obj, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time) ? time : null;
}
