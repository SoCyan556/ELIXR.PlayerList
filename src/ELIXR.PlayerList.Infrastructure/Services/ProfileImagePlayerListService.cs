using ELIXR.PlayerList.Core.Models;
using ELIXR.PlayerList.Core.Services;

namespace ELIXR.PlayerList.Infrastructure.Services;

// Independent of tag downloading: a slow public feed must not delay portraits (or the roster).
public sealed class ProfileImagePlayerListService(IPlayerListService inner, IPlayerImageService images, Func<bool> signedIn) : IPlayerListService
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<string, string> _images = new(StringComparer.OrdinalIgnoreCase);
    private Task? _worker;
    private InstanceState _current = inner.Current;
    public InstanceState Current { get { lock (_gate) return _current; } }
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
            var present = raw.Players.Select(p => p.VrchatUserId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var id in _images.Keys.Where(id => !present.Contains(id)).ToArray()) _images.Remove(id);
            state = _current = raw with { Players = raw.Players.Select(p => p with { AvatarUrl = _images.GetValueOrDefault(p.VrchatUserId) ?? p.AvatarUrl }).ToArray() };
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
                        var image = await images.GetAsync(player.VrchatUserId, token);
                        if (!signedIn()) break;
                        if (image is null) continue;
                        bool changed;
                        lock (_gate) { changed = _images.GetValueOrDefault(player.VrchatUserId) != image; _images[player.VrchatUserId] = image; }
                        if (changed) Publish();
                    }
                else
                {
                    bool changed;
                    lock (_gate) { changed = _images.Count > 0; _images.Clear(); }
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
