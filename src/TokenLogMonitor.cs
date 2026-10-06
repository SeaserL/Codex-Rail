using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace CodexTokenOverlay;

internal sealed class TokenLogMonitor : IDisposable
{
    private const int TailBytes = 4 * 1024 * 1024;
    private const int HistoricalOverlapBytes = 256 * 1024;
    private readonly string _sessionRoot;
    private readonly FileSystemWatcher? _watcher;
    private readonly ConcurrentDictionary<string, byte> _changedPaths = new(StringComparer.OrdinalIgnoreCase);
    private int _rescanNeeded;
    private const int MaximumPendingPaths = 256;
    private readonly ConcurrentDictionary<string, bool> _rootSessionCache = new(StringComparer.OrdinalIgnoreCase);
    private string? _activeLogPath;
    private DateTime _activeWriteUtc;
    private long _activeLength = -1;
    private DateTime _lastFullScanUtc = DateTime.MinValue;
    private TokenSnapshot? _lastSnapshot;
    private string? _selectedThreadId;

    public long ActiveSessionVersion { get; private set; }

    public string? ActiveThreadId => _selectedThreadId;

    public string? PreferredThreadId { get; set; }
    // The most recently written log is not proof of the visible conversation.
    public bool RequirePreferredThread { get; set; }
    public IReadOnlySet<string>? AllowedThreadIds { get; set; }

    private bool IsAllowedSession(string path) => AllowedThreadIds is null
        || AllowedThreadIds.Contains(ExtractThreadId(path));

    public TokenLogMonitor(string? sessionRoot = null)
    {
        _sessionRoot = sessionRoot ?? SessionPathResolver.Resolve();

        if (!Directory.Exists(_sessionRoot))
        {
            return;
        }

        _watcher = new FileSystemWatcher(_sessionRoot, "*.jsonl")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnLogChanged;
        _watcher.Created += OnLogChanged;
        _watcher.Renamed += (_, eventArgs) => RecordChangedPath(eventArgs.FullPath);
    }

    public bool PinActiveSession { get; set; }

    public TokenSnapshot? Poll(bool forceFullScan = false)
    {
        if (!Directory.Exists(_sessionRoot))
        {
            return null;
        }

        forceFullScan |= Interlocked.Exchange(ref _rescanNeeded, 0) != 0;
        var usePreferredThread = !PinActiveSession && !string.IsNullOrWhiteSpace(PreferredThreadId);
        if (RequirePreferredThread && !usePreferredThread)
        {
            SwitchActiveLog(null);
            return null;
        }
        if (!usePreferredThread && _activeLogPath is not null && !IsAllowedSession(_activeLogPath))
            SwitchActiveLog(null);
        ProcessChangedPaths(allowAutomaticSwitch: !usePreferredThread);

        if (usePreferredThread)
        {
            SelectPreferredRootSession(PreferredThreadId!);
        }
        else if (forceFullScan || _activeLogPath is null || DateTime.UtcNow - _lastFullScanUtc > TimeSpan.FromSeconds(20))
        {
            SelectNewestRootSession();
        }

        if (_activeLogPath is null || !File.Exists(_activeLogPath))
        {
            return _lastSnapshot;
        }

        DateTime writeUtc;
        long length;
        try
        {
            var info = new FileInfo(_activeLogPath);
            writeUtc = info.LastWriteTimeUtc;
            length = info.Length;
        }
        catch (IOException)
        {
            return _lastSnapshot;
        }

        if (_lastSnapshot is not null && writeUtc == _activeWriteUtc && length == _activeLength)
        {
            return _lastSnapshot;
        }

        var parsed = TryReadLatestTokenSnapshot(_activeLogPath, writeUtc);
        if (parsed is not null)
        {
            // 只有完整解析成功后才提交文件版本，避免卡在写到一半的 JSON 行。
            _activeWriteUtc = writeUtc;
            _activeLength = length;
            _lastSnapshot = parsed;
        }

        return _lastSnapshot;
    }

    private void RecordChangedPath(string path)
    {
        if (!_changedPaths.TryAdd(path, 0)) return;
        if (_changedPaths.Count > MaximumPendingPaths) { _changedPaths.Clear(); Interlocked.Exchange(ref _rescanNeeded, 1); }
    }
    private void OnLogChanged(object sender, FileSystemEventArgs eventArgs)
    {
        RecordChangedPath(eventArgs.FullPath);
    }

