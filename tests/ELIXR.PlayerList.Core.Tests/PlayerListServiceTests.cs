using ELIXR.PlayerList.Infrastructure.Services;
namespace ELIXR.PlayerList.Core.Tests;

public sealed class PlayerListServiceTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "elixr-log-tests-" + Guid.NewGuid().ToString("N"));
    private LocalVrchatLogPlayerListService? _service;
    private string Log(string suffix = "2026-10-16_10-00-00") => Path.Combine(_directory, "output_log_" + suffix + ".txt");
    public Task InitializeAsync() { Directory.CreateDirectory(_directory); return Task.CompletedTask; }
    public async Task DisposeAsync()
    {
        if (_service is not null) await _service.DisposeAsync();
        foreach (var path in Directory.EnumerateFiles(_directory)) File.Delete(path);
        Directory.Delete(_directory);
    }
    private LocalVrchatLogPlayerListService Service(Func<bool>? running = null) =>
        _service = new(_directory, running ?? (() => true), TimeSpan.FromHours(1));

    [Fact]
    public async Task Startup_ReplaysExistingLog_AndOnlyPublishesLatestInstance()
    {
        await File.WriteAllTextAsync(Log(), """
            Joining wrld_old:1
            Joining or Creating Room: Old World
            OnPlayerJoined Old Player (usr_old)
            Joining wrld_new:2
            Joining or Creating Room: New World
            OnPlayerJoined Existing Player (usr_existing)
            OnPlayerJoined Already Left (usr_left)
            OnPlayerLeft Already Left (usr_left)

            """);
        var service=Service();
        var notifications=0;
        service.Changed+=(_,_)=>notifications++;
        await service.StartAsync();
        var player=Assert.Single(service.Current.Players);
        Assert.Equal("usr_existing",player.VrchatUserId);
        Assert.Equal("New World",service.Current.WorldName);
        Assert.Equal("wrld_new",service.Current.WorldId);
        Assert.Equal("2",service.Current.InstanceId);
        Assert.Equal(1,notifications);
    }

    [Fact]
    public async Task AppendedEvents_TrackJoinLeave_WithoutDuplicateRoomReset()
    {
        await File.WriteAllTextAsync(Log(),"Joining or Creating Room: World\nOnPlayerJoined First (usr_first)\n");
        var service=Service();
        await service.StartAsync();
        await File.AppendAllTextAsync(Log(),"OnEnteredRoom\nOnPlayerJoined Second (usr_second)\n");
        await service.RefreshAsync();
        Assert.Equal(2,service.Current.Players.Count);
        await File.AppendAllTextAsync(Log(),"OnPlayerLeft First (usr_first)\n");
        await service.RefreshAsync();
        Assert.Equal("usr_second",Assert.Single(service.Current.Players).VrchatUserId);
    }

    [Fact]
    public async Task SplitUtf8AndIncompleteLines_AreRetainedUntilNewline()
    {
        await File.WriteAllTextAsync(Log(),"Joining or Creating Room: World\n");
        var service=Service(); await service.StartAsync();
        var bytes=System.Text.Encoding.UTF8.GetBytes("OnPlayerJoined 星 (usr_star)\n");
        var split=Array.IndexOf(bytes,(byte)0xE6)+1;
        await using(var stream=new FileStream(Log(),FileMode.Append,FileAccess.Write,FileShare.ReadWrite))
            await stream.WriteAsync(bytes.AsMemory(0,split));
        await service.RefreshAsync();
        Assert.Empty(service.Current.Players);
        await using(var stream=new FileStream(Log(),FileMode.Append,FileAccess.Write,FileShare.ReadWrite))
            await stream.WriteAsync(bytes.AsMemory(split,bytes.Length-split-1));
        await service.RefreshAsync();
        Assert.Empty(service.Current.Players);
        await File.AppendAllTextAsync(Log(),"\n");
        await service.RefreshAsync();
        Assert.Equal("星",Assert.Single(service.Current.Players).DisplayName);
    }

    [Fact]
    public async Task Rotation_DiscardsOldInstance_AndIgnoresLaterWritesToOldLog()
    {
        await File.WriteAllTextAsync(Log(),"OnPlayerJoined First (usr_first)\n");
        var service=Service(); await service.StartAsync();
        var newLog=Log("2026-10-16_11-00-00");
        await File.WriteAllTextAsync(newLog,"Joining or Creating Room: Next\nOnPlayerJoined Next Player (usr_next)\n");
        await service.RefreshAsync();
        await File.AppendAllTextAsync(Log(),"OnPlayerJoined Stale (usr_stale)\n");
        await service.RefreshAsync();
        Assert.Equal("usr_next",Assert.Single(service.Current.Players).VrchatUserId);
    }

    [Fact]
    public async Task MissingDirectory_RecoversWhenClientStarts_AndClearsOnExit()
    {
        Directory.Delete(_directory);
        var running=false;
        var service=Service(()=>running);
        await service.StartAsync();
        Assert.False(service.Current.IsActive);
        Directory.CreateDirectory(_directory);running=true;
        await File.WriteAllTextAsync(Log(),"OnPlayerJoined New Player (usr_new)\n");
        await service.RefreshAsync();
        Assert.Single(service.Current.Players);
        running=false;
        await service.RefreshAsync();
        Assert.False(service.Current.IsActive);Assert.Empty(service.Current.Players);
    }

    [Fact]
    public async Task Truncation_ResetsDecoderAndRoster()
    {
        await File.WriteAllTextAsync(Log(),"OnPlayerJoined First Long Name (usr_first)\n");
        var service=Service(); await service.StartAsync();
        await File.WriteAllTextAsync(Log(),"OnPlayerJoined Second\n");
        await service.RefreshAsync();
        Assert.Equal("Second",Assert.Single(service.Current.Players).DisplayName);
    }

    [Fact]
    public async Task PartialIdentity_IsReconciled_AndNameOnlyLeaveRemovesResolvedPlayer()
    {
        await File.WriteAllTextAsync(Log(),"OnPlayerJoined Test Player\n");
        var service=Service();await service.StartAsync();
        await File.AppendAllTextAsync(Log(),"OnPlayerJoined Test Player (usr_resolved)\n");
        await service.RefreshAsync();
        Assert.True(Assert.Single(service.Current.Players).HasCompleteIdentity);
        await File.AppendAllTextAsync(Log(),"OnPlayerLeft Test Player\n");
        await service.RefreshAsync();
        Assert.Empty(service.Current.Players);
    }

    [Fact]
    public async Task QuietLog_DoesNotPublishOrRedraw()
    {
        await File.WriteAllTextAsync(Log(),"OnPlayerJoined Player (usr_player)\n");
        var service=Service();await service.StartAsync();
        var notifications=0;service.Changed+=(_,_)=>notifications++;
        await service.RefreshAsync();await service.RefreshAsync();
        Assert.Equal(0,notifications);
    }

    [Fact]
    public async Task LateInstanceIdentity_DoesNotDiscardAlreadyDetectedPlayers()
    {
        await File.WriteAllTextAsync(Log(),"Joining or Creating Room: World\nOnPlayerJoined First (usr_first)\nOnEnteredRoom wrld_world:123\n");
        var service=Service();await service.StartAsync();
        Assert.Single(service.Current.Players);
        Assert.Equal("World",service.Current.WorldName);
        Assert.Equal("wrld_world",service.Current.WorldId);
    }
}

