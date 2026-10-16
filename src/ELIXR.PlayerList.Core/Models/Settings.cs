namespace ELIXR.PlayerList.Core.Models;

public sealed class AppSettings
{
    public bool ShowMaliciousTags { get; set; } = true;
    public bool ShowCustomTags { get; set; } = true;
    public bool ShowGroupTags { get; set; } = true;
    public bool CompactTags { get; set; }
    public bool OverlayEnabled { get; set; } = true;
    public bool OverlayVisible { get; set; } = true;
    public double UiScale { get; set; } = 1;
    public double OverlayOpacity { get; set; } = .94;
    public bool CheckUpdatesOnStartup { get; set; } = true;
    public DesktopOverlayBounds? DesktopOverlayPlacement { get; set; }

    public bool IsVisible(TagCategory category) => category switch
    {
        TagCategory.Malicious => ShowMaliciousTags,
        TagCategory.Custom => ShowCustomTags,
        TagCategory.Group => ShowGroupTags,
        _ => false
    };
}
