using ELIXR.PlayerList.Core.Models;
namespace ELIXR.PlayerList.Core.Services;

public static class TagDisplayService
{
    public static IReadOnlyList<PlayerTag> VisibleTags(IEnumerable<PlayerTag> tags, AppSettings settings) =>
        tags.Where(tag => settings.IsVisible(tag.Category)).ToArray();
    public static IReadOnlyList<TagCategory> CompactCategories(IEnumerable<PlayerTag> tags, AppSettings settings) =>
        tags.Where(tag => settings.IsVisible(tag.Category)).Select(tag => tag.Category).Distinct().OrderBy(category => category).ToArray();
}
