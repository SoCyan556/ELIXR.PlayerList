using ELIXR.PlayerList.Infrastructure.Services;
namespace ELIXR.PlayerList.Core.Tests;
public sealed class VrchatLogParserTests
{
    [Fact]
    public void Parse_JoinWithIdentity()
    {
        var result = VrchatLogParser.Parse("2026.10.16 OnPlayerJoined AlexVR (usr_1234-abcd)");
        Assert.NotNull(result);
        Assert.Equal(VrchatLogEventType.PlayerJoined, result.Type);
        Assert.Equal("AlexVR", result.DisplayName);
        Assert.Equal("usr_1234-abcd", result.UserId);
    }

    [Fact]
    public void Parse_JoinWithoutIdentity_PreservesPartialPlayer()
    {
        var result = VrchatLogParser.Parse("OnPlayerJoined: Bunni");
        Assert.NotNull(result);
        Assert.Null(result.UserId);
        Assert.Equal("Bunni", result.DisplayName);
    }
}
