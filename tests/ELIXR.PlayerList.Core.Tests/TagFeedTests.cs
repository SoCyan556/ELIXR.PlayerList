using System.Net;
using ELIXR.PlayerList.Core.Models;
using ELIXR.PlayerList.Core.Services;
using ELIXR.PlayerList.Infrastructure.Services;
namespace ELIXR.PlayerList.Core.Tests;

public sealed class TagFeedTests
{
    private const string User = "usr_11111111-2222-3333-4444-555555555555";
    private const string Group = "grp_11111111-2222-3333-4444-555555555555";
    private const string Few = """{"records":[{"UserID":"usr_11111111-2222-3333-4444-555555555555","Active":true,"TextActive":true,"Malicious":true,"Tag":[".PULSE.<color=#123456>Shared</color>",".SR.<color=cyan>Rainbow</color>","<color=#f00>A</color><color=#00f>B</color>"]}]}""";
    private const string ETags = """{"version":2,"records":[{"userId":"usr_11111111-2222-3333-4444-555555555555","active":true,"tags":[{"text":"Shared","color":"#fedcba","priority":300,"category":"custom","animated":true,"animation":"glitch-lite","gradient":["#f00","#00f"],"speed":2,"amplitude":1.5},{"text":"Client User","category":"client_user"}]}]}""";
    private const string Groups = """{"records":[{"groupId":"grp_11111111-2222-3333-4444-555555555555","active":true,"tags":[{"text":"Member","color":"#12abef","animation":"blink","category":"group"}]}]}""";
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(send(request)); }
    private static HttpResponseMessage Response(string json, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(json) };

    [Fact]
    public void FewTags_PreservesFlagsPrioritiesColorsRunsAndAliases()
    {
        var tags = TagFeedParser.Parse(Few, "FewTags")[User];
        Assert.Equal("Malicious", tags[0].Name);
        Assert.Equal(TagCategory.Malicious, tags[0].Category);
        Assert.Equal("pulse", tags[1].Animation); Assert.Equal("#123456", tags[1].Color);
        Assert.Equal("rain", tags[2].Animation); Assert.Equal("#00FFFF", tags[2].Color);
        Assert.Equal("AB", tags[3].Name); Assert.Equal(2, tags[3].Runs!.Count);
        Assert.Equal("#00f", tags[3].Runs![1].Color);
        Assert.Empty(TagFeedParser.Parse(Few.Replace("\"Active\":true", "\"Active\":false"), "FewTags"));
        Assert.Single(TagFeedParser.Parse(Few.Replace("\"TextActive\":true", "\"TextActive\":false"), "FewTags")[User]);
    }
    [Theory]
    [InlineData(".CYLN.", "")]
    [InlineData(".SCROLL.", "")]
    [InlineData(".SHAKE.", "shake")]
    [InlineData(".BLINK.", "blink")]
    public void FewTags_UsesOriginalSupportedStylesAndStaticFallbacks(string prefix, string expected)
    {
        var tags = TagFeedParser.Parse(Few.Replace(".PULSE.", prefix), "FewTags")[User];
        Assert.Equal(expected, tags[1].Animation);
    }
    [Fact]
    public void FewTags_EnlargedLabelsAreStaticAndCanonicalized()
    {
        var json = Few.Replace("\"TextActive\":true", "\"TextActive\":true,\"BigTextActive\":true,\"PlateBigText\":\".PULSE.MAL\"");
        var tags = TagFeedParser.Parse(json, "FewTags")[User];
        Assert.All(tags, tag => Assert.True(string.IsNullOrEmpty(tag.Animation)));
        Assert.Single(tags.Where(t => t.Name == "Malicious"));
    }
    [Fact]
    public void ETags_PreservesGradientSpeedAmplitudeAndRiskCategories()
    {
        var tags = TagFeedParser.Parse(ETags, "ETags")[User];
        Assert.Equal("glitch", tags[0].Animation); Assert.Equal(2, tags[0].Speed);
        Assert.Equal(1.5, tags[0].Amplitude); Assert.Equal(new[] { "#f00", "#00f" }, tags[0].Gradient);
        Assert.Equal(TagCategory.Malicious, tags[1].Category);
        Assert.Equal(TagCategory.Group, TagFeedParser.Parse(Groups, "Groups")[Group][0].Category);
    }
    [Fact]
    public void UntrustedFeedMarkupAndCssAreNotExecutable()
    {
        var tags = TagFeedParser.Parse(ETags.Replace("#fedcba", "url(https://example.com)").Replace("Shared", "<script>alert(1)</script>"), "ETags")[User];
        Assert.Equal("alert(1)", tags[0].Name); Assert.Equal("#AA66FF", tags[0].Color);
        Assert.Null(TagFeedParser.Color("#fff;display:none"));
    }
    [Fact]
    public void BBCodeColorsAndLastActiveRecordMatchOriginalCompatibility()
    {
        var bbcode = TagFeedParser.Parse(Few.Replace("<color=#f00>A</color><color=#00f>B</color>", "[color=#f00][b]A[/b][/color][color=#00f]B[/color]"), "FewTags")[User].Last();
        Assert.Equal("AB", bbcode.Name); Assert.Equal("#f00", bbcode.Color); Assert.Equal(2, bbcode.Runs!.Count);
        var json = "{\"records\":[{\"UserID\":\"" + User + "\",\"Active\":true,\"Malicious\":true},{\"UserID\":\"" + User + "\",\"Active\":\"true\",\"TextActive\":true,\"Tag\":\"Replacement\"}]}";
        Assert.Equal("Replacement", Assert.Single(TagFeedParser.Parse(json, "FewTags")[User]).Name);
    }
    [Fact]
    public async Task ExactFeeds_AreCookieFreeAndETagsWinsDuplicates_AndGroupsMatchById()
    {
        var calls = new List<string>();
        using var feeds = new HttpClient(new Handler(request => {
            Assert.Equal("raw.githubusercontent.com", request.RequestUri!.Host);
            Assert.Null(request.Headers.Authorization); Assert.False(request.Headers.Contains("Cookie"));
            calls.Add(request.RequestUri.AbsoluteUri);
            return Response(request.RequestUri.AbsolutePath.Contains("FewTags") ? Few : request.RequestUri.AbsolutePath.Contains("ETags.json") ? ETags : Groups);
        }));
        var membershipCalls = 0;
        using var api = new HttpClient(new Handler(request => {
            Assert.Equal("https://api.vrchat.cloud/api/1/users/" + User + "/groups", request.RequestUri!.AbsoluteUri);
            membershipCalls++; return Response("[{\"groupId\":\"" + Group + "\",\"name\":\"Live group name\",\"shortCode\":\"ELIXR\",\"discriminator\":\"0042\"}]");
        }));
        var directory = Path.Combine(Path.GetTempPath(), "elixr-tag-test-" + Guid.NewGuid());
        try
        {
            var service = new GitHubTagService(feeds, api, () => User, directory);
            var tags = await service.GetTagsAsync(User);
            Assert.Equal(GitHubTagService.Sources.Select(s => s.Url), calls);
            Assert.Equal("ETags", Assert.Single(tags.Where(t => t.Name == "Shared")).Source);
            Assert.Contains(tags, t => t.Name == "Member" && t.Category == TagCategory.Group);
            var groupTag = Assert.Single(tags.Where(t => t.Category == TagCategory.Group));
            Assert.Equal(Group, groupTag.GroupId); Assert.Equal("Live group name", groupTag.GroupName); Assert.Equal("ELIXR.0042", groupTag.GroupCode);
            await service.GetTagsAsync(User);
            Assert.Equal(3, calls.Count); Assert.Equal(1, membershipCalls);
            Assert.All(service.Status, s => Assert.Equal("Live", s.State));
            using var offline = new HttpClient(new Handler(_ => throw new HttpRequestException("Offline")));
            var cached = new GitHubTagService(offline, api, () => null, directory);
            Assert.Contains(await cached.GetTagsAsync(User), t => t.Name == "Shared");
            Assert.All(cached.Status, s => Assert.StartsWith("Cached", s.State));
            Assert.Equal(1, membershipCalls); // No group lookup while signed out.
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
    [Theory]
    [InlineData("ELIXR", "0042", "ELIXR.0042")]
    [InlineData(" elixr ", "0001", "ELIXR.0001")]
    [InlineData("ELIXR", "", null)]
    [InlineData("ELIXR", "42", null)]
    [InlineData("ELIXR", "0042\nanything", null)]
    public void GroupCodesAreFormattedWithoutLosingLeadingZeros(string shortCode, string discriminator, string? expected)
        => Assert.Equal(expected, VrchatGroupCode.FromParts(shortCode, discriminator));
    [Fact]
    public async Task SameLabelFromDifferentGroupsRetainsBothGroupIdentities()
    {
        var other = Group.Replace("555555555555", "666666666666");
        var groupFeed = System.Text.Json.JsonSerializer.Serialize(new { records = new[] {
            new { groupId=Group, groupName="Feed fallback name", active=true, tags=new[]{new{text="Member"}} },
            new { groupId=other, groupName="Other group", active=true, tags=new[]{new{text="Member"}} }
        }});
        using var feeds = new HttpClient(new Handler(r => Response(r.RequestUri!.AbsolutePath.Contains("FewTags") ? Few : r.RequestUri.AbsolutePath.Contains("ETags.json") ? ETags : groupFeed)));
        using var api = new HttpClient(new Handler(_ => Response(System.Text.Json.JsonSerializer.Serialize(new[] {
            new {groupId=Group, name="", shortCode="ELIXR", discriminator="0042"},
            new {groupId=other, name="Live second group", shortCode="", discriminator=""}
        }))));
        var directory = Path.Combine(Path.GetTempPath(), "elixr-tag-test-" + Guid.NewGuid());
        try
        {
            var tags = (await new GitHubTagService(feeds, api, () => User, directory).GetTagsAsync(User)).Where(t=>t.Category==TagCategory.Group).ToArray();
            Assert.Equal(2, tags.Length); Assert.Equal("Feed fallback name",tags[0].GroupName); Assert.Equal("ELIXR.0042", tags[0].GroupCode);
            Assert.Equal("Live second group", tags[1].GroupName); Assert.Null(tags[1].GroupCode); Assert.Equal(other, tags[1].GroupId);
        }
        finally { if(Directory.Exists(directory)) Directory.Delete(directory,true); }
    }
    [Fact]
    public async Task RateLimitedGroupRequestsRespectCooldown()
    {
        using var feeds = new HttpClient(new Handler(request => Response(request.RequestUri!.AbsolutePath.Contains("FewTags") ? Few : request.RequestUri.AbsolutePath.Contains("ETags.json") ? ETags : Groups)));
        var calls = 0;
        using var api = new HttpClient(new Handler(_ => { calls++; var r = Response("{}", HttpStatusCode.TooManyRequests); r.Headers.RetryAfter = new(TimeSpan.FromMinutes(3)); return r; }));
        var directory = Path.Combine(Path.GetTempPath(), "elixr-tag-test-" + Guid.NewGuid());
        try
        {
            var service = new GitHubTagService(feeds, api, () => User, directory);
            await service.GetTagsAsync(User); await service.GetTagsAsync(User);
            Assert.Equal(1, calls);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private sealed class Roster : IPlayerListService
    {
        public InstanceState Current { get; private set; } = InstanceState.Offline;
        public event EventHandler<InstanceState>? Changed;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Set(params Player[] players) { Current = new(null, null, null, true, players); Changed?.Invoke(this, Current); }
    }
    private sealed class DelayedTags : ITagService
    {
        public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<IReadOnlyList<PlayerTag>> Result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IReadOnlyList<PlayerTag>> GetTagsAsync(string id, CancellationToken cancellationToken = default) { Started.TrySetResult(); return await Result.Task.WaitAsync(cancellationToken); }
    }
    [Fact]
    public async Task SlowTagFetchDoesNotHideRosterOrResurrectDepartedPlayer()
    {
        var roster = new Roster(); var tags = new DelayedTags();
        roster.Set(new Player(User, "Test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, []));
        await using var service = new TaggedPlayerListService(roster, tags, () => true);
        await service.StartAsync();
        Assert.Single(service.Current.Players);
        await tags.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        roster.Set();
        tags.Result.SetResult([new("Arrived too late", TagCategory.Custom)]);
        await Task.Delay(80);
        Assert.Empty(service.Current.Players);
    }
}
