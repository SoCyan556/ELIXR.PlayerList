using ELIXR.PlayerList.Core.Models;
using ELIXR.PlayerList.Core.Services;
namespace ELIXR.PlayerList.Core.Tests;

public sealed class TagDisplayServiceTests
{
    [Fact]
    public void CompactCategories_DeduplicatesEachVisibleCategory()
    {
        var tags = new[] { new PlayerTag("Risk A", TagCategory.Malicious), new PlayerTag("Risk B", TagCategory.Malicious), new PlayerTag("Fun", TagCategory.Custom), new PlayerTag("Group", TagCategory.Group) };
        var result = TagDisplayService.CompactCategories(tags, new AppSettings());
        Assert.Equal(new[] { TagCategory.Malicious, TagCategory.Custom, TagCategory.Group }, result);
    }

    [Fact]
    public void VisibleTags_RespectsIndependentCategoryToggles()
    {
        var settings = new AppSettings { ShowCustomTags = false };
        var tags = new[] { new PlayerTag("Risk", TagCategory.Malicious), new PlayerTag("Fun", TagCategory.Custom) };
        Assert.Single(TagDisplayService.VisibleTags(tags, settings));
    }
}
