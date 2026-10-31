namespace ELIXR.PlayerList.Core.Models;

public enum TagCategory { Malicious, Custom, Group }
public sealed record TagColorRun(string Text, string? Color);
public sealed record PlayerTag(string Name, TagCategory Category, string? Color = null,
    IReadOnlyList<string>? Gradient = null, string? Animation = null, double Speed = 1,
    double Amplitude = 1, bool Rainbow = false, IReadOnlyList<TagColorRun>? Runs = null,
    string? Source = null, int Priority = 100, string? GroupId = null, string? GroupName = null, string? GroupCode = null);
public sealed record Player(string VrchatUserId, string DisplayName, DateTimeOffset FirstSeen, DateTimeOffset LastSeen, IReadOnlyList<PlayerTag> Tags, bool HasCompleteIdentity = true, string? AvatarUrl = null, bool IsFriend = false);
public sealed record InstanceState(string? WorldId, string? InstanceId, string? WorldName, bool IsActive, IReadOnlyList<Player> Players)
{
    public static InstanceState Offline { get; } = new(null, null, null, false, []);
}
public sealed record AuthenticatedUser(string VrchatUserId, string DisplayName);
public enum AuthenticationStatus { SignedOut, VerificationRequired, SignedIn, Failed }
public sealed record AuthenticationResult(AuthenticationStatus Status, AuthenticatedUser? User = null, IReadOnlyList<string>? VerificationMethods = null, string? Message = null);
