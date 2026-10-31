using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ELIXR.PlayerList.Core.Services;

namespace ELIXR.PlayerList.Infrastructure.Services;

// Only native code accesses VRChat. The UI receives an opaque local image URL, never cookies or signed CDN URLs.
public sealed class VrchatProfileImageService(HttpClient api, HttpClient publicImages, Func<string?> accountId, string? cacheDirectory = null) : IPlayerImageService, IPlayerRelationshipService
{
    public const string ImageHost = "avatars.elixr.example";
    public string CacheDirectory { get; } = Path.GetFullPath(cacheDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ELIXR", "PlayerList", "PlayerImages"));
    private const int MaxImageBytes = 1024 * 1024;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, (DateTimeOffset Expires, string? Image)> _profiles = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _nextRequest;
    private DateTimeOffset _rateLimitedUntil;
    private string? _account;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Account, bool Friend, DateTimeOffset Expires)> _friendships = new(StringComparer.OrdinalIgnoreCase);
    public bool? IsFriend(string vrchatUserId) => _friendships.TryGetValue(vrchatUserId, out var status) && status.Account == accountId() && status.Expires > DateTimeOffset.UtcNow ? status.Friend : null;

    public static bool AllowedImageUri(Uri uri) => uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
        (uri.Host is "files.vrchat.cloud" or "assets.vrchat.com" || uri.Host == "api.vrchat.cloud" &&
        Regex.IsMatch(uri.AbsolutePath, @"^/api/1/(?:file|image)/file_[0-9a-fA-F-]{36}/[0-9]+/(?:file|[0-9]+)(?:/modified)?/?$"));

