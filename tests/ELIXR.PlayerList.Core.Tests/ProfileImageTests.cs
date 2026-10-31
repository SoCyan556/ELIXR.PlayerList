using System.Net;
using System.Text.Json;
using ELIXR.PlayerList.Core.Models;
using ELIXR.PlayerList.Core.Services;
using ELIXR.PlayerList.Infrastructure.Services;
namespace ELIXR.PlayerList.Core.Tests;

public sealed class ProfileImageTests
{
    private const string User = "usr_00000000-0000-0000-0000-000000000001";
    private const string FileUrl = "https://api.vrchat.cloud/api/1/file/file_00000000-0000-0000-0000-000000000001/1/file";
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9t8AAAAASUVORK5CYII=");
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken t) => Task.FromResult(send(r)); }
    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(body) };
    private static string Profile(string field = "userIcon") => JsonSerializer.Serialize(new Dictionary<string,string> { ["id"] = User, [field] = FileUrl });
    private static string DirectoryForTest() => Path.Combine(Path.GetTempPath(), "elixr-image-tests-" + Guid.NewGuid());

    [Fact]
    public void ResolvedPublicIconTakesPriorityOverLegacyImages()
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new { iconUrl = FileUrl.Replace("001/1/file", "001/7/file"), userIcon = FileUrl }));
        var urls = VrchatProfileImageService.Candidates(json.RootElement);
        Assert.EndsWith("/7/128", urls[0].AbsolutePath);
        Assert.EndsWith("/7/file", urls[1].AbsolutePath);
        Assert.EndsWith("/1/128", urls[2].AbsolutePath);
    }
    [Fact]
    public async Task OtherUserPublicProfileWithOnlyIconUrlLoads()
    {
        var otherUser = User.Replace("000001", "000002");
        using var api = new HttpClient(new Handler(r => r.RequestUri!.AbsolutePath.Contains("/users/")
            ? Json(JsonSerializer.Serialize(new { id = otherUser, iconUrl = FileUrl }))
            : new(HttpStatusCode.OK) { Content = new ByteArrayContent(Png) }));
        var directory = DirectoryForTest();
        try { Assert.NotNull(await new VrchatProfileImageService(api, api, () => User, directory).GetAsync(otherUser)); }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact]
    public void UntrustedResolvedIconStillFallsBackToLegacyFields()
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new { iconUrl = "https://evil.example/icon.png", userIcon = FileUrl }));
        Assert.All(VrchatProfileImageService.Candidates(json.RootElement), uri => Assert.Equal("api.vrchat.cloud", uri.Host));
        Assert.Equal(2, VrchatProfileImageService.Candidates(json.RootElement).Count);
    }
    [Fact]
    public void ImagePreferenceUsesUserIconThenOverrideThenAvatar_AndSmallThumbnail()
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(new { userIcon = FileUrl, profilePicOverrideThumbnail = FileUrl.Replace("001/1/file", "001/2/file"), currentAvatarThumbnailImageUrl = FileUrl.Replace("001/1/file", "001/3/file") }));
        var urls = VrchatProfileImageService.Candidates(json.RootElement).Select(x => x.AbsoluteUri).ToArray();
        Assert.Contains("/api/1/image/", urls[0]); Assert.EndsWith("/1/128", urls[0]);
        Assert.Contains("/2/128", urls[2]); Assert.Contains("/3/128", urls[4]);
    }
    [Theory]
    [InlineData("https://evil.example/image.png")]
    [InlineData("http://files.vrchat.cloud/image.png")]
    [InlineData("https://api.vrchat.cloud/api/1/auth/user")]
    [InlineData("https://files.vrchat.cloud.evil.example/image.png")]
    [InlineData("https://user:password@files.vrchat.cloud/image.png")]
    [InlineData("https://127.0.0.1/image.png")]
    public void ImageUrlsCannotReachUntrustedHostsOrNonImageApiRoutes(string url) => Assert.False(VrchatProfileImageService.AllowedImageUri(new(url)));

    [Fact]
    public async Task NativeImagesAreCachedAndCdnReceivesNoCredentials()
    {
        var apiCalls = 0; var cdnCalls = 0;
        using var api = new HttpClient(new Handler(r => {
            apiCalls++; Assert.Equal("api.vrchat.cloud", r.RequestUri!.Host);
            if (r.RequestUri.AbsolutePath.Contains("/users/")) return Json(Profile());
            Assert.EndsWith("/128", r.RequestUri.AbsolutePath);
            return new(HttpStatusCode.Found) { Headers = { Location = new("https://files.vrchat.cloud/test.png") } };
        }));
        api.DefaultRequestHeaders.Add("Cookie", "auth=native_test_cookie");
        using var cdn = new HttpClient(new Handler(r => {
            cdnCalls++; Assert.Null(r.Headers.Authorization); Assert.False(r.Headers.Contains("Cookie"));
            return new(HttpStatusCode.OK) { Content = new ByteArrayContent(Png) };
        }));
        var directory = DirectoryForTest();
        try
        {
            var service = new VrchatProfileImageService(api, cdn, () => User, directory);
            var url = await service.GetAsync(User);
            Assert.StartsWith("https://avatars.elixr.example/", url); Assert.EndsWith(".png", url);
            Assert.DoesNotContain(User, url);
            Assert.Equal(Png, await System.IO.File.ReadAllBytesAsync(Assert.Single(Directory.GetFiles(directory))));
            Assert.Equal(url, await service.GetAsync(User)); Assert.Equal(2, apiCalls); Assert.Equal(1, cdnCalls);
            var second = new VrchatProfileImageService(api, cdn, () => User, directory);
            Assert.Equal(url, await second.GetAsync(User)); Assert.Equal(3, apiCalls); Assert.Equal(1, cdnCalls);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact]
    public async Task RedirectToUntrustedHostIsRejectedWithoutRequestingIt()
    {
        using var api = new HttpClient(new Handler(r => r.RequestUri!.AbsolutePath.Contains("/users/") ? Json(Profile()) : new(HttpStatusCode.Found) { Headers = { Location = new("https://evil.example/file.png") } }));
        using var cdn = new HttpClient(new Handler(_ => throw new Exception("Must not contact an untrusted host")));
        Assert.Null(await new VrchatProfileImageService(api, cdn, () => User, DirectoryForTest()).GetAsync(User));
    }
    [Fact]
    public async Task MissingImagesAndSignedOutProfilesDoNotHammerApi()
    {
        var calls = 0;
        using var api = new HttpClient(new Handler(_ => { calls++; return Json("{\"id\":\"" + User + "\"}"); }));
        var service = new VrchatProfileImageService(api, api, () => User, DirectoryForTest());
        Assert.Null(await service.GetAsync(User)); Assert.Null(await service.GetAsync(User)); Assert.Equal(1, calls);
        Assert.Null(await new VrchatProfileImageService(api, api, () => null).GetAsync(User)); Assert.Equal(1, calls);
        Assert.Null(await service.GetAsync("usr_invalid")); Assert.Equal(1, calls);
    }
    [Fact]
    public async Task RateLimitStopsOtherProfileLookups()
    {
        var calls = 0;
        using var api = new HttpClient(new Handler(_ => { calls++; var r = Json("{}", HttpStatusCode.TooManyRequests); r.Headers.RetryAfter = new(TimeSpan.FromMinutes(2)); return r; }));
        var service = new VrchatProfileImageService(api, api, () => User, DirectoryForTest());
        await service.GetAsync(User); await service.GetAsync(User.Replace("000001", "000002")); Assert.Equal(1, calls);
    }
    [Fact]
    public void OnlyRasterImageSignaturesAreAccepted()
    {
        Assert.Equal(".png", VrchatProfileImageService.ImageExtension(Png));
        Assert.Null(VrchatProfileImageService.ImageExtension(System.Text.Encoding.UTF8.GetBytes("<svg onload='alert(1)'/>")));
        Assert.Null(VrchatProfileImageService.ImageExtension(System.Text.Encoding.UTF8.GetBytes("<html>error</html>")));
    }
    [Fact]
    public async Task OversizeImagesRemainSafeFallbacks()
    {
        using var api = new HttpClient(new Handler(r => {
            if (r.RequestUri!.AbsolutePath.Contains("/users/")) return Json(Profile());
            var content = new ByteArrayContent(Png); content.Headers.ContentLength = 2 * 1024 * 1024;
            return new(HttpStatusCode.OK) { Content = content };
        }));
        Assert.Null(await new VrchatProfileImageService(api, api, () => User, DirectoryForTest()).GetAsync(User));
    }
    [Fact]
    public async Task OversizeIconCanFallBackToAvatarThumbnail()
    {
        using var api = new HttpClient(new Handler(r => {
            if (r.RequestUri!.AbsolutePath.Contains("/users/"))
                return Json(JsonSerializer.Serialize(new { id = User, userIcon = FileUrl, currentAvatarThumbnailImageUrl = FileUrl.Replace("000001/", "000002/") }));
            var content = new ByteArrayContent(Png);
            if (r.RequestUri.AbsolutePath.Contains("000001/")) content.Headers.ContentLength = 2 * 1024 * 1024;
            return new(HttpStatusCode.OK) { Content = content };
        }));
        var directory = DirectoryForTest();
        try { Assert.NotNull(await new VrchatProfileImageService(api, api, () => User, directory).GetAsync(User)); }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Fact]
    public async Task FriendshipIsReadWithoutAnImageAndNeverLeaksAcrossAccounts()
    {
        string? account=User;
        using var api=new HttpClient(new Handler(_=>Json(JsonSerializer.Serialize(new {id=User,isFriend=true}))));
        var service=new VrchatProfileImageService(api,api,()=>account,DirectoryForTest());
        Assert.Null(service.IsFriend(User));
        Assert.Null(await service.GetAsync(User)); Assert.True(service.IsFriend(User));
        account=User.Replace("000001","000002"); Assert.Null(service.IsFriend(User));
        account=null; Assert.Null(service.IsFriend(User));
    }
    [Theory]
    [InlineData("\"true\"")]
    [InlineData("null")]
    public async Task OnlyAnActualBooleanFriendshipFieldIsAccepted(string value)
    {
        using var api=new HttpClient(new Handler(_=>Json("{\"id\":\""+User+"\",\"isFriend\":"+value+"}")));
        var service=new VrchatProfileImageService(api,api,()=>User,DirectoryForTest());
        await service.GetAsync(User); Assert.Null(service.IsFriend(User));
    }
    private sealed class RelationshipImages : IPlayerImageService,IPlayerRelationshipService
    {
        public bool? IsFriend(string id)=>true;
        public Task<string?> GetAsync(string id,CancellationToken token=default)=>Task.FromResult<string?>(null);
    }
    [Fact]
    public async Task RosterPublishesFriendshipEvenWhenThePortraitIsUnavailable()
    {
        var roster=new Roster();roster.Set(new Player(User,"Test",DateTimeOffset.Now,DateTimeOffset.Now,[]));
        await using var service=new ProfileImagePlayerListService(roster,new RelationshipImages(),()=>true);
        var updated=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        service.Changed+=(_,state)=>{if(state.Players.FirstOrDefault()?.IsFriend==true)updated.TrySetResult();};
        await service.StartAsync(); await updated.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(Assert.Single(service.Current.Players).IsFriend);Assert.Null(service.Current.Players[0].AvatarUrl);
    }
    private sealed class Roster : IPlayerListService
    {
        public InstanceState Current { get; private set; } = InstanceState.Offline;
        public event EventHandler<InstanceState>? Changed;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Set(params Player[] players) { Current = new(null, null, null, true, players); Changed?.Invoke(this, Current); }
    }
    private sealed class DelayedImages : IPlayerImageService
    {
        public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string?> Result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<string?> GetAsync(string id, CancellationToken cancellationToken = default) { Started.TrySetResult(); return await Result.Task.WaitAsync(cancellationToken); }
    }
    [Fact]
    public async Task ImageLoadingNeverBlocksRosterOrResurrectsDepartedPlayers()
    {
        var roster = new Roster(); var images = new DelayedImages();
        roster.Set(new Player(User, "Test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, []));
        await using var service = new ProfileImagePlayerListService(roster, images, () => true);
        await service.StartAsync(); Assert.Single(service.Current.Players);
        await images.Started.Task.WaitAsync(TimeSpan.FromSeconds(3)); roster.Set();
        images.Result.SetResult("https://avatars.elixr.example/test.png"); await Task.Delay(80);
        Assert.Empty(service.Current.Players);
    }
}
