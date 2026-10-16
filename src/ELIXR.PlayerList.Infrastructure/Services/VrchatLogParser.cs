using System.Text.RegularExpressions;
namespace ELIXR.PlayerList.Infrastructure.Services;

public enum VrchatLogEventType { PlayerJoined, PlayerLeft, RoomJoined, RoomLeft, RoomNamed, RoomEntered }
public sealed record VrchatLogEvent(VrchatLogEventType Type, string? DisplayName = null, string? UserId = null, string? Location = null, string? WorldName = null);

public static partial class VrchatLogParser
{
    [GeneratedRegex(@"OnPlayer(Joined|Left)(?:\s*[:\-])?\s+(.+?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex PlayerRegex();
    [GeneratedRegex(@"\s*\((usr_[\w-]+)\)(?:\s.*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex IdentityRegex();
    [GeneratedRegex(@"(wrld_[\w-]+:[^\s]+)", RegexOptions.IgnoreCase)]
    private static partial Regex LocationRegex();

    public static VrchatLogEvent? Parse(string line)
    {
        var player = PlayerRegex().Match(line);
        if (player.Success)
        {
            var name = player.Groups[2].Value;
            var identity = IdentityRegex().Match(name);
            var id = identity.Success ? identity.Groups[1].Value.ToLowerInvariant() : null;
            if (identity.Success) name = name[..identity.Index].TrimEnd();
            return new(player.Groups[1].Value.Equals("Joined", StringComparison.OrdinalIgnoreCase) ? VrchatLogEventType.PlayerJoined : VrchatLogEventType.PlayerLeft, name.Trim(), id);
        }
        const string roomNameMarker = "Joining or Creating Room:";
        var nameIndex = line.IndexOf(roomNameMarker, StringComparison.OrdinalIgnoreCase);
        var location = LocationRegex().Match(line);
        if (nameIndex >= 0)
            return new(VrchatLogEventType.RoomNamed, Location: location.Success ? location.Value : null, WorldName: line[(nameIndex+roomNameMarker.Length)..].Trim());
        if (line.Contains("Joining wrld_", StringComparison.OrdinalIgnoreCase) && location.Success)
            return new(VrchatLogEventType.RoomJoined, Location: location.Value);
        if (line.Contains("OnEnteredRoom", StringComparison.OrdinalIgnoreCase) || line.Contains("Entered Room", StringComparison.OrdinalIgnoreCase))
            return new(VrchatLogEventType.RoomEntered, Location: location.Success ? location.Value : null);
        if (line.Contains("OnLeftRoom", StringComparison.OrdinalIgnoreCase) || line.Contains("Left Room", StringComparison.OrdinalIgnoreCase))
            return new(VrchatLogEventType.RoomLeft);
        return null;
    }
}

