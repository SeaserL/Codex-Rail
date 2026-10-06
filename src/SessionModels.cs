using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace CodexTokenOverlay;

internal sealed record TokenSnapshot(
    string ThreadId,
    string LogPath,
    long TotalTokens,
    long InputTokens,
    long CachedInputTokens,
    long OutputTokens,
    long ReasoningOutputTokens,
    long ContextUsedTokens,
    long ContextWindowTokens,
    DateTime UpdatedAtUtc)
{
    public double ContextPercent => ContextWindowTokens <= 0
        ? 0
        : Math.Clamp(ContextUsedTokens * 100d / ContextWindowTokens, 0, 100);

    public double CacheHitPercent => InputTokens <= 0
        ? 0d
        : Math.Clamp((double)CachedInputTokens * 100d / InputTokens, 0d, 100d);

    public long UncachedInputTokens => Math.Max(0, InputTokens - CachedInputTokens);
}

internal sealed record ActiveThreadRouteStatus(
    string? ThreadId,
    int ActiveWindowCount,
    bool IsConnected,
    long Version,
    string? LastError);
