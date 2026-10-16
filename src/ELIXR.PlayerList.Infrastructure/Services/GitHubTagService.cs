using System.Net;
using System.Text.Json;
using ELIXR.PlayerList.Core.Models;
using ELIXR.PlayerList.Core.Services;

namespace ELIXR.PlayerList.Infrastructure.Services;

public sealed record TagFeedStatus(string Name, string Url, string State, int Records);

public sealed class GitHubTagService(HttpClient feedsHttp, HttpClient vrchatHttp, Func<string?> accountId, string? cacheDirectory = null) : ITagService
{
    public static readonly (string Name, string Url)[] Sources = [
        ("FewTags", "https://raw.githubusercontent.com/Fewdys/FewTags/refs/heads/main/FewTags.json"),
        ("ETags", "https://raw.githubusercontent.com/SoCyan556/ETags/refs/heads/main/ETags.json"),
        ("Groups", "https://raw.githubusercontent.com/SoCyan556/etags-groups/refs/heads/main/groups.json")];
    private readonly string _cache = cacheDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ELIXR", "PlayerList", "TagFeeds");
    private readonly SemaphoreSlim _refreshLock = new(1, 1), _groupsLock = new(1, 1);
    private readonly Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<PlayerTag>>> _catalogs = [];
    private sealed record GroupMembership(string Id, string? Name, string? Code);
    private readonly Dictionary<string, (DateTimeOffset Expires, GroupMembership[] Groups)> _memberships = [];
    private DateTimeOffset _nextRefresh, _nextGroupRequest;
    private string? _groupAccount;
    private TagFeedStatus[] _status = Sources.Select(s => new TagFeedStatus(s.Name, s.Url, "Not loaded", 0)).ToArray();
    public IReadOnlyList<TagFeedStatus> Status => _status;

