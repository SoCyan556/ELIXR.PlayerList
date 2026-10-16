using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Threading;
using ELIXR.PlayerList.Core.Services;
using ELIXR.PlayerList.Core.Models;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;

namespace ELIXR.PlayerList.Desktop;

public partial class MainWindow : Window
{
    private bool IsUiPreview
    {
        get
        {
#if DEBUG
            return Environment.GetCommandLineArgs().Contains("--ui-preview");
#else
            return false;
#endif
        }
    }
    private const string AppOrigin = "https://elixr.example";
    private readonly IPlayerListService _players;
    private readonly ISettingsService _settings;
    private readonly IOverlayService? _overlay;
    private readonly ELIXR.PlayerList.Infrastructure.Services.GitHubTagService? _tagFeeds;
    private readonly SemaphoreSlim _settingsLock = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private bool _ready, _closing, _pushQueued;
    private readonly IAuthenticationService _authentication;
    private AuthenticationResult _authResult = new(AuthenticationStatus.SignedOut);
    private readonly SemaphoreSlim _authLock = new(1, 1);
    private CancellationTokenSource? _authCancellation;
    private bool _authBusy, _restoring, _playerStarted;
    private CoreWebView2Environment? _webEnvironment;
    private DesktopOverlayWindow? _desktopOverlay;
    private Rect? _desktopOverlayPlacement;
    private readonly string? _imageCacheDirectory;

    public MainWindow(IPlayerListService players, ISettingsService settings, IAuthenticationService authentication, IOverlayService? overlay = null, ELIXR.PlayerList.Infrastructure.Services.GitHubTagService? tagFeeds = null, string? imageCacheDirectory = null, ELIXR.PlayerList.Infrastructure.Services.GitHubUpdateService? updates = null)
    {
        InitializeComponent();
        _players = players;
        _settings = settings;
        _overlay = overlay;
        _tagFeeds = tagFeeds;
        _imageCacheDirectory = imageCacheDirectory;
        _authentication = authentication;
        _updates = updates;
        SourceInitialized += (_, _) =>
        {
            var dark = 1;
            DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref dark, sizeof(int));
        };
        Loaded += InitializeAsync;
        _players.Changed += PlayersChanged;
        Closed += ShutdownAsync;
    }

    private static bool IsTrusted(string uri) => Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
        && parsed.GetLeftPart(UriPartial.Authority) == AppOrigin;

    private async void InitializeAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            var content = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            if (!File.Exists(Path.Combine(content, "index.html"))) throw new FileNotFoundException("Build the Vue frontend before running the desktop host.");
            var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ELIXR", "PlayerList", "WebView2");
            var environment = await CoreWebView2Environment.CreateAsync(null, userData);
            _webEnvironment = environment;
            if (_closing) return;
            await Browser.EnsureCoreWebView2Async(environment);
            var core = Browser.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
#if !DEBUG
            core.Settings.AreDevToolsEnabled = false;
