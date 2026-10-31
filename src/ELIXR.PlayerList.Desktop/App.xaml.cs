using System.Windows;
using ELIXR.PlayerList.Infrastructure.Services;
namespace ELIXR.PlayerList.Desktop;
public partial class App : Application
{
    private System.Net.Http.HttpClient? _http;
    private System.Net.Http.HttpClient? _feedsHttp;
    private Mutex? _instanceMutex;
    private bool _ownsInstanceMutex;
    private async void OnStartup(object sender, StartupEventArgs e)
    {
        // WPF only draws the small native host/editor. Avoid a driver-dependent
        // composition-channel stall before Loaded; the HWND-based WebView2 keeps
        // its own normal rendering pipeline and CSS animations.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
#if DEBUG
        var imageDiagnostic = Array.IndexOf(e.Args, "--diagnose-images");
        if (imageDiagnostic >= 0 && imageDiagnostic + 1 < e.Args.Length)
        {
            await ImageDiagnostics.RunAsync(e.Args[imageDiagnostic + 1]);
            Shutdown(); return;
        }
#endif
        var smokeIndex = Array.IndexOf(e.Args, "--smoke-test");
        if (smokeIndex < 0 && !e.Args.Contains("--ui-preview"))
        {
            var user = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
            _instanceMutex = new Mutex(true, "Local\\ELIXR.PlayerList." + user, out _ownsInstanceMutex);
            if (!_ownsInstanceMutex)
            {
                var existing = FindWindow(null, "ELIXR Player List");
                if (existing != IntPtr.Zero) { ShowWindowAsync(existing, 9); SetForegroundWindow(existing); }
                Shutdown(); return;
            }
        }
        string? settingsPath = smokeIndex >= 0 && smokeIndex + 1 < e.Args.Length ? e.Args[smokeIndex + 1] + ".settings.json" : null;
#if DEBUG
        if (e.Args.Contains("--ui-preview")) settingsPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "elixr-ui-preview-settings.json");
#endif
        var settings = new JsonSettingsService(settingsPath);
        try { await settings.LoadAsync(); }
        catch (Exception ex) when (ex is System.IO.IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            MessageBox.Show("Saved settings could not be read. Default settings will be used.", "ELIXR Player List");
        }
        var cookies = new System.Net.CookieContainer();
        _http = new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
        var sessionPath = settingsPath is null ? null : settingsPath + ".session.bin";
        var authentication = new VrchatAuthenticationService(_http, cookies, new WindowsSessionStore(sessionPath));
        // Public feeds never receive VRChat cookies or account credentials.
        _feedsHttp = new System.Net.Http.HttpClient(new System.Net.Http.HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
        var tags = new GitHubTagService(_feedsHttp, _http, () => authentication.CurrentUser?.VrchatUserId);
        var recent = new RecentPlayersService(() => authentication.CurrentUser?.VrchatUserId, settingsPath is null ? null : settingsPath + ".history");
        var roster = new LocalVrchatLogPlayerListService();
        roster.Encountered += (_, player) => recent.Observe(player);
        var taggedPlayers = new TaggedPlayerListService(roster, tags, () => authentication.CurrentUser is not null);
        var images = new VrchatProfileImageService(_http, _feedsHttp, () => authentication.CurrentUser?.VrchatUserId);
        var players = new ProfileImagePlayerListService(taggedPlayers, images, () => authentication.CurrentUser is not null, () => authentication.CurrentUser?.VrchatUserId);
        players.Changed += (_, current) => { foreach (var player in current.Players) recent.Observe(player); };
        StartupDiagnostics.SmokeCheckpoint("native-window");
        MainWindow = new MainWindow(players, settings, authentication, tagFeeds: tags, imageCacheDirectory: images.CacheDirectory, updates: new GitHubUpdateService(_feedsHttp)) { UsageRegistration = settingsPath is null ? new UsageRegistrationService(_feedsHttp) : null, UserSearch = new VrchatUserSearchService(_http, () => authentication.CurrentUser?.VrchatUserId), SearchImages = images, RecentPlayers = recent };
        MainWindow.Show();
    }
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string windowName);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr hwnd, int command);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    protected override void OnExit(ExitEventArgs e)
    {
        _http?.Dispose(); _feedsHttp?.Dispose();
        if (_ownsInstanceMutex) _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}

