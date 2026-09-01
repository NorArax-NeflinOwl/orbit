using Xunit;
using Orbit.Mobile.Screens;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The rule both clients draw people by - see Avatar, and Orbit.Web's AvatarHelper for the browser's
/// half. These are here because the same person turning up as "A" in one list and "AL" in another is
/// the kind of difference nobody reports and everybody notices.
/// </summary>
public class AvatarTests
{
    [Theory]
    [InlineData("Ada Lovelace", "AL")]
    [InlineData("Ada", "AD")]
    [InlineData("A", "A")]
    [InlineData("Ada Mary Lovelace", "AM")]
    [InlineData("  Ada   Lovelace  ", "AL")]
    public void TakesUpToTwoInitials(string displayName, string expected)
        => Assert.Equal(expected, Avatar.InitialsOf(displayName));

    /// <summary>An avatar reading "?" looks like a fault rather than an unnamed account.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void SaysNothingForSomebodyWithNoName(string? displayName)
        => Assert.Equal(string.Empty, Avatar.InitialsOf(displayName));

    [Fact]
    public void GivesTheSamePersonTheSameColourEveryTime()
    {
        var id = Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff");

        Assert.Equal(Avatar.Of(id, "Ada Lovelace").Hue, Avatar.Of(id, "Somebody Else").Hue);
    }

    [Fact]
    public void PicksAHueThatCanBeDrawn()
    {
        var hues = Enumerable.Range(0, 500).Select(_ => Avatar.Of(Guid.NewGuid(), "Ada").Hue).ToList();

        Assert.All(hues, hue => Assert.InRange(hue, 0, 359));
    }

    /// <summary>Two people the reader is looking at together should not have to share a colour.</summary>
    [Fact]
    public void TellsPeopleApartByColour()
    {
        var hues = Enumerable.Range(0, 50).Select(_ => Avatar.Of(Guid.NewGuid(), "Ada").Hue).ToHashSet();

        Assert.True(hues.Count > 25, $"Only {hues.Count} colours across 50 people.");
    }
}
