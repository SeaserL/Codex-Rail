namespace CodexTokenOverlay;

internal sealed record WindowCandidateFacts(
    IntPtr Handle,
    uint ProcessId,
    bool IsCodexProcess,
    bool IsVisible,
    bool IsMinimized,
    IntPtr OwnerHandle,
    long ExtendedStyle,
    IntRect Bounds,
    string ClassName);

internal sealed record CodexWindowCandidateSelection(WindowCandidateFacts Host);

internal static class CodexWindowClassifier
{
    internal const long WsExToolWindow = 0x00000080L;
    internal const long WsExLayered = 0x00080000L;
    private const string ChromiumTopLevelClass = "Chrome_WidgetWin_1";

    public static CodexWindowCandidateSelection? Select(
        IReadOnlyList<WindowCandidateFacts> candidates,
        IntPtr foregroundHandle)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var foreground = candidates.FirstOrDefault(item => item.Handle == foregroundHandle);
        if (foreground is null || !foreground.IsCodexProcess || !foreground.IsVisible)
        {
            return null;
        }

        var sameProcess = candidates
            .Where(item => item.ProcessId == foreground.ProcessId && item.IsCodexProcess)
            .ToArray();
        var host = sameProcess
            .Where(IsHost)
            .OrderByDescending(Area)
            .ThenBy(item => item.Handle.ToInt64())
            .FirstOrDefault();
        if (host is null)
        {
            return null;
        }

        return new CodexWindowCandidateSelection(host);
    }

    private static bool IsHost(WindowCandidateFacts item) =>
        item.IsVisible &&
        !item.IsMinimized &&
        item.OwnerHandle == IntPtr.Zero &&
        item.ClassName.Equals(ChromiumTopLevelClass, StringComparison.Ordinal) &&
        item.Bounds.Width >= 500 &&
        item.Bounds.Height >= 400 &&
        (item.ExtendedStyle & (WsExToolWindow | WsExLayered)) == 0;

    private static long Area(WindowCandidateFacts item) =>
        (long)item.Bounds.Width * item.Bounds.Height;
}
