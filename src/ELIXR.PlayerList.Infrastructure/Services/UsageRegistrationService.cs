using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace ELIXR.PlayerList.Infrastructure.Services;

/// <summary>One-time, best-effort first-use registration. Never sends VRChat credentials or rosters.</summary>
public sealed class UsageRegistrationService(HttpClient publicHttp, string? markerDirectory = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _directory = markerDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ELIXR", "PlayerList", "UsageRegistration");
    public async Task RegisterAsync(string userId)
    {
        if (!userId.StartsWith("usr_", StringComparison.OrdinalIgnoreCase) || userId.Length != 40 || !Guid.TryParseExact(userId[4..], "D", out _)) return;
        await _gate.WaitAsync();
        try
        {
            var id = userId.ToLowerInvariant();
            var marker = Path.Combine(_directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id))));
            if (File.Exists(marker)) return;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var response = await publicHttp.PostAsJsonAsync("https://elixrvr.com/api/player-list/usage", new { vrchatUserId = id }, timeout.Token);
            if (!response.IsSuccessStatusCode) return;
            Directory.CreateDirectory(_directory);
            await File.WriteAllTextAsync(marker, "registered", timeout.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or OperationCanceledException or ObjectDisposedException)
        { /* Offline or unavailable: retry on the next sign-in, without blocking the player list. */ }
        finally { _gate.Release(); }
    }
}
