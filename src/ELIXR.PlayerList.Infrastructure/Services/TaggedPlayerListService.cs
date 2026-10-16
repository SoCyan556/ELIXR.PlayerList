using ELIXR.PlayerList.Core.Models;
using ELIXR.PlayerList.Core.Services;

namespace ELIXR.PlayerList.Infrastructure.Services;

// Roster detection stays independent of network enrichment: never delay or resurrect a player.
public sealed class TaggedPlayerListService(IPlayerListService inner, ITagService tags, Func<bool> signedIn) : IPlayerListService
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<string, IReadOnlyList<PlayerTag>> _tags = new(StringComparer.OrdinalIgnoreCase);
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
            foreach (var id in _tags.Keys.Where(id => !present.Contains(id)).ToArray()) _tags.Remove(id);
            state = _current = raw with { Players = raw.Players.Select(p => p with { Tags = _tags.GetValueOrDefault(p.VrchatUserId) ?? p.Tags }).ToArray() };
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
                {
                    if (tags is GitHubTagService catalog)
                    {
                        var status = catalog.Status;
                        await catalog.RefreshAsync(token);
                        if (!ReferenceEquals(status, catalog.Status)) Publish();
                    }
                    foreach (var player in inner.Current.Players.Where(p => p.HasCompleteIdentity).ToArray())
                    {
                        if (!signedIn()) break;
                        var result = await tags.GetTagsAsync(player.VrchatUserId, token);
                        if (!signedIn()) break;
                        bool changed;
                        lock (_gate)
                        {
                            changed = !_tags.TryGetValue(player.VrchatUserId, out var previous) || !SameTags(previous, result);
                            _tags[player.VrchatUserId] = result;
                        }
                        if (changed) Publish();
                    }
                }
                else { lock (_gate) _tags.Clear(); }
                await Task.Delay(2000, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
    private static bool SameTags(IReadOnlyList<PlayerTag> a, IReadOnlyList<PlayerTag> b) =>
        System.Text.Json.JsonSerializer.Serialize(a) == System.Text.Json.JsonSerializer.Serialize(b);
    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        inner.Changed -= OnChanged;
        if (_worker is not null) await _worker;
        await inner.DisposeAsync();
        _stop.Dispose();
    }
}