    private void ProcessChangedPaths(bool allowAutomaticSwitch)
    {
        var newestPath = _activeLogPath;
        var newestWriteUtc = _activeLogPath is null ? DateTime.MinValue : SafeGetLastWriteUtc(_activeLogPath);

        foreach (var path in _changedPaths.Keys)
        {
            if (!_changedPaths.TryRemove(path, out _)) continue;
            if (!allowAutomaticSwitch)
            {
                continue;
            }

            if (PinActiveSession
                && _activeLogPath is not null
                && !path.Equals(_activeLogPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!File.Exists(path) || !IsAllowedSession(path) || !IsRootDesktopSession(path))
            {
                continue;
            }

            var writeUtc = SafeGetLastWriteUtc(path);
            if (writeUtc >= newestWriteUtc)
            {
                newestPath = path;
                newestWriteUtc = writeUtc;
            }
        }

        if (newestPath is not null && !newestPath.Equals(_activeLogPath, StringComparison.OrdinalIgnoreCase))
        {
            SwitchActiveLog(newestPath);
        }
    }

    private void SelectPreferredRootSession(string threadId)
    {
        if (_selectedThreadId?.Equals(threadId, StringComparison.OrdinalIgnoreCase) == true
            && _activeLogPath is not null
            && File.Exists(_activeLogPath))
        {
            return;
        }

        try
        {
            var searchPattern = $"*{threadId}.jsonl";
            var candidate = Directory.EnumerateFiles(_sessionRoot, searchPattern, SearchOption.AllDirectories)
                .Where(IsRootDesktopSession)
                .OrderByDescending(SafeGetLastWriteUtc)
                .FirstOrDefault();

            SwitchActiveLog(candidate, threadId);
        }
        catch (IOException)
        {
            SwitchActiveLog(null, threadId);
        }
        catch (UnauthorizedAccessException)
        {
            SwitchActiveLog(null, threadId);
        }
        catch (ArgumentException)
        {
            // IPC 会话 ID 理论上是 UUID；异常输入只显示等待状态。
            SwitchActiveLog(null, threadId);
        }
    }

    private void SelectNewestRootSession()
    {
        _lastFullScanUtc = DateTime.UtcNow;

        if (PinActiveSession && _activeLogPath is not null && File.Exists(_activeLogPath))
        {
            return;
        }

        try
        {
            var candidates = Directory.EnumerateFiles(_sessionRoot, "*.jsonl", SearchOption.AllDirectories)
                .Select(path => new { Path = path, WriteUtc = SafeGetLastWriteUtc(path) })
                .OrderByDescending(item => item.WriteUtc);

            foreach (var candidate in candidates)
            {
                if (!IsAllowedSession(candidate.Path) || !IsRootDesktopSession(candidate.Path))
                {
                    continue;
                }

                if (!_activeLogPath?.Equals(candidate.Path, StringComparison.OrdinalIgnoreCase) ?? true)
                {
                    SwitchActiveLog(candidate.Path, ExtractThreadId(candidate.Path));
                }
                return;
            }
        }
        catch (IOException)
        {
            // Codex 正在轮转日志时，下一个轮询周期会重试。
        }
        catch (UnauthorizedAccessException)
        {
            // 个别旧目录不可读时保留上一次成功结果。
        }
    }

    private bool IsRootDesktopSession(string path)
    {
        if (_rootSessionCache.TryGetValue(path, out var cached))
        {
            return cached;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 64 * 1024);
            var firstLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(firstLine))
            {
                // Created 事件可能早于 Codex 写完首行，不能把暂时失败永久缓存。
                return false;
            }

            using var document = JsonDocument.Parse(firstLine);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var type) || type.GetString() != "session_meta")
            {
                _rootSessionCache[path] = false;
                return false;
            }

            if (!root.TryGetProperty("payload", out var payload))
            {
                _rootSessionCache[path] = false;
                return false;
            }

            if (!payload.TryGetProperty("originator", out var originator)
                || originator.ValueKind != JsonValueKind.String
                || !string.Equals(originator.GetString(), "Codex Desktop", StringComparison.OrdinalIgnoreCase))
            {
                _rootSessionCache[path] = false;
                return false;
            }

            // Desktop 根会话首行固定为 source="vscode"；子代理后续会重放父会话，
            // 因此必须只检查第一物理行并严格匹配字符串，不能搜索整份文件。
            var isRoot = payload.TryGetProperty("source", out var source)
                && source.ValueKind == JsonValueKind.String
                && string.Equals(source.GetString(), "vscode", StringComparison.OrdinalIgnoreCase);
            _rootSessionCache[path] = isRoot;
            return isRoot;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // 首行可能仍在写入；不缓存失败，下次 Changed 或全扫描会重试。
            return false;
        }
    }

    private void SwitchActiveLog(string? path, string? threadId = null)
    {
        threadId ??= path is null ? null : ExtractThreadId(path);
        if (string.Equals(_activeLogPath, path, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_selectedThreadId, threadId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _activeLogPath = path;
        _selectedThreadId = threadId;
        _activeWriteUtc = DateTime.MinValue;
        _activeLength = -1;
        _lastSnapshot = null;
        ActiveSessionVersion++;
    }

    private static TokenSnapshot? TryReadLatestTokenSnapshot(string path, DateTime writeUtc)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var blockEnd = stream.Length;

            while (blockEnd > 0)
            {
                var blockStart = Math.Max(0, blockEnd - TailBytes);
                var bytesToRead = (int)(blockEnd - blockStart);
                var buffer = new byte[bytesToRead];
                stream.Seek(blockStart, SeekOrigin.Begin);
                var offset = 0;
                while (offset < bytesToRead)
                {
                    var read = stream.Read(buffer, offset, bytesToRead - offset);
                    if (read == 0)
                    {
                        break;
                    }
                    offset += read;
                }

                var text = Encoding.UTF8.GetString(buffer, 0, offset);
                if (blockStart > 0)
                {
                    // 当前块可能从一行中间开始；下一块会以重叠区补齐这行。
                    var firstNewLine = text.IndexOf('\n');
                    text = firstNewLine >= 0 ? text[(firstNewLine + 1)..] : string.Empty;
                }

                var parsed = TryParseLatestTokenSnapshot(text, path, writeUtc);
                if (parsed is not null)
                {
                    return parsed;
                }

                if (blockStart == 0)
                {
                    break;
                }
                blockEnd = Math.Min(stream.Length, blockStart + HistoricalOverlapBytes);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // 日志可能正写到一半；保留上一帧，下一次更新会再次解析。
        }

        return null;
    }

    private static TokenSnapshot? TryParseLatestTokenSnapshot(string text, string path, DateTime writeUtc)
    {
        var lines = text.Split('\n');

        for (var index = lines.Length - 1; index >= 0; index--)
        {
            var line = lines[index].Trim();
            if (line.Length == 0 || !line.Contains("\"token_count\"", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!root.TryGetProperty("type", out var eventType)
                    || eventType.ValueKind != JsonValueKind.String
                    || eventType.GetString() != "event_msg")
                {
                    continue;
                }

                if (!root.TryGetProperty("payload", out var payload)
                    || payload.ValueKind != JsonValueKind.Object
                    || !payload.TryGetProperty("type", out var payloadType)
                    || payloadType.ValueKind != JsonValueKind.String
                    || payloadType.GetString() != "token_count"
                    || !payload.TryGetProperty("info", out var info)
                    || info.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!info.TryGetProperty("total_token_usage", out var total)
                    || total.ValueKind != JsonValueKind.Object
                    || !info.TryGetProperty("last_token_usage", out var last)
                    || last.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var threadId = ExtractThreadId(path);
                return new TokenSnapshot(
                    threadId,
                    path,
                    GetLong(total, "total_tokens"),
                    GetLong(total, "input_tokens"),
                    GetLong(total, "cached_input_tokens"),
                    GetLong(total, "output_tokens"),
                    GetLong(total, "reasoning_output_tokens"),
                    GetLong(last, "total_tokens"),
                    GetLong(info, "model_context_window"),
                    writeUtc);
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                // Codex 可能正在追加最后一行；继续寻找前一个完整快照。
            }
        }

        return null;
    }

    private static long GetLong(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.TryGetInt64(out var value)
            ? value
            : 0;
    }

    private static string ExtractThreadId(string path)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);
        return fileName.Length >= 36 ? fileName[^36..] : fileName;
    }

    private static DateTime SafeGetLastWriteUtc(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return DateTime.MinValue;
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
