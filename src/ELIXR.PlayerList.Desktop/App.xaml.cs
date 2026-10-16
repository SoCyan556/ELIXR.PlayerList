using System.Windows;
using ELIXR.PlayerList.Infrastructure.Services;
namespace ELIXR.PlayerList.Desktop;
public partial class App : Application
{
    private System.Net.Http.HttpClient? _http;
    private System.Net.Http.HttpClient? _feedsHttp;
    private async void OnStartup(object sender, StartupEventArgs e)
    {
#if DEBUG
        var imageDiagnostic = Array.IndexOf(e.Args, "--diagnose-images");
        if (imageDiagnostic >= 0 && imageDiagnostic + 1 < e.Args.Length)
        {
            await ImageDiagnostics.RunAsync(e.Args[imageDiagnostic + 1]);
            Shutdown(); return;
        }
#endif
        var smokeIndex = Array.IndexOf(e.Args, "--smoke-test");
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
        var taggedPlayers = new TaggedPlayerListService(new LocalVrchatLogPlayerListService(), tags, () => authentication.CurrentUser is not null);
        var images = new VrchatProfileImageService(_http, _feedsHttp, () => authentication.CurrentUser?.VrchatUserId);
        var players = new ProfileImagePlayerListService(taggedPlayers, images, () => authentication.CurrentUser is not null);
        MainWindow = new MainWindow(players, settings, authentication, tagFeeds: tags, imageCacheDirectory: images.CacheDirectory, updates: new GitHubUpdateService(_feedsHttp));
        MainWindow.Show();
    }
    protected override void OnExit(ExitEventArgs e) { _http?.Dispose(); _feedsHttp?.Dispose(); base.OnExit(e); }
}

