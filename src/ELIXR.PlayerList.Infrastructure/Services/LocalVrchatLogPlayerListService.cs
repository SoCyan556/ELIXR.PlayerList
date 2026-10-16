using System.Diagnostics;
using System.Globalization;
using System.Text;
using ELIXR.PlayerList.Core.Models;
using ELIXR.PlayerList.Core.Services;
namespace ELIXR.PlayerList.Infrastructure.Services;

// One bounded streaming read at startup reconstructs the latest instance, then only new bytes are read.
// A lightweight timer also recovers from missing directories, missed filesystem notifications and rotation.
public sealed class LocalVrchatLogPlayerListService : IPlayerListService
{
    private readonly string _directory;
    private readonly Func<bool> _isClientRunning;
    private readonly TimeSpan _interval;
    private readonly Dictionary<string, Player> _players = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
    private readonly StringBuilder _line = new();
    private Task? _worker;
    private string? _currentLog, _worldId, _instanceId, _worldName;
    private long _position;
    private bool _active, _awaitingRoomName, _discardingLine, _dirty, _started, _disposed;
    public InstanceState Current { get; private set; } = InstanceState.Offline;
    public event EventHandler<InstanceState>? Changed;

    public LocalVrchatLogPlayerListService(string? directory = null, Func<bool>? isClientRunning = null, TimeSpan? interval = null)
    {
        _directory = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low", "VRChat", "VRChat");
        _isClientRunning = isClientRunning ?? ClientRunning;
        _interval = interval ?? TimeSpan.FromMilliseconds(750);
    }

    private static bool ClientRunning()
    {
        var processes = Process.GetProcessesByName("VRChat");
        try { return processes.Length > 0; }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) return;
        _started = true;
        try
        {
            await RefreshAsync(cancellationToken);
            _worker = WatchAsync();
        }
        catch { _started = false; throw; }
    }

    private async Task WatchAsync()
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(_lifetime.Token))
                await RefreshAsync(_lifetime.Token);
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
    }

    // Also permits deterministic tests without timing-sensitive FileSystemWatcher events.
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_isClientRunning() || !Directory.Exists(_directory))
            {
                if (_currentLog is not null || _active || _players.Count > 0) ResetLog(null);
            }
            else
            {
                // VRChat's timestamped filenames sort by session creation, so late writes to older logs cannot steal focus.
                var latest = Directory.EnumerateFiles(_directory, "output_log_*.txt")
                    .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
                if (!string.Equals(latest, _currentLog, StringComparison.OrdinalIgnoreCase)) ResetLog(latest);
                if (_currentLog is not null) await ReadAsync(cancellationToken);
            }
            if (_dirty)
            {
                Current = new(_worldId, _instanceId, _worldName, _active, _players.Values.ToArray());
                _dirty = false;
                Changed?.Invoke(this, Current);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Retry on the next tick; transient log locks must not destroy the current roster or terminate observation.
        }
        finally { _gate.Release(); }
    }

    private void ResetLog(string? path)
    {
        _currentLog = path;
        _position = 0;
        _decoder.Reset();
        _line.Clear();
        _discardingLine = false;
        ResetRoom();
    }

    private void ResetRoom()
    {
        _players.Clear();
        _worldId = _instanceId = _worldName = null;
        _active = _awaitingRoomName = false;
        _dirty = true;
    }

    private async Task ReadAsync(CancellationToken token)
    {
        await using var stream = new FileStream(_currentLog!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (_position > stream.Length) ResetLog(_currentLog);
        stream.Position = _position;
        if (stream.Position == stream.Length) return;
        var bytes = new byte[65536];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        int count;
        while ((count = await stream.ReadAsync(bytes, token).ConfigureAwait(false)) > 0)
        {
            var length = _decoder.GetChars(bytes, 0, count, chars, 0, false);
            for (var i = 0; i < length; i++)
            {
                var ch = chars[i];
                if (ch == '\n')
                {
                    if (!_discardingLine) Apply(_line.ToString().TrimEnd('\r'));
                    _line.Clear();
                    _discardingLine = false;
                }
                else if (!_discardingLine)
                {
                    if (_line.Length >= 1024 * 1024) { _line.Clear(); _discardingLine = true; }
                    else _line.Append(ch);
                }
            }
            _position = stream.Position;
        }
        // Retain incomplete UTF-8 characters and unfinished lines until the next append.
    }

    private void Apply(string line)
    {
        var item = VrchatLogParser.Parse(line);
        if (item is null) return;
        var seen = line.Length >= 19 && DateTime.TryParseExact(line[..19], "yyyy.MM.dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var timestamp)
            ? new DateTimeOffset(timestamp) : DateTimeOffset.Now;
        switch (item.Type)
        {
            case VrchatLogEventType.RoomJoined:
                ResetRoom();
                SetLocation(item.Location);
                _active = _awaitingRoomName = true;
                break;
            case VrchatLogEventType.RoomNamed:
                if (!_awaitingRoomName) ResetRoom();
                if (item.Location is not null) SetLocation(item.Location);
                _worldName = item.WorldName;
                _active = true;
                _awaitingRoomName = false;
                break;
            case VrchatLogEventType.RoomEntered:
                if (item.Location is not null)
                {
                    if (_worldId is not null && _worldId + ":" + _instanceId != item.Location) ResetRoom();
                    SetLocation(item.Location);
                }
                _active = true;
                break;
            case VrchatLogEventType.RoomLeft:
                ResetRoom();
                break;
            case VrchatLogEventType.PlayerJoined:
                var name = item.DisplayName ?? "Unknown";
                var partialKey = "partial::" + name;
                var key = item.UserId ?? partialKey;
                _players.TryGetValue(key, out var old);
                if (item.UserId is not null) _players.Remove(partialKey);
                _players[key] = new(item.UserId ?? "", name, old?.FirstSeen ?? seen, seen, old?.Tags ?? [], item.UserId is not null);
                _active = true;
                break;
            case VrchatLogEventType.PlayerLeft:
                if (item.UserId is not null) _players.Remove(item.UserId);
                else
                {
                    foreach (var match in _players.Where(p => p.Value.DisplayName.Equals(item.DisplayName, StringComparison.OrdinalIgnoreCase)).Select(p => p.Key).ToArray())
                        _players.Remove(match);
                }
                _players.Remove("partial::" + item.DisplayName);
                break;
        }
        _dirty = true;
    }

    private void SetLocation(string? location)
    {
        var parts = (location ?? "").Split(':', 2);
        _worldId = parts.ElementAtOrDefault(0);
        _instanceId = parts.ElementAtOrDefault(1);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _lifetime.Cancel();
        if (_worker is not null) await _worker;
        await _gate.WaitAsync();
        try { _disposed = true; }
        finally { _gate.Release(); }
        _lifetime.Dispose();
        // Do not dispose the gate while an externally requested refresh might be awaiting it.
    }
}