    public async Task RefreshAsync(CancellationToken token = default)
    {
        await _refreshLock.WaitAsync(token);
        try
        {
            if (DateTimeOffset.UtcNow < _nextRefresh) return;
            var statuses = new List<TagFeedStatus>();
            var failed = false;
            foreach (var source in Sources)
            {
                var state = "Live";
                var path = Path.Combine(_cache, source.Name + ".json");
                try
                {
                    using var download = CancellationTokenSource.CreateLinkedTokenSource(token);
                    download.CancelAfter(TimeSpan.FromSeconds(30));
                    var fetchToken = download.Token;
                    using var request = new HttpRequestMessage(HttpMethod.Get, source.Url);
                    request.Headers.UserAgent.ParseAdd("ELIXR-PlayerList/0.1.0");
                    using var response = await feedsHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, fetchToken);
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength > 32 * 1024 * 1024) throw new IOException("Tag feed is too large.");
                    await using var stream = await response.Content.ReadAsStreamAsync(fetchToken);
                    using var memory = new MemoryStream();
                    var buffer = new byte[32768];
                    int read;
                    while ((read = await stream.ReadAsync(buffer, fetchToken)) > 0)
                    {
                        if (memory.Length + read > 32 * 1024 * 1024) throw new IOException("Tag feed is too large.");
                        memory.Write(buffer, 0, read);
                    }
                    var json = System.Text.Encoding.UTF8.GetString(memory.ToArray());
                    _catalogs[source.Name] = TagFeedParser.Parse(json.TrimStart('\uFEFF'), source.Name);
                    try
                    {
                        Directory.CreateDirectory(_cache);
                        await File.WriteAllTextAsync(path + ".tmp", json, token);
                        File.Move(path + ".tmp", path, true);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* In-memory feed remains usable. */ }
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or TaskCanceledException)
                {
                    token.ThrowIfCancellationRequested();
                    failed = true;
                    state = "Unavailable";
                    if (!_catalogs.ContainsKey(source.Name))
                    {
                        try
                        {
                            if (new FileInfo(path) is { Exists: true, Length: <= 33554432 })
                                _catalogs[source.Name] = TagFeedParser.Parse(await File.ReadAllTextAsync(path, token), source.Name);
                        }
                        catch (Exception cacheError) when (cacheError is IOException or UnauthorizedAccessException or JsonException) { }
                    }
                    if (_catalogs.ContainsKey(source.Name)) state = "Cached · refresh failed";
                }
                statuses.Add(new(source.Name, source.Url, state, _catalogs.GetValueOrDefault(source.Name)?.Count ?? 0));
            }
            _status = statuses.ToArray();
            _nextRefresh = DateTimeOffset.UtcNow.AddSeconds(failed ? 60 : 600);
        }
        finally { _refreshLock.Release(); }
    }

    public async Task<IReadOnlyList<PlayerTag>> GetTagsAsync(string vrchatUserId, CancellationToken cancellationToken = default)
    {
        if (!TagFeedParser.ValidId(vrchatUserId, "usr_")) return [];
        await RefreshAsync(cancellationToken);
        var tags = new List<PlayerTag>();
        IReadOnlyDictionary<string, IReadOnlyList<PlayerTag>>? groups;
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Match the original app: ETags wins duplicate labels over FewTags.
            foreach (var source in new[] { "ETags", "FewTags" })
                if (_catalogs.GetValueOrDefault(source)?.TryGetValue(vrchatUserId, out var values) == true) tags.AddRange(values);
            groups = _catalogs.GetValueOrDefault("Groups");
        }
        finally { _refreshLock.Release(); }
        if (groups is { Count: > 0 })
            foreach (var group in await GetGroupsAsync(vrchatUserId, cancellationToken))
                if (groups.TryGetValue(group.Id, out var values)) tags.AddRange(values.Select(tag => tag with {
                    GroupId = group.Id, GroupName = group.Name ?? tag.GroupName, GroupCode = group.Code }));
        // Identical labels can belong to different groups; keep each group's identity.
        return tags.DistinctBy(t => (t.Category, t.Name.ToLowerInvariant(), t.GroupId?.ToLowerInvariant())).ToArray();
    }

    private async Task<GroupMembership[]> GetGroupsAsync(string id, CancellationToken token)
    {
        await _groupsLock.WaitAsync(token);
        try
        {
            var account = accountId();
            if (_groupAccount != account) { _memberships.Clear(); _groupAccount = account; }
            if (account is null) return [];
            var now = DateTimeOffset.UtcNow;
            if (_memberships.TryGetValue(id, out var cached) && cached.Expires > now) return cached.Groups;
            if (now < _nextGroupRequest) return cached.Groups ?? [];
            _nextGroupRequest = now.AddSeconds(1.5); // Single-flight, bounded API traffic.
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.vrchat.cloud/api/1/users/" + Uri.EscapeDataString(id) + "/groups");
            request.Headers.UserAgent.ParseAdd("ELIXR-PlayerList/0.1.0");
            using var response = await vrchatHttp.SendAsync(request, token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var delay = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - now) ?? TimeSpan.FromMinutes(1);
                _nextGroupRequest = now + (delay > TimeSpan.FromSeconds(5) ? delay : TimeSpan.FromSeconds(5));
                return cached.Groups ?? [];
            }
            if (!response.IsSuccessStatusCode) { _memberships[id] = (now.AddMinutes(1), cached.Groups ?? []); return cached.Groups ?? []; }
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (json.RootElement.ValueKind != JsonValueKind.Array) return [];
            var result = json.RootElement.EnumerateArray().Select(x => new GroupMembership(
                    TagFeedParser.Text(x, "groupId"),
                    string.IsNullOrWhiteSpace(TagFeedParser.Text(x, "name")) ? null : TagFeedParser.Text(x, "name").Trim(),
                    VrchatGroupCode.FromParts(TagFeedParser.Text(x, "shortCode"), TagFeedParser.Text(x, "discriminator"))))
                .Where(x => TagFeedParser.ValidId(x.Id, "grp_")).DistinctBy(x => x.Id, StringComparer.OrdinalIgnoreCase).ToArray();
            // A logout/account switch while awaiting the network must not populate the new account's cache.
            if (accountId() != account) return [];
            if (_memberships.Count >= 512)
                foreach (var key in _memberships.OrderBy(pair => pair.Value.Expires).Take(128).Select(pair => pair.Key).ToArray()) _memberships.Remove(key);
            _memberships[id] = (now.AddMinutes(10), result);
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        { token.ThrowIfCancellationRequested(); _nextGroupRequest = DateTimeOffset.UtcNow.AddMinutes(1); return []; }
        finally { _groupsLock.Release(); }
    }
}
