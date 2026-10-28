using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ELIXR.PlayerList.Infrastructure.Services;

namespace ELIXR.PlayerList.Core.Tests;

public sealed class UserSearchTests
{
    private const string Id = "usr_11111111-2222-3333-4444-555555555555";
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(reply(request));
    }
    private static HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    [Fact]
    public async Task EncodesDisplayNameAndBoundsPagingWithoutAcceptingReturnedUrls()
    {
        using var http = new HttpClient(new Handler(request => {
            Assert.Equal("api.vrchat.cloud", request.RequestUri!.Host);
            Assert.Equal("/api/1/users", request.RequestUri.AbsolutePath);
            Assert.Contains("search=a%26n%3D100", request.RequestUri.Query);
            Assert.Contains("&n=20&offset=20", request.RequestUri.Query);
            return Response($$"""[{"id":"{{Id}}","displayName":"A & B","iconUrl":"https://untrusted.example/image"},{"id":"invalid","displayName":"Bad"},{"id":"{{Id}}","displayName":"Duplicate"}]""");
        }));
        var result = await new VrchatUserSearchService(http, () => Id).SearchAsync(" a&n=100 ", 1);
        var user = Assert.Single(result.Users);
        Assert.Equal(Id, user.VrchatUserId); Assert.False(user.TagsChecked); Assert.Null(user.AvatarUrl); Assert.Empty(user.Tags); Assert.False(result.HasMore);
    }
    [Theory]
    [InlineData("", 0)] [InlineData("x", 0)] [InlineData("hello\nworld", 0)] [InlineData("valid", -1)] [InlineData("valid", 50)]
    public async Task RejectsInvalidQueriesBeforeAnyRequest(string query, int page)
    {
        using var http = new HttpClient(new Handler(_ => throw new Exception("Must not send")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new VrchatUserSearchService(http, () => Id).SearchAsync(query, page));
    }
    [Fact]
    public async Task RequiresAnAuthenticatedAccount()
    {
        using var http = new HttpClient(new Handler(_ => throw new Exception("Must not send")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new VrchatUserSearchService(http, () => null).SearchAsync("valid", 0));
    }
    [Fact]
    public async Task HonorsRateLimitAndDoesNotSendAgainDuringCooldown()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(_ => {calls++;var response=Response("{}",HttpStatusCode.TooManyRequests);response.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromMinutes(2));return response;}));
        var service = new VrchatUserSearchService(http, () => Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SearchAsync("valid",0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SearchAsync("valid",0));
        Assert.Equal(1,calls);
    }
    [Fact]
    public async Task DiscardsAnAccountSwitchDuringSearch()
    {
        string? account = Id;
        using var http = new HttpClient(new Handler(_ => {account=null;return Response("[]");}));
        await Assert.ThrowsAsync<OperationCanceledException>(() => new VrchatUserSearchService(http, () => account).SearchAsync("valid",0));
    }
    [Fact]
    public async Task ReturnsHasMoreOnlyForAFullPage()
    {
        var rows=Enumerable.Range(0,20).Select(i=>$"{{\"id\":\"usr_11111111-2222-3333-4444-{i:D12}\",\"displayName\":\"User {i}\"}}");
        using var http = new HttpClient(new Handler(_=>Response("["+string.Join(',',rows)+"]")));
        Assert.True((await new VrchatUserSearchService(http,()=>Id).SearchAsync("valid",0)).HasMore);
    }
    [Fact]
    public async Task NeverTreatsAuthenticationFailureAsAnEmptyResult()
    {
        using var http = new HttpClient(new Handler(_=>Response("{}",HttpStatusCode.Unauthorized)));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new VrchatUserSearchService(http,()=>Id).SearchAsync("valid",0));
    }
}
