using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ELIXR.PlayerList.Infrastructure.Services;
namespace ELIXR.PlayerList.Core.Tests;

public sealed class UpdateTests
{
    private static readonly byte[] Installer = Encoding.UTF8.GetBytes("MZ-test-installer");
    private static readonly string Hash = Convert.ToHexString(SHA256.HashData(Installer)).ToLowerInvariant();
    private const string Url = "https://github.com/SoCyan556/ELIXR.PlayerList/releases/download/v0.2.0/ELIXR-PlayerList-0.2.0-Setup-x64.exe";
    private static string Release(string tag = "v0.2.0", bool draft = false, bool prerelease = false, string? url = null, string? digest = null, long? size = null) => JsonSerializer.Serialize(new {
        tag_name = tag, draft, prerelease,
        assets = new[] { new { name = "ELIXR-PlayerList-0.2.0-Setup-x64.exe", browser_download_url = url ?? Url, digest = digest ?? "sha256:" + Hash, size = size ?? Installer.Length } }
    });
    private static AvailableUpdate? Parse(string text) { using var json = JsonDocument.Parse(text); return GitHubUpdateService.ParseRelease(json.RootElement, new(0, 1, 0)); }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(send(request)); }
    [Fact] public void ValidNewStableInstallerIsAccepted() => Assert.Equal("0.2.0", Parse(Release())?.Version);
    [Theory]
    [InlineData("v0.1.0")][InlineData("v0.0.9")][InlineData("v0.2.0-beta")][InlineData("../../evil")]
    public void OldOrInvalidVersionsAreRejected(string tag) => Assert.Null(Parse(Release(tag: tag)));
    [Fact] public void DraftAndPrereleaseAreRejected() { Assert.Null(Parse(Release(draft: true))); Assert.Null(Parse(Release(prerelease: true))); }
    [Theory]
    [InlineData("https://evil.example/setup.exe")]
    [InlineData("https://github.com/attacker/ELIXR.PlayerList/releases/download/v0.2.0/ELIXR-PlayerList-0.2.0-Setup-x64.exe")]
    public void OtherRepositoriesCannotSupplyUpdates(string url) => Assert.Null(Parse(Release(url: url)));
    [Fact] public void ChecksumAndBoundedSizeAreMandatory() { Assert.Null(Parse(Release(digest: ""))); Assert.Null(Parse(Release(size: 0))); Assert.Null(Parse(Release(size: long.MaxValue))); }
    [Fact] public async Task NoReleaseIsNotAnError()
    {
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.NotFound)));
        Assert.Null(await new GitHubUpdateService(http).CheckAsync(new(0, 1, 0)));
    }
    [Fact] public async Task VerifiedDownloadFollowsOnlyTrustedRedirects()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(r => {
            calls++; Assert.False(r.Headers.Contains("Cookie")); Assert.Null(r.Headers.Authorization);
            if (r.RequestUri!.Host == "github.com") return new(HttpStatusCode.Found) { Headers = { Location = new("https://release-assets.githubusercontent.com/release.exe?secret=not-logged") } };
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(Installer) };
        }));
        var directory = Path.Combine(Path.GetTempPath(), "elixr-update-tests-" + Guid.NewGuid());
        try {
            var path = await new GitHubUpdateService(http).DownloadAsync(Parse(Release())!, directory);
            Assert.Equal(Installer, await File.ReadAllBytesAsync(path)); Assert.Equal(2, calls); Assert.Single(Directory.GetFiles(directory));
        } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Theory]
    [InlineData("https://evil.example/setup.exe")][InlineData("http://release-assets.githubusercontent.com/setup.exe")]
    public async Task UntrustedRedirectIsNeverContacted(string redirect)
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(_ => { calls++; return new(HttpStatusCode.Found) { Headers = { Location = new(redirect) } }; }));
        var directory = Path.Combine(Path.GetTempPath(), "elixr-update-tests-" + Guid.NewGuid());
        try {
            await Assert.ThrowsAsync<InvalidDataException>(() => new GitHubUpdateService(http).DownloadAsync(Parse(Release())!, directory));
            Assert.Equal(1, calls); Assert.Empty(Directory.GetFiles(directory));
        } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task TamperedOrTruncatedDownloadIsNotExecutable(bool truncate)
    {
        var wrong = truncate ? Installer[..4] : Encoding.UTF8.GetBytes("XX-test-installer");
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new ByteArrayContent(wrong) }));
        var directory = Path.Combine(Path.GetTempPath(), "elixr-update-tests-" + Guid.NewGuid());
        try {
            await Assert.ThrowsAsync<InvalidDataException>(() => new GitHubUpdateService(http).DownloadAsync(Parse(Release())!, directory));
            Assert.Empty(Directory.GetFiles(directory));
        } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
