using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
namespace ELIXR.PlayerList.Infrastructure.Services;

public interface ISessionStore
{
    Task<string?> ReadAsync(CancellationToken token = default);
    Task WriteAsync(string value, CancellationToken token = default);
    Task DeleteAsync(CancellationToken token = default);
}

[SupportedOSPlatform("windows")]
public sealed class WindowsSessionStore(string? path = null) : ISessionStore
{
    private readonly string _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ELIXR", "PlayerList", "session.bin");
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ELIXR.PlayerList.Session.v1");
    public async Task<string?> ReadAsync(CancellationToken token = default)
    {
        if (!File.Exists(_path)) return null;
        byte[]? plain = null;
        try
        {
            plain = ProtectedData.Unprotect(await File.ReadAllBytesAsync(_path, token), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException) { await DeleteAsync(token); return null; }
        finally { if (plain is not null) CryptographicOperations.ZeroMemory(plain); }
    }
    public async Task WriteAsync(string value, CancellationToken token = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var plain = Encoding.UTF8.GetBytes(value);
        try
        {
            var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            var temporary = _path + ".tmp";
            await File.WriteAllBytesAsync(temporary, encrypted, token);
            File.Move(temporary, _path, true);
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }
    public Task DeleteAsync(CancellationToken token = default)
    {
        File.Delete(_path);
        File.Delete(_path + ".tmp");
        return Task.CompletedTask;
    }
}

