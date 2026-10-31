using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ELIXR.PlayerList.Core.Models;
using ELIXR.PlayerList.Core.Services;

namespace ELIXR.PlayerList.Infrastructure.Services;

// Per-account, local-only encounter snapshots. Network searches never call Observe.
public sealed class RecentPlayersService : IRecentPlayersService, IAsyncDisposable
{
    private readonly Func<string?> _accountId;
    private readonly Func<DateTimeOffset> _now;
    private readonly string _directory;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _io = new(1, 1);
    private readonly Dictionary<string, Player> _players = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    private string? _account;
    private long _revision, _savedRevision;
    public string? Notice { get; private set; }
    public event EventHandler? Changed;
    public RecentPlayersService(Func<string?> accountId, string? directory = null, Func<DateTimeOffset>? now = null)
    {
        _accountId = accountId;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _directory = Path.GetFullPath(directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ELIXR", "PlayerList", "RecentPlayers"));
        _worker = Task.Run(PersistAsync);
    }
    public IReadOnlyList<Player> Current
    {
        get { lock (_gate) return SameAccount() ? _players.Values.Where(p => p.LastSeen >= _now().AddDays(-30)).OrderByDescending(p => p.LastSeen).Take(1000).ToArray() : []; }
    }
    private bool SameAccount() => _account is not null && string.Equals(_account, _accountId(), StringComparison.OrdinalIgnoreCase);
    private string FileFor(string account) => Path.Combine(_directory, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(account))) + ".json");
    public async Task ActivateAsync(string account, CancellationToken token = default)
    {
        if (!TagFeedParser.ValidId(account, "usr_")) throw new InvalidOperationException("Invalid history account.");
        await FlushAsync(token);
        await _io.WaitAsync(token);
        try
        {
            account = account.ToLowerInvariant();
            lock (_gate) { if (_account == account) return; _account = account; _players.Clear(); _revision = _savedRevision = 0; Notice = null; }
            var path = FileFor(account);
            if (File.Exists(path))
            {
                try
                {
                    if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new JsonException("History file exceeds the size limit.");
                    await using var stream = File.OpenRead(path);
                    var entries = await JsonSerializer.DeserializeAsync<Player[]>(stream, cancellationToken: token) ?? [];
                    lock (_gate)
                    {
                        foreach (var player in entries.Take(1000).Where(p => p is not null && Valid(p)))
                            _players[player.VrchatUserId] = Safe(player);
                        _revision++;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
                {
                    // Preserve the unreadable original instead of silently replacing someone's history.
                    try { if (File.Exists(path)) File.Move(path, path + ".unreadable-" + Guid.NewGuid().ToString("N")); }
                    catch (Exception backupError) when (backupError is IOException or UnauthorizedAccessException) { }
                    Notice = "Saved recent players could not be read. New encounters remain available during this session.";
                }
            }
        }
        finally { _io.Release(); }
        Changed?.Invoke(this, EventArgs.Empty);
    }
    private bool Valid(Player player) => TagFeedParser.ValidId(player.VrchatUserId ?? "", "usr_") && player.HasCompleteIdentity &&
        !string.IsNullOrWhiteSpace(player.DisplayName) && player.LastSeen >= _now().AddDays(-30) && player.LastSeen <= _now().AddMinutes(5) &&
        !string.Equals(player.VrchatUserId, _account, StringComparison.OrdinalIgnoreCase);
    private static Player Safe(Player player) => player with {
        IsFriend = false,
        DisplayName = player.DisplayName.Length > 256 ? player.DisplayName[..256] : player.DisplayName,
        Tags = (player.Tags ?? []).Where(t => t is not null).Take(64).ToArray(),
        AvatarUrl = player.AvatarUrl is { } url && System.Text.RegularExpressions.Regex.IsMatch(url, @"^https://avatars\.elixr\.example/[0-9a-f]{64}\.(?:png|jpg|jpeg|webp|gif)$") ? url : null
    };
    public void Observe(Player player)
    {
        bool changed;
        lock (_gate)
        {
            if (!SameAccount() || !Valid(player)) return;
            player = Safe(player);
            if (_players.TryGetValue(player.VrchatUserId, out var previous))
            {
                if (player.LastSeen < previous.LastSeen) return;
                player = player with { FirstSeen = previous.FirstSeen < player.FirstSeen ? previous.FirstSeen : player.FirstSeen,
                    Tags = player.Tags.Count > 0 ? player.Tags : previous.Tags, AvatarUrl = player.AvatarUrl ?? previous.AvatarUrl };
                if (JsonSerializer.Serialize(previous) == JsonSerializer.Serialize(player)) return;
            }
            _players[player.VrchatUserId] = player;
            foreach (var id in _players.Values.OrderByDescending(p => p.LastSeen).Skip(1000).Select(p => p.VrchatUserId).ToArray()) _players.Remove(id);
            foreach (var id in _players.Values.Where(p => p.LastSeen < _now().AddDays(-30)).Select(p => p.VrchatUserId).ToArray()) _players.Remove(id);
            _revision++; changed = true;
        }
        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }
    public Task<IReadOnlyList<Player>> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
    public async Task RecordAsync(IEnumerable<Player> players, CancellationToken cancellationToken = default)
    { foreach (var player in players) Observe(player); await FlushAsync(cancellationToken); }
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate) { if (!SameAccount()) return; _players.Clear(); _revision++; }
        Changed?.Invoke(this, EventArgs.Empty); await FlushAsync(cancellationToken);
    }
    public async Task FlushAsync(CancellationToken token = default)
    {
        await _io.WaitAsync(token);
        try
        {
            string? account; Player[] snapshot; long revision;
            lock (_gate)
            {
                if (_account is null || _revision == _savedRevision) return;
                account = _account; revision = _revision;
                snapshot = _players.Values.OrderByDescending(p => p.LastSeen).Take(1000).ToArray();
            }
            try
            {
                Directory.CreateDirectory(_directory);
                var path = FileFor(account);
                await using (var stream = File.Create(path + ".tmp")) await JsonSerializer.SerializeAsync(stream, snapshot, cancellationToken: token);
                File.Move(path + ".tmp", path, true);
                lock (_gate) { if (_account == account) _savedRevision = revision; }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (Notice != "Recent players could not be saved. Check local folder permissions.")
                {
                    Notice = "Recent players could not be saved. Check local folder permissions.";
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            }
        }
        finally { _io.Release(); }
    }
    private async Task PersistAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try { while (await timer.WaitForNextTickAsync(_stop.Token)) await FlushAsync(_stop.Token); }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }
    public async ValueTask DisposeAsync()
    { _stop.Cancel(); await _worker; await FlushAsync(); _stop.Dispose(); }
}
