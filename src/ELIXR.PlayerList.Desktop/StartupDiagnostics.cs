using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ELIXR.PlayerList.Desktop;

internal static class StartupDiagnostics
{
    internal static void SmokeCheckpoint(string stage)
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, "--smoke-test");
        if (index < 0 || index + 1 >= args.Length) return;
        try { File.WriteAllText(args[index + 1] + ".startup.json", JsonSerializer.Serialize(new { stage, time = DateTimeOffset.UtcNow })); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
    internal static string Write(Exception error, string stage, string? runtimeVersion)
    {
        var code = $"0x{error.HResult:X8}";
        try
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ELIXR", "PlayerList");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "startup-error.json");
            // One bounded diagnostic is overwritten on failure. No exception text,
            // profile data, credentials, cookies, browser arguments or environment dump.
            File.WriteAllText(path, JsonSerializer.Serialize(new {
                time = DateTimeOffset.UtcNow, stage, code, errorType = error.GetType().Name,
                appVersion = typeof(App).Assembly.GetName().Version?.ToString(),
                runtimeVersion, architecture = RuntimeInformation.ProcessArchitecture.ToString()
            }, new JsonSerializerOptions { WriteIndented = true }));
            return $"Error {code}\nStartup details: {path}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return $"Error {code} (startup diagnostics could not be saved)."; }
    }
}
