using ELIXR.PlayerList.Core.Models;
using ELIXR.PlayerList.Core.Services;

namespace ELIXR.PlayerList.Infrastructure.Services;

// Independent of tag downloading: a slow public feed must not delay portraits (or the roster).
public sealed class ProfileImagePlayerListService(IPlayerListService inner, IPlayerImageService images, Func<bool> signedIn, Func<string?>? accountId = null) : IPlayerListService
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<string, string> _images = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> _friends = new(StringComparer.OrdinalIgnoreCase);
    private string? _account;
    private string? Account => accountId is null ? signedIn() ? "current" : null : accountId();
    private Task? _worker;
    private InstanceState _current = inner.Current;
    public InstanceState Current {
        get { lock (_gate) return _account == Account ? _current : _current with { Players = _current.Players.Select(p => p with { IsFriend = false }).ToArray() }; }
    }
    public event EventHandler<InstanceState>? Changed;
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_worker is not null) return;
        inner.Changed += OnChanged;
        await inner.StartAsync(cancellationToken);
        Publish();
        _worker = Task.Run(() => EnrichAsync(_stop.Token));
    }
    private void OnChanged(object? sender, InstanceState state) => Publish();
    private void Publish()
    {
        InstanceState state;
        lock (_gate)
        {
            var raw = inner.Current;
            var account = Account;
            if (_account != account) { _images.Clear(); _friends.Clear(); _account = account; }
            var present = raw.Players.Select(p => p.VrchatUserId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var id in _images.Keys.Where(id => !present.Contains(id)).ToArray()) _images.Remove(id);
            foreach (var id in _friends.Keys.Where(id => !present.Contains(id)).ToArray()) _friends.Remove(id);
            state = _current = raw with { Players = raw.Players.Select(p => p with { AvatarUrl = _images.GetValueOrDefault(p.VrchatUserId) ?? p.AvatarUrl, IsFriend = account is not null && _friends.GetValueOrDefault(p.VrchatUserId) }).ToArray() };
        }
        Changed?.Invoke(this, state);
    }
    private async Task EnrichAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (signedIn())
                    foreach (var player in inner.Current.Players.Where(p => p.HasCompleteIdentity).ToArray())
                    {
                        if (!signedIn()) break;
                        if (!inner.Current.Players.Any(p => p.VrchatUserId == player.VrchatUserId)) continue;
                        var account = Account;
                        var image = await images.GetAsync(player.VrchatUserId, token);
                        if (!signedIn() || account != Account) break;
                        var friend = images is IPlayerRelationshipService relationships && relationships.IsFriend(player.VrchatUserId) == true;
                        bool changed;
                        lock (_gate) {
                            var accountChanged = _account != account;
                            if (accountChanged) { _images.Clear(); _friends.Clear(); _account = account; }
                            changed = accountChanged || _friends.GetValueOrDefault(player.VrchatUserId) != friend || image is not null && _images.GetValueOrDefault(player.VrchatUserId) != image;
                            _friends[player.VrchatUserId] = friend;
                            if (image is not null) _images[player.VrchatUserId] = image;
                        }
                        if (changed) Publish();
                    }
                else
                {
                    bool changed;
                    lock (_gate) { changed = _images.Count > 0 || _friends.Count > 0; _images.Clear(); _friends.Clear(); _account = null; }
                    if (changed) Publish();
                }
                await Task.Delay(500, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
    public async ValueTask DisposeAsync()
    {
        _stop.Cancel(); inner.Changed -= OnChanged;
        if (_worker is not null) await _worker;
        await inner.DisposeAsync(); _stop.Dispose();
    }
}
