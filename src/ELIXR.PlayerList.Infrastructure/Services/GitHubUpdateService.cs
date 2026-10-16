using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ELIXR.PlayerList.Infrastructure.Services;

public sealed record AvailableUpdate(string Version, string AssetName, Uri DownloadUrl, string Sha256, long Size);

// Uses a cookie-free client. Only this repository's stable releases can supply an installer.
public sealed class GitHubUpdateService(HttpClient publicHttp)
{
    public const string Repository = "SoCyan556/ELIXR.PlayerList";
    private const long MaxInstallerBytes = 500L * 1024 * 1024;
    public static AvailableUpdate? ParseRelease(JsonElement release, Version current)
    {
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) return null;
        var tag = release.GetProperty("tag_name").GetString() ?? "";
        if (!Regex.IsMatch(tag, @"^v[0-9]+\.[0-9]+\.[0-9]+$")) return null;
        if (!Version.TryParse(tag[1..], out var version) || version <= new Version(current.Major, current.Minor, Math.Max(current.Build, 0))) return null;
        var name = $"ELIXR-PlayerList-{tag[1..]}-Setup-x64.exe";
        var expected = $"https://github.com/{Repository}/releases/download/{tag}/{name}";
        foreach (var asset in release.GetProperty("assets").EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != name || asset.GetProperty("browser_download_url").GetString() != expected) continue;
            var digest = asset.TryGetProperty("digest", out var value) ? value.GetString() : null;
            var size = asset.GetProperty("size").GetInt64();
            if (digest is null || !Regex.IsMatch(digest, @"^sha256:[a-fA-F0-9]{64}$") || size is <= 0 or > MaxInstallerBytes) continue;
            return new(tag[1..], name, new Uri(expected), digest[7..].ToLowerInvariant(), size);
        }
        return null;
    }
    public async Task<AvailableUpdate?> CheckAsync(Version current, CancellationToken token = default)
    {
        using var request = Request(new Uri($"https://api.github.com/repos/{Repository}/releases/latest"));
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await publicHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync(token);
        using var body = new MemoryStream();
        await CopyBoundedAsync(input, body, 2 * 1024 * 1024, token);
        using var json = JsonDocument.Parse(body.ToArray());
        return ParseRelease(json.RootElement, current);
    }
    public async Task<string> DownloadAsync(AvailableUpdate update, string directory, CancellationToken token = default)
    {
        var expected = $"https://github.com/{Repository}/releases/download/v{update.Version}/ELIXR-PlayerList-{update.Version}-Setup-x64.exe";
        if (!Regex.IsMatch(update.Version, @"^[0-9]+\.[0-9]+\.[0-9]+$") || update.DownloadUrl.AbsoluteUri != expected ||
            !Regex.IsMatch(update.Sha256, @"^[a-fA-F0-9]{64}$") || update.Size is <= 0 or > MaxInstallerBytes)
            throw new InvalidDataException("Invalid update metadata.");
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, $"ELIXR-Setup-{Guid.NewGuid():N}.exe");
        var partial = destination + ".part";
        try
        {
            var uri = update.DownloadUrl;
            for (var hop = 0; hop < 6; hop++)
            {
                if (uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
                    uri.Host is not ("github.com" or "release-assets.githubusercontent.com" or "objects.githubusercontent.com"))
                    throw new InvalidDataException("Untrusted update redirect.");
                using var request = Request(uri);
                using var response = await publicHttp.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location is { } location)
                { uri = location.IsAbsoluteUri ? location : new Uri(uri, location); continue; }
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is { } length && length != update.Size) throw new InvalidDataException("Installer size mismatch.");
                await using (var input = await response.Content.ReadAsStreamAsync(token))
                await using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    await CopyBoundedAsync(input, output, update.Size, token);
                    if (output.Length != update.Size) throw new InvalidDataException("Incomplete installer.");
                }
                await using (var file = File.OpenRead(partial))
                {
                    var actual = await SHA256.HashDataAsync(file, token);
                    if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(update.Sha256))) throw new InvalidDataException("Installer checksum mismatch.");
                }
                File.Move(partial, destination);
                return destination;
            }
            throw new InvalidDataException("Too many update redirects.");
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }
    private static HttpRequestMessage Request(Uri uri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("ELIXR-PlayerList/0.1.0");
        return request;
    }
    private static async Task CopyBoundedAsync(Stream input, Stream output, long limit, CancellationToken token)
    {
        var buffer = new byte[81920]; long total = 0; int count;
        while ((count = await input.ReadAsync(buffer, token)) > 0)
        {
            total += count;
            if (total > limit) throw new InvalidDataException("Response exceeds the expected size.");
            await output.WriteAsync(buffer.AsMemory(0, count), token);
        }
    }
}
