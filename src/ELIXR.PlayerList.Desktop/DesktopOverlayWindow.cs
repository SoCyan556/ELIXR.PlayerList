using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Shell;
using System.Windows.Threading;
using ELIXR.PlayerList.Core.Models;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ELIXR.PlayerList.Desktop;

// A separate desktop surface, not the SteamVR overlay. Reuses the existing browser process/profile.
public sealed class DesktopOverlayWindow : Window
{
    private const string Origin = "https://elixr.example";
    private readonly WebView2 _browser = new() { DefaultBackgroundColor = System.Drawing.Color.FromArgb(20, 20, 20) };
    private bool _ready, _closed;
    private bool _finishingClose, _allowClose, _applyingPlacement;
    private (int Width, int Height, int Diameter) _regionBounds;
    private readonly DispatcherTimer _placementTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private readonly Func<DesktopOverlayBounds, Task<bool>>? _savePlacement;
    private OverlayResizeWindow? _resizeEditor;
    internal DesktopOverlayBounds Placement => new(Left, Top, Width, Height);
    public event EventHandler? Ready;
    public event EventHandler<CoreWebView2WebMessageReceivedEventArgs>? CommandRequested;
    public DesktopOverlayWindow(CoreWebView2Environment environment, bool preview, Rect? placement = null, string? imageCacheDirectory = null, Func<DesktopOverlayBounds, Task<bool>>? savePlacement = null)
    {
        Title = "ELIXR · Desktop Overlay";
        _savePlacement = savePlacement;
        Width = 440; Height = 660; MinWidth = 240; MinHeight = 240;
        WindowStyle = WindowStyle.None;
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false });
        Topmost = true; ShowActivated = false; ShowInTaskbar = false;
        ResizeMode = ResizeMode.CanResize;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 18, 25));
        var work = SystemParameters.WorkArea;
        Left = Math.Max(work.Left, work.Right - Width - 24); Top = work.Top + 24;
        if (placement is { } bounds)
        {
            ApplyPlacement(new(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
        }
        _placementTimer.Tick += async (_, _) => { _placementTimer.Stop(); if (!_closed && !_finishingClose && _resizeEditor is null && _savePlacement is not null) await _savePlacement(Placement); };
        LocationChanged += (_, _) => QueuePlacementSave();
        SizeChanged += (_, _) => { UpdateWindowRegion(); QueuePlacementSave(); };
        Content = _browser;
        SourceInitialized += (_, _) =>
        {
            var dark = 1; var corners = 1; // Custom region owns the silhouette; do not add DWM's different-radius frame.
            var border = unchecked((int)0xfffffffe); // DWMWA_COLOR_NONE
            var handle = new WindowInteropHelper(this).Handle;
            DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
            DwmSetWindowAttribute(handle, 33, ref corners, sizeof(int));
            DwmSetWindowAttribute(handle, 34, ref border, sizeof(int));
            HwndSource.FromHwnd(handle)?.AddHook(WindowMessages);
            UpdateWindowRegion();
        };
        Loaded += async (_, _) =>
        {
            try
            {
                await _browser.EnsureCoreWebView2Async(environment);
                if (_closed) return;
                // WindowChrome installs its initial region during Show/Loaded, after
                // SourceInitialized. Apply our final silhouette once that has completed.
                _regionBounds = default;
                UpdateWindowRegion();
                var core = _browser.CoreWebView2;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.AreHostObjectsAllowed = false;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsZoomControlEnabled = false;
                core.Settings.IsPasswordAutosaveEnabled = false;
                core.Settings.IsGeneralAutofillEnabled = false;
                core.Settings.IsNonClientRegionSupportEnabled = true;
#if !DEBUG
                core.Settings.AreDevToolsEnabled = false;
#endif
                core.SetVirtualHostNameToFolderMapping("elixr.example", Path.Combine(AppContext.BaseDirectory, "wwwroot"), CoreWebView2HostResourceAccessKind.Deny);
                PlayerImageMapping.Configure(core, imageCacheDirectory);
                core.NavigationStarting += (_, e) => { if (!Trusted(e.Uri)) e.Cancel = true; };
                core.NewWindowRequested += (_, e) => e.Handled = true;
                core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
                core.DownloadStarting += (_, e) => e.Cancel = true;
                core.WebMessageReceived += (_, e) =>
                {
                    if (!Trusted(e.Source) || e.WebMessageAsJson.Length > 4096 || _closed) return;
                    try
                    {
                        using var json = JsonDocument.Parse(e.WebMessageAsJson);
                        var root = json.RootElement;
                        if (!root.TryGetProperty("type", out var type)) return;
                        switch (type.GetString())
                        {
                            case "ready": _ready = true; Ready?.Invoke(this, EventArgs.Empty); break;
                            case "desktopOverlay.close": Close(); break;
                            case "desktopOverlay.resize": OpenResizeEditor(); break;
                            case "desktopOverlay.layout" when root.TryGetProperty("compact", out var compactFlag) && compactFlag.ValueKind is JsonValueKind.True or JsonValueKind.False:
                                var scale = root.TryGetProperty("scale", out var scaleValue) && scaleValue.TryGetDouble(out var number) && double.IsFinite(number) ? Math.Clamp(number, .85, 1.3) : 1;
                                Width = Math.Clamp((compactFlag.GetBoolean() ? 270 : 440) * scale, MinWidth, SystemParameters.VirtualScreenWidth);
                                Left = Math.Clamp(Left, SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width);
                                break;
                            case "profile.copyId":
                            case "profile.openInApp":
                                CommandRequested?.Invoke(this, e); break;
                            case "settings.set" when root.TryGetProperty("key", out var key) && key.GetString() is "compactTags" or "showMaliciousTags" or "showCustomTags" or "showGroupTags":
                                CommandRequested?.Invoke(this, e); break;
                        }
                    }
                    catch (Exception ex) when (ex is JsonException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
                };
                core.Navigate(Origin + "/index.html?surface=desktop-overlay" + (preview ? "&preview=1" : ""));
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                if (_closed) return;
                MessageBox.Show("The desktop overlay could not start. Please try opening it again.", "ELIXR Player List");
                Close();
            }
        };
        Closing += async (_, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;
            if (_finishingClose) return;
            _finishingClose = true; _placementTimer.Stop();
            _resizeEditor?.CancelAndClose(immediate: true);
            if (_savePlacement is not null) await _savePlacement(Placement);
            _allowClose = true; Close();
        };
        Closed += (_, _) => { _closed = true; _ready = false; _placementTimer.Stop(); _resizeEditor?.Close(); _browser.Dispose(); };
    }
    internal void ApplyPlacement(DesktopOverlayBounds value)
    {
        var clamped = DesktopOverlayBounds.Clamp(value, new(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight));
        if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
        _applyingPlacement = true;
        try { Width = clamped.Width; Height = clamped.Height; Left = clamped.Left; Top = clamped.Top; }
        finally { _applyingPlacement = false; }
        _resizeEditor?.RefreshPlacement();
    }
    private void QueuePlacementSave()
    {
        if (_applyingPlacement) return;
        _resizeEditor?.RefreshPlacement();
        if (!IsLoaded || _closed || _finishingClose || _resizeEditor is not null || WindowState != WindowState.Normal) return;
        _placementTimer.Stop(); _placementTimer.Start();
    }
    internal void OpenResizeEditor()
    {
        if (_resizeEditor is not null) { _resizeEditor.Activate(); return; }
        if (_closed || _finishingClose) return;
        if (WindowState != WindowState.Normal) WindowState = WindowState.Normal;
        _placementTimer.Stop();
        var editor = new OverlayResizeWindow(this, _savePlacement ?? (_ => Task.FromResult(false)));
        _resizeEditor = editor;
        editor.Closed += (_, _) => { _resizeEditor = null; };
        editor.Show(); editor.Activate();
    }
    public void PostState(string json)
    {
        if (_ready && !_closed && _browser.CoreWebView2 is { } core && Trusted(core.Source)) core.PostWebMessageAsJson(json);
    }
    private static bool Trusted(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.GetLeftPart(UriPartial.Authority) == Origin;
    private IntPtr WindowMessages(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Defer until Windows/WPF have committed the new pixel bounds and monitor DPI.
        if (message is 0x0005 or 0x02e0) Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(UpdateWindowRegion));
        return IntPtr.Zero;
    }
    private void UpdateWindowRegion()
    {
        if (_closed) return;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var rect)) return;
        var diameter = (int)Math.Round(34 * GetDpiForWindow(handle) / 96d);
        var bounds = (Width: rect.Right - rect.Left, Height: rect.Bottom - rect.Top, Diameter: diameter);
        if (bounds == _regionBounds) return;
        // Clip the HWND itself, including WebView2's child HWND, to the same 17-DIP radius
        // as the CSS border. Opaque browser backing pixels can no longer show outside it.
        var region = CreateRoundRectRgn(0, 0, bounds.Width + 1, bounds.Height + 1, diameter, diameter);
        if (region == IntPtr.Zero) return;
        _regionBounds = bounds; // Set before SetWindowRgn, which can synchronously send window messages.
        if (SetWindowRgn(handle, region, true) == 0) { _regionBounds = default; DeleteObject(region); }
        // Successful SetWindowRgn transfers ownership to Windows.
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, bool redraw);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int ellipseWidth, int ellipseHeight);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
#if DEBUG
    [DllImport("user32.dll")] private static extern int GetWindowRgn(IntPtr hwnd, IntPtr region);
    [DllImport("gdi32.dll")] private static extern bool PtInRegion(IntPtr region, int x, int y);
    internal bool HasClippedCorners
    {
        get
        {
            var handle = new WindowInteropHelper(this).Handle;
            GetWindowRect(handle, out var rect);
            var width = rect.Right - rect.Left; var height = rect.Bottom - rect.Top;
            var region = CreateRoundRectRgn(0, 0, 1, 1, 0, 0);
            try { return GetWindowRgn(handle, region) > 0 && !PtInRegion(region, 0, 0) && !PtInRegion(region, width - 1, 0) && !PtInRegion(region, 0, height - 1) && !PtInRegion(region, width - 1, height - 1) && PtInRegion(region, width / 2, height / 2); }
            finally { DeleteObject(region); }
        }
    }
    internal OverlayResizeWindow? ResizeEditor => _resizeEditor;
    internal bool NativeDragEnabled => _browser.CoreWebView2.Settings.IsNonClientRegionSupportEnabled;
    internal bool IsReady => _ready;
    internal Task<string> InspectAsync(string expression) => _browser.CoreWebView2.ExecuteScriptAsync(expression);
    internal async Task CaptureAsync(string path)
    {
        await using var stream = File.Create(path);
        await _browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
    }
#endif
}
