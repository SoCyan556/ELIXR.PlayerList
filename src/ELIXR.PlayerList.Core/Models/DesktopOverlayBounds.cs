namespace ELIXR.PlayerList.Core.Models;

public sealed record DesktopOverlayBounds(double Left, double Top, double Width, double Height)
{
    public static DesktopOverlayBounds Clamp(DesktopOverlayBounds value, DesktopOverlayBounds screen)
    {
        static double Safe(double number, double fallback) => double.IsFinite(number) ? number : fallback;
        var maxWidth = Math.Max(1, Safe(screen.Width, 1920));
        var maxHeight = Math.Max(1, Safe(screen.Height, 1080));
        var width = Math.Clamp(Safe(value.Width, 440), Math.Min(240, maxWidth), maxWidth);
        var height = Math.Clamp(Safe(value.Height, 660), Math.Min(240, maxHeight), maxHeight);
        var left = Safe(screen.Left, 0); var top = Safe(screen.Top, 0);
        return new(Math.Clamp(Safe(value.Left, left), left, left + maxWidth - width),
            Math.Clamp(Safe(value.Top, top), top, top + maxHeight - height), width, height);
    }
}
