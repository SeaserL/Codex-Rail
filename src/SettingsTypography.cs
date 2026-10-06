namespace CodexTokenOverlay;

internal static class SettingsTypography
{
    internal static readonly Font Body = new("Microsoft YaHei UI", 9);
    internal static readonly Font Heading = new("Microsoft YaHei UI", 10, FontStyle.Bold);
    static SettingsTypography() { Application.ApplicationExit += (_, _) => { Body.Dispose(); Heading.Dispose(); }; }
}
