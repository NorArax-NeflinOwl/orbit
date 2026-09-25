using Orbit.Core.Folders;
using Orbit.Mobile.Screens.Folders;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// What the bar calls a list screen. The browser keeps its folders as a row of tabs above the cards, so
/// what is chosen is on the screen whether anybody looks for it or not; the phone keeps them in the menu
/// under the screen's name, which is shut. A screen narrowed to "Work" therefore looked exactly like one
/// showing everything. Asked for on 2026-09-24.
/// </summary>
public sealed class ScreenTitleWithFolderTests
{
    [Fact]
    public void The_folder_being_read_is_named_beside_the_screen()
    {
        Assert.Equal(
            "Notes · Work",
            ScreenTitleWithFolder.Of("Notes", FolderKey.Of(Guid.NewGuid()), "Work"));
    }

    /// <summary>
    /// Public is left unsaid, as the request asks: it is where a page opens and where anything unfiled
    /// already is, so naming it would put a word on every screen to say "nothing in particular".
    /// </summary>
    [Fact]
    public void And_the_ordinary_folder_is_left_unsaid()
    {
        Assert.Equal("Notes", ScreenTitleWithFolder.Of("Notes", FolderKey.Default, "Public"));
    }

    /// <summary>The other built-in ones are said: none of them is where the screen opens.</summary>
    [Theory]
    [InlineData(BuiltInFolder.Private, "Private")]
    [InlineData(BuiltInFolder.Archived, "Archived")]
    [InlineData(BuiltInFolder.Finished, "Finished")]
    public void And_the_other_built_in_ones_are(BuiltInFolder builtIn, string name)
    {
        Assert.Equal($"Notes · {name}", ScreenTitleWithFolder.Of("Notes", FolderKey.Of(builtIn), name));
    }

    /// <summary>
    /// A screen whose folders have not been read yet has no name to add, and half a title with a
    /// separator hanging off it is worse than the name on its own.
    /// </summary>
    [Fact]
    public void And_a_folder_with_no_name_yet_adds_nothing()
    {
        Assert.Equal("Notes", ScreenTitleWithFolder.Of("Notes", FolderKey.Of(Guid.NewGuid()), "   "));
    }
}
