using ELIXR.PlayerList.Core.Models;
using ELIXR.PlayerList.Infrastructure.Services;
namespace ELIXR.PlayerList.Core.Tests;

public sealed class RecentPlayersTests : IAsyncLifetime
{
    private const string Owner = "usr_ffffffff-ffff-ffff-ffff-ffffffffffff";
    private const string OtherOwner = "usr_eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee";
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 12, 0, 0, TimeSpan.Zero);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "elixr-recent-test-" + Guid.NewGuid().ToString("N"));
    private readonly List<RecentPlayersService> _services = [];
    private string? _account = Owner;
    private RecentPlayersService Service()
    { var service = new RecentPlayersService(() => _account, _directory, () => Now); _services.Add(service); return service; }
    private static Player Player(int id, DateTimeOffset? seen = null) => new($"usr_11111111-2222-3333-4444-{id:D12}", "Player " + id, seen ?? Now, seen ?? Now, []);
    public Task InitializeAsync() { Directory.CreateDirectory(_directory); return Task.CompletedTask; }
    public async Task DisposeAsync()
    {
        foreach (var service in _services) await service.DisposeAsync();
        foreach (var path in Directory.GetFiles(_directory)) File.Delete(path);
        Directory.Delete(_directory);
    }
    [Fact]
    public async Task DeduplicatesRenamesAndOrdersLatestEncounterFirst()
    {
        var service = Service(); await service.ActivateAsync(Owner);
        service.Observe(Player(1, Now.AddMinutes(-5))); service.Observe(Player(2, Now.AddMinutes(-3)));
        service.Observe(Player(1) with { DisplayName = "New name", Tags = [new("Fixture", TagCategory.Custom)] });
        var rows = await service.GetAsync();
        Assert.Equal(2, rows.Count); Assert.Equal("New name", rows[0].DisplayName); Assert.Equal(Now.AddMinutes(-5), rows[0].FirstSeen);
        service.Observe(Player(1, Now.AddMinutes(-10))); Assert.Equal(Now, service.Current[0].LastSeen);
    }
    [Fact]
    public async Task PersistsAcrossRestartsWithTagsAndLocalPortraitOnly()
    {
        var service = Service(); await service.ActivateAsync(Owner);
        service.Observe(Player(1) with { Tags = [new("Fixture", TagCategory.Group, GroupCode:"ELIXR.0042")], AvatarUrl = "https://untrusted.example/image" });
        await service.FlushAsync();
        var reloaded = Service(); await reloaded.ActivateAsync(Owner);
        var user = Assert.Single(reloaded.Current); Assert.Equal("Fixture", Assert.Single(user.Tags).Name); Assert.Null(user.AvatarUrl);
    }
    [Fact]
    public async Task AccountSwitchAndLogoutNeverExposeAnotherAccountsHistory()
    {
        var service = Service(); await service.ActivateAsync(Owner); service.Observe(Player(1));
        _account = OtherOwner; Assert.Empty(service.Current); service.Observe(Player(3));
        await service.ActivateAsync(OtherOwner); Assert.Empty(service.Current); service.Observe(Player(2));
        _account = Owner; await service.ActivateAsync(Owner); Assert.Equal(Player(1).VrchatUserId, Assert.Single(service.Current).VrchatUserId);
        _account = null; Assert.Empty(service.Current); service.Observe(Player(4));
    }
    [Fact]
    public async Task FiltersExpiredPartialOwnAndFutureEntriesAndBoundsHistory()
    {
        var service = Service(); await service.ActivateAsync(Owner);
        service.Observe(Player(1, Now.AddDays(-31))); service.Observe(Player(2, Now.AddDays(1)));
        service.Observe(Player(3) with { HasCompleteIdentity=false }); service.Observe(Player(4) with { VrchatUserId=Owner });
        Assert.Empty(service.Current);
        for (var i=1;i<=1001;i++) service.Observe(Player(i,Now.AddSeconds(-i)));
        Assert.Equal(1000,service.Current.Count); Assert.DoesNotContain(service.Current,p=>p.VrchatUserId==Player(1001).VrchatUserId);
    }
    [Fact]
    public async Task ClearIsPersisted()
    {
        var service = Service(); await service.ActivateAsync(Owner); service.Observe(Player(1)); await service.ClearAsync();
        var reloaded=Service(); await reloaded.ActivateAsync(Owner); Assert.Empty(reloaded.Current);
    }
    [Fact]
    public async Task CorruptHistoryIsPreservedAndDoesNotBreakNewEncounters()
    {
        var service = Service(); await service.ActivateAsync(Owner); service.Observe(Player(1)); await service.FlushAsync();
        var path=Assert.Single(Directory.GetFiles(_directory,"*.json")); await File.WriteAllTextAsync(path,"not json");
        var reloaded=Service(); await reloaded.ActivateAsync(Owner); Assert.NotNull(reloaded.Notice);
        Assert.Single(Directory.GetFiles(_directory,"*.unreadable-*"));
        reloaded.Observe(Player(2)); Assert.Single(reloaded.Current); await reloaded.FlushAsync();
    }
    [Fact]
    public async Task LogReplayCapturesPlayersWhoLeaveBeforeTheFirstRosterSnapshot()
    {
        // This service must capture log events, not only the final current-instance list.
        var service = Service(); await service.ActivateAsync(Owner);
        var log=Path.Combine(_directory,"output_log_2026-10-28.txt");
        var date=Now.LocalDateTime.ToString("yyyy.MM.dd HH:mm:ss");
        await File.WriteAllTextAsync(log,$"{date} Joining or Creating Room: Test\n{date} OnPlayerJoined Departed ({Player(1).VrchatUserId})\n{date} OnPlayerLeft Departed ({Player(1).VrchatUserId})\n");
        await using var roster=new LocalVrchatLogPlayerListService(_directory,()=>true,TimeSpan.FromHours(1));
        roster.Encountered+=(_,player)=>service.Observe(player);
        await roster.StartAsync(); Assert.Empty(roster.Current.Players); Assert.Single(service.Current);
    }
}
