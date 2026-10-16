using System.Text.Json;
using ELIXR.PlayerList.Core.Models;
using ELIXR.PlayerList.Core.Services;
namespace ELIXR.PlayerList.Infrastructure.Services;

public sealed class JsonSettingsService : ISettingsService
{
    private readonly string _path;
    public AppSettings Current { get; private set; } = new();
    public JsonSettingsService(string? path = null)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ELIXR", "PlayerList");
        _path = path ?? Path.Combine(directory, "settings.json");
    }
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return;
        await using var stream = File.OpenRead(_path);
        Current = await JsonSerializer.DeserializeAsync<AppSettings>(stream, cancellationToken: cancellationToken) ?? new();
    }
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temp = _path + ".tmp";
        await using (var stream = File.Create(temp))
            await JsonSerializer.SerializeAsync(stream, Current, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
        File.Move(temp, _path, true);
    }
}
