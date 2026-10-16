using System.Net;
using ELIXR.PlayerList.Infrastructure.Services;

namespace ELIXR.PlayerList.Core.Tests;

public class UsageRegistrationTests
{
    private sealed class Handler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Calls;
        public string? Body;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal("https://elixrvr.com/api/player-list/usage", request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            Assert.False(request.Headers.Contains("Cookie"));
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status);
        }
    }
    [Fact]
    public async Task SendsOnlyOwnIdAndRemembersSuccess()
    {
        var directory = Path.Combine(Path.GetTempPath(), "elixr-usage-test-" + Guid.NewGuid());
        using var handler = new Handler(HttpStatusCode.Created); using var http = new HttpClient(handler);
        var service = new UsageRegistrationService(http, directory);
        const string id = "usr_00000000-0000-0000-0000-000000000001";
        await service.RegisterAsync(id); await service.RegisterAsync(id);
        Assert.Equal(1, handler.Calls); Assert.Equal("{\"vrchatUserId\":\"" + id + "\"}", handler.Body);
        foreach(var file in Directory.GetFiles(directory)) File.Delete(file);
        Directory.Delete(directory);
    }
    [Fact]
    public async Task OfflineFailureDoesNotMarkRegistrationAndInvalidIdsNeverSend()
    {
        var directory = Path.Combine(Path.GetTempPath(), "elixr-usage-test-" + Guid.NewGuid());
        using var handler = new Handler(HttpStatusCode.ServiceUnavailable); using var http = new HttpClient(handler);
        var service = new UsageRegistrationService(http, directory);
        await service.RegisterAsync("invalid"); Assert.Equal(0, handler.Calls);
        await service.RegisterAsync("usr_00000000-0000-0000-0000-000000000001");
        Assert.False(Directory.Exists(directory));
    }
}