    public static IReadOnlyList<Uri> Candidates(JsonElement user)
    {
        var result = new List<Uri>();
        // Newer public profiles expose the resolved portrait as iconUrl. Keep the legacy
        // fields for the authenticated account and older VRChat profile responses.
        foreach (var key in new[] { "iconUrl", "userIcon", "userIconThumbnail", "profilePicOverrideThumbnail", "profilePicOverride", "currentAvatarThumbnailImageUrl", "currentAvatarImageUrl" })
        {
            var text = TagFeedParser.Text(user, key);
            if (text.StartsWith("/api/1/", StringComparison.Ordinal)) text = "https://api.vrchat.cloud" + text;
            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || !AllowedImageUri(uri)) continue;
            // VRChat's image endpoint supplies a small thumbnail instead of the original uploaded image.
            if (uri.Host == "api.vrchat.cloud")
            {
                var path = Regex.Replace(uri.AbsolutePath, @"^/api/1/(?:file|image)/(file_[0-9a-fA-F-]{36})/([0-9]+)/(?:file|[0-9]+)(/modified)?/?$", "/api/1/image/$1/$2/128$3");
                result.Add(new Uri("https://api.vrchat.cloud" + path));
            }
            result.Add(uri);
        }
        return result.Distinct().ToArray();
    }

    public async Task<string?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!TagFeedParser.ValidId(id, "usr_")) return null;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var account = accountId();
            if (_account != account) { _profiles.Clear(); _friendships.Clear(); _account = account; }
            if (account is null) return null;
            var now = DateTimeOffset.UtcNow;
            if (_profiles.TryGetValue(id, out var cached) && cached.Expires > now && CachedExists(cached.Image)) return cached.Image;
            if (now < _nextRequest) return cached.Image;
            _nextRequest = now.AddSeconds(1);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var token = timeout.Token;
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.vrchat.cloud/api/1/users/" + id);
            request.Headers.UserAgent.ParseAdd("ELIXR-PlayerList/0.1.0");
            using var response = await api.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests) { Cooldown(response); return cached.Image; }
            if (response.StatusCode == HttpStatusCode.Unauthorized) _nextRequest = now.AddMinutes(1);
            if (!response.IsSuccessStatusCode) return Remember(id, cached.Image, response.StatusCode == HttpStatusCode.Unauthorized ? 60 : 120);
            var body = await ReadBoundedAsync(response, 512 * 1024, token);
            using var json = JsonDocument.Parse(body);
            if (TagFeedParser.Text(json.RootElement, "id") != id) return Remember(id, cached.Image, 120);
            if (accountId() != account) return null;
            var friendship = TagFeedParser.Property(json.RootElement, "isFriend");
            if (friendship.ValueKind is JsonValueKind.True or JsonValueKind.False)
                _friendships[id] = (account, friendship.GetBoolean(), DateTimeOffset.UtcNow.AddMinutes(10));
            foreach (var uri in Candidates(json.RootElement))
            {
                string? image;
                try { image = await DownloadAsync(uri, token); }
                catch (Exception ex) when (ex is HttpRequestException or IOException) { continue; }
                if (accountId() != account) return null;
                if (image is not null) return Remember(id, image, 600);
                if (DateTimeOffset.UtcNow < _rateLimitedUntil) break;
            }
            return Remember(id, cached.Image, 120);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or IOException or UnauthorizedAccessException or JsonException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Remember(id, _profiles.GetValueOrDefault(id).Image, 60);
        }
        finally { _gate.Release(); }
    }
    private bool CachedExists(string? image) => image is null || File.Exists(Path.Combine(CacheDirectory, new Uri(image).Segments.Last()));
    private string? Remember(string id, string? image, int seconds)
    {
        if (_profiles.Count >= 512)
            foreach (var key in _profiles.OrderBy(p => p.Value.Expires).Take(128).Select(p => p.Key).ToArray()) { _profiles.Remove(key); _friendships.TryRemove(key, out _); }
        _profiles[id] = (DateTimeOffset.UtcNow.AddSeconds(seconds), image);
        return image;
    }
    private void Cooldown(HttpResponseMessage response)
    {
        var now = DateTimeOffset.UtcNow;
        var delay = response.Headers.RetryAfter?.Delta ?? (response.Headers.RetryAfter?.Date - now) ?? TimeSpan.FromMinutes(1);
        _nextRequest = now + (delay > TimeSpan.FromSeconds(5) ? delay : TimeSpan.FromSeconds(5));
        _rateLimitedUntil = _nextRequest;
    }
    private async Task<string?> DownloadAsync(Uri initial, CancellationToken token)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(initial.AbsoluteUri))).ToLowerInvariant();
        foreach (var extension in new[] { ".png", ".jpg", ".webp", ".gif" })
        {
            var info = new FileInfo(Path.Combine(CacheDirectory, key + extension));
            if (info is { Exists: true, Length: > 0 and <= MaxImageBytes }) return "https://" + ImageHost + "/" + key + extension;
        }
        var uri = initial;
        for (var hop = 0; hop < 5 && AllowedImageUri(uri); hop++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("ELIXR-PlayerList/0.1.0");
            using var response = await (uri.Host == "api.vrchat.cloud" ? api : publicImages).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
            { uri = location.IsAbsoluteUri ? location : new Uri(uri, location); continue; }
            if (response.StatusCode == HttpStatusCode.TooManyRequests) { Cooldown(response); return null; }
            if (!response.IsSuccessStatusCode) return null;
            var bytes = await ReadBoundedAsync(response, MaxImageBytes, token);
            var extension = ImageExtension(bytes);
            if (extension is null) return null;
            Directory.CreateDirectory(CacheDirectory);
            var name = key + extension;
            var path = Path.Combine(CacheDirectory, name);
            await File.WriteAllBytesAsync(path + ".tmp", bytes, token);
            File.Move(path + ".tmp", path, true);
            PruneCache();
            return "https://" + ImageHost + "/" + name;
        }
        return null;
    }
    public static string? ImageExtension(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return ".png";
        if (bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 })) return ".jpg";
        if (bytes.Length >= 12 && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP") return ".webp";
        if (bytes.Length >= 6 && Encoding.ASCII.GetString(bytes, 0, 6) is "GIF87a" or "GIF89a") return ".gif";
        return null; // Never serve SVG, HTML, JSON, or other executable content.
    }
    private static async Task<byte[]> ReadBoundedAsync(HttpResponseMessage response, int limit, CancellationToken token)
    {
        if (response.Content.Headers.ContentLength > limit) throw new IOException("Image response exceeds its size limit.");
        await using var input = await response.Content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        var buffer = new byte[16384]; int read;
        while ((read = await input.ReadAsync(buffer, token)) > 0)
        { if (output.Length + read > limit) throw new IOException("Image response exceeds its size limit."); output.Write(buffer, 0, read); }
        return output.ToArray();
    }
    private void PruneCache()
    {
        // Only remove generated hash-named image files in this dedicated cache, never arbitrary user files.
        var files = new DirectoryInfo(CacheDirectory).EnumerateFiles().Where(f => Regex.IsMatch(f.Name, @"^[a-f0-9]{64}\.(png|jpg|webp|gif)$")).OrderByDescending(f => f.LastWriteTimeUtc).ToArray();
        long size = 0;
        foreach (var file in files)
        {
            size += file.Length;
            if (size <= 32 * 1024 * 1024) continue;
            try { file.Delete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
