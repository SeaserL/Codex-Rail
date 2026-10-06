using System.Drawing;
using System.Text.Json.Serialization;

namespace CodexTokenOverlay;

internal readonly record struct IntRect(int X, int Y, int Width, int Height)
{
    [JsonIgnore] public int Left => X;
    [JsonIgnore] public int Top => Y;
    [JsonIgnore] public int Right => X + Width;
    [JsonIgnore] public int Bottom => Y + Height;
    [JsonIgnore] public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(int x, int y) =>
        x >= Left && x < Right && y >= Top && y < Bottom;

    public Rectangle ToRectangle() => new(X, Y, Width, Height);

    public static IntRect FromRectangle(Rectangle value) =>
        new(value.X, value.Y, value.Width, value.Height);
}

internal readonly record struct WindowChromeMetrics(
    int CaptionButtonWidth,
    int CaptionButtonHeight,
    int FrameWidth,
    int FrameHeight,
    int PaddedBorderWidth);

internal sealed record CodexWindowInfo(
    IntPtr Handle,
    IntRect WindowBounds,
    IntRect ExtendedFrameBounds,
    IntRect? CaptionButtonBounds,
    IntRect WorkingArea,
    uint Dpi,
    WindowChromeMetrics ChromeMetrics);
