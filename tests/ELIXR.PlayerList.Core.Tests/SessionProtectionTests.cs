using ELIXR.PlayerList.Infrastructure.Services;
namespace ELIXR.PlayerList.Core.Tests;
public sealed class SessionProtectionTests
{
    [Fact]
    public async Task WindowsProtection_EncryptsAtRest_RoundTrips_AndDeletesCorruption()
    {
        if (!OperatingSystem.IsWindows()) return;
        var folder = Path.Combine(Path.GetTempPath(), "elixr-session-test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(folder, "session.bin");
        try
        {
            var store = new WindowsSessionStore(path);
            const string secret = "test-only-session-value";
            await store.WriteAsync(secret);
            Assert.DoesNotContain(secret,System.Text.Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path)));
            Assert.Equal(secret, await store.ReadAsync());
            await File.WriteAllBytesAsync(path, [1,2,3,4]);
            Assert.Null(await store.ReadAsync());
            Assert.False(File.Exists(path));
        }
        finally { if(File.Exists(path)) File.Delete(path); if(Directory.Exists(folder)) Directory.Delete(folder); }
    }
}

