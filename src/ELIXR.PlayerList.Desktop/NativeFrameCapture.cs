#if DEBUG
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ELIXR.PlayerList.Desktop;

// Captures this application's own standard Windows frame for diagnostic screenshots.
internal static class NativeFrameCapture
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct WindowRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd, out WindowRect rect);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hwnd, IntPtr dc, uint flags);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);

    internal static BitmapSource Capture(Window window, out WindowRect rect)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        GetWindowRect(hwnd, out rect);
        var dc = GetWindowDC(hwnd);
        var memory = CreateCompatibleDC(dc);
        var bitmap = CreateCompatibleBitmap(dc, rect.Right-rect.Left, rect.Bottom-rect.Top);
        var old = SelectObject(memory, bitmap);
        try
        {
            PrintWindow(hwnd,memory,2);
            var result = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            result.Freeze();
            return result;
        }
        finally
        {
            SelectObject(memory,old);
            DeleteObject(bitmap);DeleteDC(memory);ReleaseDC(hwnd,dc);
        }
    }
}
#endif

