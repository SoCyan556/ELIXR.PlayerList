using System.Net;
using System.Text.Json;
using ELIXR.PlayerList.Core.Models;

namespace ELIXR.PlayerList.Infrastructure.Services;

public sealed record UserSearchResult(string VrchatUserId, string DisplayName, IReadOnlyList<PlayerTag> Tags, bool TagsChecked = false, string? AvatarUrl = null);
public sealed record UserSearchPage(IReadOnlyList<UserSearchResult> Users, bool HasMore);

// Uses only the existing authenticated, non-redirecting VRChat client. No search history is persisted.
public sealed class VrchatUserSearchService(HttpClient api, Func<string?> accountId)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _nextRequest;
    public async Task<UserSearchPage> SearchAsync(string query, int page, CancellationToken token = default)
    {
        query = query.Trim();
        if (query.Length is < 2 or > 64 || query.Any(char.IsControl) || page is < 0 or > 49)
            throw new InvalidOperationException("Enter a name between 2 and 64 characters.");
        var account = accountId() ?? throw new InvalidOperationException("Sign in to VRChat before searching.");
        await _gate.WaitAsync(token);
        try
        {
            if (DateTimeOffset.UtcNow < _nextRequest) throw new InvalidOperationException("VRChat search is cooling down. Please wait a moment and retry.");
            _nextRequest = DateTimeOffset.UtcNow.AddSeconds(2);
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.vrchat.cloud/api/1/users?search=" + Uri.EscapeDataString(query) + "&n=20&offset=" + (page * 20));
            request.Headers.UserAgent.ParseAdd("ELIXR-PlayerList/0.1.2");
            using var response = await api.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var delay = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromMinutes(1);
                _nextRequest = DateTimeOffset.UtcNow + (delay > TimeSpan.FromSeconds(5) ? delay : TimeSpan.FromSeconds(5));
                throw new InvalidOperationException("VRChat is rate limiting search. Please wait before trying again.");
            }
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new InvalidOperationException("Your VRChat session could not search. Please sign out and sign in again.");
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > 2 * 1024 * 1024) throw new IOException("Search response is too large.");
            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192]; int read;
            while ((read = await stream.ReadAsync(chunk, token)) > 0) { if (buffer.Length + read > 2 * 1024 * 1024) throw new IOException("Search response is too large."); buffer.Write(chunk, 0, read); }
            using var json = JsonDocument.Parse(buffer.ToArray());
            if (json.RootElement.ValueKind != JsonValueKind.Array) throw new JsonException("Invalid search response.");
            if (accountId() != account) throw new OperationCanceledException(token);
            var users = json.RootElement.EnumerateArray().Take(20).Select(user => new UserSearchResult(TagFeedParser.Text(user, "id"), TagFeedParser.Text(user, "displayName"), []))
                .Where(user => TagFeedParser.ValidId(user.VrchatUserId, "usr_") && user.DisplayName.Length is > 0 and <= 256)
                .DistinctBy(user => user.VrchatUserId, StringComparer.OrdinalIgnoreCase).ToArray();
            return new(users, json.RootElement.GetArrayLength() >= 20 && page < 49);
        }
        finally { _gate.Release(); }
    }
}
