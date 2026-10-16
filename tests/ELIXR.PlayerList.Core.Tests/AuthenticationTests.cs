using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ELIXR.PlayerList.Core.Models;
using ELIXR.PlayerList.Infrastructure.Services;
namespace ELIXR.PlayerList.Core.Tests;

public sealed class AuthenticationTests
{
    private const string User = """{"id":"usr_11111111-2222-3333-4444-555555555555","displayName":"Test User"}""";
    private sealed class MemoryStore : ISessionStore
    {
        public string? Value;
        public int Writes;
        public Task<string?> ReadAsync(CancellationToken token = default) => Task.FromResult(Value);
        public Task WriteAsync(string value, CancellationToken token = default) { Value=value;Writes++;return Task.CompletedTask; }
        public Task DeleteAsync(CancellationToken token = default) { Value=null;return Task.CompletedTask; }
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request);
    }
    private static HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK, bool cookie = false)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if(cookie) response.Headers.TryAddWithoutValidation("Set-Cookie", "auth=test_session_token; Path=/; Secure; HttpOnly");
        return response;
    }

    [Fact]
    public async Task RememberMe_SavesOnlyCookies_AndReusesThemOnRestore()
    {
        var store = new MemoryStore();
        var jar = new CookieContainer();
        using var http = new HttpClient(new Handler(request => {
            Assert.Equal("api.vrchat.cloud",request.RequestUri!.Host);
            Assert.Equal("/api/1/auth/user",request.RequestUri.AbsolutePath);
            Assert.Equal("Basic",request.Headers.Authorization?.Scheme);
            Assert.Equal("name%2Btest:p%40ss%3Aword", Encoding.UTF8.GetString(Convert.FromBase64String(request.Headers.Authorization!.Parameter!)));
            return Task.FromResult(Response(User,cookie:true));
        }));
        var auth = new VrchatAuthenticationService(http,jar,store);
        Assert.Equal(AuthenticationStatus.SignedIn,(await auth.LoginAsync("name+test","p@ss:word",true)).Status);
        Assert.NotNull(store.Value);
        Assert.DoesNotContain("p%40ss",store.Value);
        Assert.DoesNotContain("name%2B",store.Value);
        Assert.Contains("auth",store.Value);
        var restoredJar = new CookieContainer();
        using var restoreHttp = new HttpClient(new Handler(request => {
            Assert.Null(request.Headers.Authorization);
            Assert.Equal("test_session_token",restoredJar.GetCookies(new Uri("https://api.vrchat.cloud/"))["auth"]?.Value);
            return Task.FromResult(Response(User));
        }));
        Assert.Equal(AuthenticationStatus.SignedIn,(await new VrchatAuthenticationService(restoreHttp,restoredJar,store).RestoreSessionAsync()).Status);
    }

    [Fact]
    public async Task RememberMeUnchecked_RemovesOldSessionWithoutSavingNewOne()
    {
        var store = new MemoryStore { Value = "old" };
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Response(User,cookie:true))));
        var result = await new VrchatAuthenticationService(http,new(),store).LoginAsync("user","password",false);
        Assert.Equal(AuthenticationStatus.SignedIn,result.Status);
        Assert.Null(store.Value);Assert.Equal(0,store.Writes);
    }

    [Theory]
    [InlineData("totp","totp")]
    [InlineData("emailOtp","emailotp")]
    [InlineData("otp","otp")]
    public async Task Verification_FetchesCurrentUserBeforeSavingSession(string method,string path)
    {
        var store = new MemoryStore();
        var count = 0;
        using var http = new HttpClient(new Handler(async request => {
            count++;
            if(count==1) return Response("{\"requiresTwoFactorAuth\":[\""+method+"\"]}",cookie:true);
            if(count==2) {
                Assert.Equal("/api/1/auth/twofactorauth/"+path+"/verify",request.RequestUri!.AbsolutePath);
                Assert.Null(request.Headers.Authorization);
                Assert.Contains("001234",await request.Content!.ReadAsStringAsync());
                return Response("""{"verified":true}""");
            }
            return Response(User);
        }));
        var auth = new VrchatAuthenticationService(http,new(),store);
        Assert.Equal(AuthenticationStatus.VerificationRequired,(await auth.LoginAsync("user","secret",true)).Status);
        Assert.Equal(0,store.Writes);
        Assert.Equal(AuthenticationStatus.SignedIn,(await auth.VerifyAsync(method,"001234")).Status);
        Assert.Equal(1,store.Writes);
    }

    [Fact]
    public async Task ExpiredSession_IsRemoved()
    {
        var store = new MemoryStore { Value = """[{"Name":"auth","Value":"expired"}]""" };
        using var http = new HttpClient(new Handler(_ => Task.FromResult(Response("{}",HttpStatusCode.Unauthorized))));
        var auth = new VrchatAuthenticationService(http,new(),store);
        Assert.Equal(AuthenticationStatus.SignedOut,(await auth.RestoreSessionAsync()).Status);
        Assert.Null(store.Value);
        Assert.Null(auth.CurrentUser);
    }

    [Fact]
    public async Task NetworkFailure_DoesNotDeleteRememberedSession()
    {
        var saved = """[{"Name":"auth","Value":"saved"}]""";
        var store = new MemoryStore { Value=saved };
        using var http = new HttpClient(new Handler(_ => throw new HttpRequestException()));
        Assert.Equal(AuthenticationStatus.Failed,(await new VrchatAuthenticationService(http,new(),store).RestoreSessionAsync()).Status);
        Assert.Equal(saved,store.Value);
    }

    [Fact]
    public async Task RateLimit_PreventsImmediateCredentialRetry()
    {
        var count=0;
        using var http = new HttpClient(new Handler(_ => { count++; var response=Response("{}",HttpStatusCode.TooManyRequests);response.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromMinutes(1));return Task.FromResult(response); }));
        var auth=new VrchatAuthenticationService(http,new(),new MemoryStore());
        await auth.LoginAsync("user","password",true);
        await auth.LoginAsync("user","password",true);
        Assert.Equal(1,count);
    }

    [Fact]
    public async Task Logout_ClearsSavedSessionEvenWhenNetworkFails()
    {
        var store=new MemoryStore {Value="saved"};
        var jar=new CookieContainer();jar.Add(new Cookie("auth","value","/","api.vrchat.cloud"));
        using var http=new HttpClient(new Handler(_=>throw new HttpRequestException()));
        await new VrchatAuthenticationService(http,jar,store).LogoutAsync();
        Assert.Null(store.Value);Assert.Null(jar.GetCookies(new Uri("https://api.vrchat.cloud"))["auth"]);
    }
}

