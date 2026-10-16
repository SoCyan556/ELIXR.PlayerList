#if DEBUG
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using ELIXR.PlayerList.Infrastructure.Services;

namespace ELIXR.PlayerList.Desktop;

// Opt-in local diagnostic: no names, user IDs, cookies, signed URLs or response bodies are written.
internal static class ImageDiagnostics
{
    private sealed class ReadOnlySession : ISessionStore
    {
        public Task<string?> ReadAsync(CancellationToken token = default) => new WindowsSessionStore().ReadAsync(token);
        public Task WriteAsync(string value, CancellationToken token = default) => Task.CompletedTask;
        public Task DeleteAsync(CancellationToken token = default) => Task.CompletedTask;
    }
    private sealed class TraceHandler(HttpMessageHandler inner, List<object> events) : DelegatingHandler(inner)
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var response = await base.SendAsync(request, token);
            var uri = request.RequestUri!;
            var profile = uri.AbsolutePath.Contains("/users/");
            events.Add(new { stage = profile ? "profile" : uri.AbsolutePath.Contains("/auth/") ? "session" : "image", host = uri.Host, status = (int)response.StatusCode, redirectHost = response.Headers.Location is { IsAbsoluteUri: true } location ? location.Host : null });
            if (profile && response.IsSuccessStatusCode)
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                events.Add(new { fields = json.RootElement.EnumerateObject().Select(p => p.Name).ToArray(), idMatches = TagFeedParser.Text(json.RootElement, "id") == uri.Segments.Last() });
                foreach (var key in new[] { "iconUrl", "userIcon", "profilePicOverrideThumbnail", "profilePicOverride", "currentAvatarThumbnailImageUrl", "currentAvatarImageUrl" })
                {
                    var value = TagFeedParser.Text(json.RootElement, key);
                    if (Uri.TryCreate(value, UriKind.Absolute, out var candidate))
                        events.Add(new { field = key, host = candidate.Host, allowed = VrchatProfileImageService.AllowedImageUri(candidate) });
                }
            }
            return response;
        }
    }
    internal static async Task RunAsync(string output)
    {
        var events = new List<object>();
        try
        {
            var cookies = new CookieContainer();
            using var api = new HttpClient(new TraceHandler(new HttpClientHandler { CookieContainer = cookies, AllowAutoRedirect = false }, events));
            using var cdn = new HttpClient(new TraceHandler(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }, events));
            var auth = new VrchatAuthenticationService(api, cookies, new ReadOnlySession());
            var restored = await auth.RestoreSessionAsync();
            events.Add(new { authentication = restored.Status.ToString() });
            if (auth.CurrentUser is not null)
            {
                await using var roster = new LocalVrchatLogPlayerListService();
                await roster.StartAsync();
                var images = new VrchatProfileImageService(api, cdn, () => auth.CurrentUser.VrchatUserId);
                foreach (var player in roster.Current.Players.Where(p => p.HasCompleteIdentity && p.VrchatUserId != auth.CurrentUser.VrchatUserId).Take(3))
                {
                    events.Add(new { resolved = await images.GetAsync(player.VrchatUserId) is not null });
                    await Task.Delay(1100);
                }
            }
        }
        catch (Exception ex) { events.Add(new { errorType = ex.GetType().Name }); }
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(events, new JsonSerializerOptions { WriteIndented = true }));
    }
}
#endif
