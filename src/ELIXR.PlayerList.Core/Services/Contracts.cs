using ELIXR.PlayerList.Core.Models;
namespace ELIXR.PlayerList.Core.Services;

public interface IPlayerListService : IAsyncDisposable
{
    InstanceState Current { get; }
    event EventHandler<InstanceState>? Changed;
    Task StartAsync(CancellationToken cancellationToken = default);
}
public interface IAuthenticationService
{
    AuthenticatedUser? CurrentUser { get; }
    Task<AuthenticationResult> LoginAsync(string username, string password, bool remember, CancellationToken token = default);
    Task<AuthenticationResult> VerifyAsync(string method, string code, CancellationToken token = default);
    Task<AuthenticationResult> RestoreSessionAsync(CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}
public interface ITagService { Task<IReadOnlyList<PlayerTag>> GetTagsAsync(string vrchatUserId, CancellationToken cancellationToken = default); }
public interface IPlayerImageService { Task<string?> GetAsync(string vrchatUserId, CancellationToken cancellationToken = default); }
public interface IPlayerRelationshipService { bool? IsFriend(string vrchatUserId); }
public interface IRecentPlayersService
{
    Task<IReadOnlyList<Player>> GetAsync(CancellationToken cancellationToken = default);
    Task RecordAsync(IEnumerable<Player> players, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
public interface ISettingsService
{
    AppSettings Current { get; }
    Task LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
public interface IOverlayService
{
    bool IsAvailable { get; }
    bool IsVisible { get; }
    Task SetVisibleAsync(bool visible, CancellationToken cancellationToken = default);
}
public interface IUpdateService { Task<Version?> GetAvailableVersionAsync(CancellationToken cancellationToken = default); }
public interface IUserRegistrationService { Task RegisterVerifiedUserAsync(CancellationToken cancellationToken = default); }
