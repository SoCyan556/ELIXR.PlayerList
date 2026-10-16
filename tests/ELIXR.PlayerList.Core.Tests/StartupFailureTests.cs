using ELIXR.PlayerList.Core.Models;
namespace ELIXR.PlayerList.Core.Tests;

public sealed class StartupFailureTests
{
    [Fact] public void DisconnectedRuntimeIsNotReportedAsAMissingPackage()
    {
        var message = DesktopStartupFailure.Describe(unchecked((int)0x80010108), "browser-control");
        Assert.Contains("disconnected", message); Assert.Contains("does not necessarily", message);
        Assert.DoesNotContain("rebuild", message);
    }
    [Fact] public void MissingRuntimePointsToBundledInstaller() => Assert.Contains("No separate package", DesktopStartupFailure.Describe(1, "runtime-discovery"));
    [Fact] public void MissingApplicationFilesDoNotTellUsersToBuildTheFrontend() => Assert.Contains("do not need to build", DesktopStartupFailure.Describe(1, "bundled-frontend"));
}
