using ELIXR.PlayerList.Core.Models;
using ELIXR.PlayerList.Infrastructure.Services;
namespace ELIXR.PlayerList.Core.Tests;

public sealed class DesktopOverlayBoundsTests
{
    [Fact]
    public void RestoredLayoutIsClampedToAvailableDesktop()
    {
        var screen = new DesktopOverlayBounds(0,0,1920,1080);
        Assert.Equal(new DesktopOverlayBounds(1680,0,240,1080),DesktopOverlayBounds.Clamp(new(3000,-300,1,5000),screen));
    }
    [Fact]
    public void NegativeMonitorCoordinatesAreRetained()
    {
        var saved = new DesktopOverlayBounds(-1600,60,440,660);
        Assert.Equal(saved,DesktopOverlayBounds.Clamp(saved,new(-1920,0,3840,1080)));
    }
    [Fact]
    public void InvalidGeometryCannotBreakWindowStartup()
    {
        Assert.Equal(new DesktopOverlayBounds(0,0,440,660),DesktopOverlayBounds.Clamp(new(double.NaN,double.PositiveInfinity,double.NaN,double.NegativeInfinity),new(0,0,1920,1080)));
        Assert.Equal(new DesktopOverlayBounds(0,0,200,180),DesktopOverlayBounds.Clamp(new(500,500,440,660),new(0,0,200,180)));
    }
    [Fact]
    public async Task PlacementRoundTripsThroughExistingAtomicSettingsStore()
    {
        var path = Path.Combine(Path.GetTempPath(),"elixr-placement-"+Guid.NewGuid()+".json");
        try
        {
            var settings = new JsonSettingsService(path); settings.Current.DesktopOverlayPlacement = new(-1200,80,420,560);
            settings.Current.CompactTags = true; await settings.SaveAsync();
            var reopened = new JsonSettingsService(path); await reopened.LoadAsync();
            Assert.Equal(settings.Current.DesktopOverlayPlacement,reopened.Current.DesktopOverlayPlacement);Assert.True(reopened.Current.CompactTags);
        }
        finally { if(File.Exists(path))File.Delete(path); }
    }
    [Fact]
    public async Task OlderSettingsRemainCompatibleWithoutPlacement()
    {
        var path = Path.Combine(Path.GetTempPath(),"elixr-legacy-placement-"+Guid.NewGuid()+".json");
        try
        {
            await File.WriteAllTextAsync(path,"{\"CompactTags\":true}");
            var settings = new JsonSettingsService(path);await settings.LoadAsync();
            Assert.True(settings.Current.CompactTags);Assert.Null(settings.Current.DesktopOverlayPlacement);
        }
        finally { if(File.Exists(path))File.Delete(path); }
    }
}
