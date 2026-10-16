using System.IO;
using System.Diagnostics;
using System.Text.Json;
using ELIXR.PlayerList.Infrastructure.Services;

namespace ELIXR.PlayerList.Desktop;

public partial class MainWindow
{
    private GitHubUpdateService? _updates;
    private AvailableUpdate? _availableUpdate;
    private readonly CancellationTokenSource _updateLifetime = new();
    private bool _updateBusy, _updatePrompt, _startupUpdateChecked;
    private string _updateMessage = "Updates are checked against the official GitHub releases.";
    private static string CurrentVersion => typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.1.0";
    private object UpdateState => new { currentVersion = CurrentVersion, availableVersion = _availableUpdate?.Version, prompt = _updatePrompt, busy = _updateBusy, message = _updateMessage, enabled = _updates is not null };
    private async Task CheckForUpdatesAsync()
    {
        if (_updates is null || _updateBusy || IsUiPreview) return;
        _updateBusy = true; _updateMessage = "Checking for updates…"; PushState();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_updateLifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            _availableUpdate = await _updates.CheckAsync(Version.Parse(CurrentVersion), timeout.Token);
            _updatePrompt = _availableUpdate is not null;
            _updateMessage = _availableUpdate is null ? "You're up to date." : $"Version {_availableUpdate.Version} is available.";
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or IOException or InvalidDataException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OperationCanceledException)
        { _updateMessage = "Couldn't check for updates. Please try again later."; }
        finally { _updateBusy = false; PushState(); }
    }
    private async Task InstallUpdateAsync()
    {
        if (_updates is null || _availableUpdate is null || _updateBusy || IsUiPreview) return;
        _updateBusy = true; _updatePrompt = true; _updateMessage = "Downloading and verifying the installer…"; PushState();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_updateLifetime.Token);
            timeout.CancelAfter(TimeSpan.FromMinutes(10));
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ELIXR", "PlayerList", "Updates");
            var path = await _updates.DownloadAsync(_availableUpdate, directory, timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            Close(); // The installer presents its normal confirmation UI; it is never run silently.
        }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or OperationCanceledException or System.ComponentModel.Win32Exception or System.Security.Cryptography.CryptographicException)
        { _updateMessage = "The update couldn't be verified or started. Nothing was installed. Try again later."; }
        finally { _updateBusy = false; PushState(); }
    }
}
