namespace ELIXR.PlayerList.Core.Models;

public static class DesktopStartupFailure
{
    public static string Describe(int errorCode, string stage)
    {
        if (stage == "bundled-frontend")
            return "ELIXR's installed interface files are incomplete. Run the latest ELIXR Player List installer again to repair them. You do not need to build or install development packages.";
        if (errorCode == unchecked((int)0x80010108))
            return "WebView2 disconnected while ELIXR was starting. This does not necessarily mean a package is missing. Close ELIXR and open it again. If it repeats, restart Windows and run the latest ELIXR Player List installer, which includes the required runtime.";
        if (stage == "runtime-discovery")
            return "The WebView2 runtime is missing or unavailable. Run the latest ELIXR Player List installer: it includes the full runtime, .NET, and the application files. No separate package download is required.";
        return "ELIXR could not start its desktop interface. Close ELIXR and try again. If it repeats, restart Windows and run the latest ELIXR Player List installer to repair the application and its bundled prerequisite.";
    }
}