#endif
            core.SetVirtualHostNameToFolderMapping("elixr.example", content, CoreWebView2HostResourceAccessKind.Deny);
            PlayerImageMapping.Configure(core, _imageCacheDirectory);
            core.NavigationStarting += (_, args) => { if (!IsTrusted(args.Uri)) args.Cancel = true; };
            core.NewWindowRequested += (_, args) => args.Handled = true;
            core.PermissionRequested += (_, args) => args.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (_, args) => args.Cancel = true;
            core.WebMessageReceived += MessageReceivedAsync;
            core.Navigate(AppOrigin + "/index.html" + (IsUiPreview ? "?preview=1" : ""));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            MessageBox.Show("The desktop interface could not start. Install the Microsoft Edge WebView2 Runtime and rebuild the frontend.\n\n" + ex.Message, "ELIXR Player List", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void PlayersChanged(object? sender, Core.Models.InstanceState instance)
    {
        if (_closing || Dispatcher.HasShutdownStarted) return;
        Dispatcher.BeginInvoke(() =>
        {
            if (_pushQueued || _closing) return;
            _pushQueued = true;
            Dispatcher.BeginInvoke(() => { _pushQueued = false; PushState(); }, DispatcherPriority.Background);
        });
    }

    private void Post(object payload)
    {
        if (_closing) return;
        var json = JsonSerializer.Serialize(payload, _json);
        if (_ready && Browser.CoreWebView2 is { } core && IsTrusted(core.Source)) core.PostWebMessageAsJson(json);
        _desktopOverlay?.PostState(json);
    }

    private void PushState()
    {
        if (!IsUiPreview && _authentication.CurrentUser is null) _desktopOverlay?.Close();
        var current = _players.Current;
        Post(new { type = "state", payload = new {
            connected = current.IsActive,
            instance = new { current.WorldId, current.WorldName, current.InstanceId, current.IsActive },
            players = _authentication.CurrentUser is not null ? current.Players : [],
            settings = _settings.Current,
            tagFeeds = _tagFeeds?.Status,
            updates = UpdateState,
            desktopOverlay = new { visible = _desktopOverlay is not null },
            auth = new { status = _authResult.Status.ToString(), user = _authResult.User, verificationMethods = _authResult.VerificationMethods, message = _authResult.Message, busy = _authBusy, restoring = _restoring },
            capabilities = new { overlay = _overlay?.IsAvailable == true, desktopOverlay = true, history = false, authentication = _authentication.CurrentUser is not null }
        }});
    }

    private async void MessageReceivedAsync(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!IsTrusted(e.Source) || e.WebMessageAsJson.Length > 4096) return;
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            var type = root.GetProperty("type").GetString();
            if (type is not ("ready" or "auth.login" or "auth.verify" or "auth.restore" or "auth.logout" or "updates.check" or "updates.later" or "updates.install") && !IsUiPreview && _authentication.CurrentUser is null)
                throw new InvalidOperationException("Sign in first.");
            switch (type)
            {
                case "ready":
                    _ready = true;
                    if (IsUiPreview) PushState();
                    else await RunAuthAsync(token => _authentication.RestoreSessionAsync(token), true);
                    if (!_startupUpdateChecked && !IsUiPreview && !Environment.GetCommandLineArgs().Contains("--smoke-test"))
                    {
                        _startupUpdateChecked = true;
                        if (_settings.Current.CheckUpdatesOnStartup) _ = CheckForUpdatesAsync();
                    }
                    await RunSmokeTestAsync();
#if DEBUG
                    await CaptureUiStatesAsync();
#endif
                    break;
                case "updates.check": await CheckForUpdatesAsync(); break;
                case "updates.install": await InstallUpdateAsync(); break;
                case "updates.later": if (!_updateBusy) { _updatePrompt = false; PushState(); } break;
                case "auth.login":
                    if (!IsUiPreview)
                    {
                        var username = root.GetProperty("username").GetString() ?? "";
                        var password = root.GetProperty("password").GetString() ?? "";
                        var remember = root.GetProperty("remember").GetBoolean();
                        await RunAuthAsync(token => _authentication.LoginAsync(username, password, remember, token));
                    }
                    break;
                case "auth.verify":
                    if (!IsUiPreview)
                    {
                        var method = root.GetProperty("method").GetString() ?? "";
                        var code = root.GetProperty("code").GetString() ?? "";
                        await RunAuthAsync(token => _authentication.VerifyAsync(method, code, token));
                    }
                    break;
                case "auth.restore":
                    if (!IsUiPreview) await RunAuthAsync(token => _authentication.RestoreSessionAsync(token), true);
                    break;
                case "auth.logout":
                    if (!IsUiPreview) await SignOutAsync();
                    break;
                case "settings.set":
                    await _settingsLock.WaitAsync();
                    try
                    {
                        var key = root.GetProperty("key").GetString();
                        var previous = JsonSerializer.SerializeToElement(_settings.Current, _json);
                        ApplySetting(key, root.GetProperty("value"));
                        try { await _settings.SaveAsync(); }
                        catch
                        {
                            ApplySetting(key, previous.GetProperty(key!));
                            throw;
                        }
                        PushState();
                    }
                    finally { _settingsLock.Release(); }
                    break;
                case "overlay.visibility":
                    if (_overlay?.IsAvailable != true) throw new InvalidOperationException("The VR overlay is unavailable.");
                    await _overlay.SetVisibleAsync(root.GetProperty("visible").GetBoolean());
                    PushState();
                    break;
                case "desktopOverlay.visibility":
                    SetDesktopOverlayVisible(root.GetProperty("visible").GetBoolean());
                    PushState();
                    break;
                case "profile.copyId":
                    var copiedId = ValidatedUserId(root);
                    Clipboard.SetText(copiedId);
                    Post(new { type = "profile.copied", userId = copiedId });
                    break;
                case "group.copyCode":
                    var groupUserId = ValidatedUserId(root);
                    var groupId = root.GetProperty("groupId").GetString() ?? "";
                    if (!ELIXR.PlayerList.Infrastructure.Services.TagFeedParser.ValidId(groupId, "grp_")) throw new InvalidOperationException("Invalid group ID.");
                    var groupCode = _players.Current.Players.FirstOrDefault(p => p.VrchatUserId == groupUserId)?.Tags
                        .FirstOrDefault(t => t.Category == TagCategory.Group && t.GroupId == groupId && VrchatGroupCode.IsValid(t.GroupCode))?.GroupCode;
                    if (!VrchatGroupCode.IsValid(groupCode))
                    {
                        Post(new { type = "error", message = "This group's code is not available yet." });
                        break;
                    }
                    Clipboard.SetText(groupCode!);
                    Post(new { type = "group.copied", userId = groupUserId, groupId });
                    break;
                case "profile.openInApp":
                    var selectedId = ValidatedUserId(root);
                    if (!IsUiPreview && !_players.Current.Players.Any(p => p.VrchatUserId == selectedId))
                    {
                        Post(new { type = "error", message = "This player has left the current instance." });
                        break;
                    }
                    Show();
                    if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                    Activate();
                    Browser.Focus();
                    Post(new { type = "profile.focus", userId = selectedId });
                    break;
                case "profile.open":
                    var userId = ValidatedUserId(root);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://vrchat.com/home/user/" + userId) { UseShellExecute = true });
                    break;
                default: throw new InvalidOperationException("Unsupported desktop command.");
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException or IOException or UnauthorizedAccessException or FormatException or COMException)
        {
            PushState();
            Post(new { type = "error", message = "The requested change could not be applied." });
        }
    }

    private void ApplySetting(string? key, JsonElement value)
    {
        var s = _settings.Current;
        switch (key)
        {
            case "showMaliciousTags": s.ShowMaliciousTags = value.GetBoolean(); break;
            case "showCustomTags": s.ShowCustomTags = value.GetBoolean(); break;
            case "showGroupTags": s.ShowGroupTags = value.GetBoolean(); break;
            case "compactTags": s.CompactTags = value.GetBoolean(); break;
            case "uiScale": s.UiScale = Math.Clamp(value.GetDouble(), .85, 1.3); break;
            case "overlayOpacity": s.OverlayOpacity = Math.Clamp(value.GetDouble(), .3, 1); break;
            case "checkUpdatesOnStartup": s.CheckUpdatesOnStartup = value.GetBoolean(); break;
            default: throw new InvalidOperationException("Unsupported setting.");
        }
    }

    private void SetDesktopOverlayVisible(bool visible)
    {
        if (!visible) { _desktopOverlay?.Close(); return; }
        if (_desktopOverlay is not null || _webEnvironment is null || _closing) return;
        if (!IsUiPreview && _authentication.CurrentUser is null) return;
        var saved = _settings.Current.DesktopOverlayPlacement;
        if (saved is not null) saved = DesktopOverlayBounds.Clamp(saved, new(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight));
        var placement = _desktopOverlayPlacement ?? (saved is null ? (Rect?)null : new Rect(saved.Left, saved.Top, Math.Max(0, saved.Width), Math.Max(0, saved.Height)));
        var window = new DesktopOverlayWindow(_webEnvironment, IsUiPreview, placement, _imageCacheDirectory, SaveDesktopOverlayPlacementAsync);
        _desktopOverlay = window;
        window.Ready += (_, _) => PushState();
        window.CommandRequested += MessageReceivedAsync;
        window.Closing += (_, _) =>
        {
            var bounds = window.WindowState == WindowState.Normal ? new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight) : window.RestoreBounds;
            if (!bounds.IsEmpty) _desktopOverlayPlacement = bounds;
        };
        window.Closed += (_, _) =>
        {
            _desktopOverlay = null;
            if (!_closing) PushState();
        };
        window.Show();
    }

    private async Task<bool> SaveDesktopOverlayPlacementAsync(DesktopOverlayBounds bounds)
    {
        var clamped = DesktopOverlayBounds.Clamp(bounds, new(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight));
        await _settingsLock.WaitAsync();
        try
        {
            var previous = _settings.Current.DesktopOverlayPlacement;
            _settings.Current.DesktopOverlayPlacement = clamped;
            try { await _settings.SaveAsync(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _settings.Current.DesktopOverlayPlacement = previous;
                Post(new { type = "error", message = "The overlay layout could not be saved. Please try again." });
                return false;
            }
            _desktopOverlayPlacement = new Rect(clamped.Left, clamped.Top, clamped.Width, clamped.Height);
            return true;
        }
        finally { _settingsLock.Release(); }
    }

    private async Task RunAuthAsync(Func<CancellationToken, Task<AuthenticationResult>> action, bool restoring = false)
    {
        if (!await _authLock.WaitAsync(0)) return;
        _authCancellation = new CancellationTokenSource();
        _authBusy = true;
        _restoring = restoring;
        PushState();
        try
        {
            _authResult = await action(_authCancellation.Token);
            if (_authResult.Status == AuthenticationStatus.SignedIn && !_playerStarted)
            {
                try { await _players.StartAsync(); _playerStarted = true; }
                catch (IOException) { Post(new { type = "error", message = "Signed in. VRChat logs are currently unavailable." }); }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException or OperationCanceledException)
        { _authResult = new(AuthenticationStatus.Failed, Message: "The session could not be opened securely. Please sign in again."); }
        finally
        {
            _authBusy = false;
            _restoring = false;
            _authCancellation.Dispose();
            _authCancellation = null;
            _authLock.Release();
            PushState();
        }
    }

    private async Task SignOutAsync()
    {
        _desktopOverlay?.Close();
        _authCancellation?.Cancel();
        await _authLock.WaitAsync();
        _authBusy = true;
        PushState();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await _authentication.LogoutAsync(timeout.Token);
            _authResult = new(AuthenticationStatus.SignedOut);
        }
        finally { _authResult = new(AuthenticationStatus.SignedOut); _authBusy = false; _authLock.Release(); PushState(); }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private static string ValidatedUserId(JsonElement root)
    {
        var id = root.GetProperty("userId").GetString() ?? "";
        if (!id.StartsWith("usr_", StringComparison.Ordinal) || id.Length != 40 || !Guid.TryParseExact(id[4..], "D", out _))
            throw new InvalidOperationException("Invalid VRChat user ID.");
        return id;
    }

    // Opt-in integration diagnostic: exercises the packaged Vue app in the actual WebView2 runtime.
    private async Task RunSmokeTestAsync()
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, "--smoke-test");
        if (index < 0 || index + 1 >= args.Length) return;
        if (_authentication.CurrentUser is null)
        {
            await Task.Delay(650);
            var loginResult = await Browser.CoreWebView2.ExecuteScriptAsync("""
                JSON.stringify({passed: !!document.querySelector('.login-screen') &&
                    document.querySelector('h1')?.textContent === 'Sign in to VRChat' &&
                    !!document.querySelector('input[autocomplete="current-password"]') &&
                    !!document.querySelector('.remember input') &&
                    !document.querySelector('.app-shell'),
                    screen:'sign-in'})
                """);
            await File.WriteAllTextAsync(args[index + 1], JsonSerializer.Deserialize<string>(loginResult));
            await using (var image = File.Create(args[index + 1] + ".png"))
                await Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, image);
            Close();
            return;
        }
        await Task.Delay(700);
        // ExecuteScriptAsync does not await JS promises; poll the diagnostic result after completion.
        await Browser.CoreWebView2.ExecuteScriptAsync("""
            window.__elixrSmoke = null;
            (async () => {
                const checks = [];
                const delay = () => new Promise(r => setTimeout(r, 80));
                for (const name of ['Home','Recent','Settings','Players']) {
                    [...document.querySelectorAll('nav button')].find(b => b.textContent.trim() === name).click();
                    await delay();
                    checks.push(document.querySelector('h1')?.textContent === ({Players:'Player List',Recent:'Recent Players'}[name] || name));
                }
                checks.push(document.querySelectorAll('.tag-controls [role=switch]').length === 4);
                checks.push(!document.querySelector('.notice'));
                async function toggleCompact() {
                    return await new Promise(resolve => {
                        const button = [...document.querySelectorAll('.tag-controls button')].find(b => b.textContent.includes('Compact Tags'));
                        const wanted = button.getAttribute('aria-checked') !== 'true';
                        const timeout = setTimeout(() => { window.chrome.webview.removeEventListener('message', listener); resolve(false); }, 1800);
                        const listener = ({data}) => {
                            if (data.type !== 'state' || data.payload.settings.compactTags !== wanted) return;
                            clearTimeout(timeout);
                            window.chrome.webview.removeEventListener('message', listener);
                            resolve(true);
                        };
                        window.chrome.webview.addEventListener('message', listener);
                        button.click();
                    });
                }
                checks.push(await toggleCompact());
                checks.push(await toggleCompact());
                window.__elixrSmoke = { passed: checks.every(Boolean), checks };
            })()
            """);
        for (var attempt = 0; attempt < 60; attempt++)
        {
            if (await Browser.CoreWebView2.ExecuteScriptAsync("window.__elixrSmoke !== null") == "true") break;
            await Task.Delay(100);
        }
        var result = await Browser.CoreWebView2.ExecuteScriptAsync("window.__elixrSmoke");
        await File.WriteAllTextAsync(args[index + 1], result);
        await using (var image = File.Create(args[index + 1] + ".png"))
            await Browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, image);
        Close();
    }

    private async void ShutdownAsync(object? sender, EventArgs e)
    {
        _closing = true;
        _updateLifetime.Cancel();
        _desktopOverlay?.Close();
        _authCancellation?.Cancel();
        _players.Changed -= PlayersChanged;
        await _players.DisposeAsync();
        Browser.Dispose();
    }
    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
}

