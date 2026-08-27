using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Navigation;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Where the phone's back gesture leads. Only Android asks, and it asks from every screen, so the two
/// things this can get wrong are both everywhere at once: sending the reader somewhere that is not
/// above them, or leaving the app when there was somewhere to go.
/// </summary>
public sealed class UpNavigationTests
{
    [Theory]
    [InlineData(Screen.Conversation, nameof(IScreenNavigator.ShowContacts))]
    [InlineData(Screen.GroupConversation, nameof(IScreenNavigator.ShowGroups))]
    [InlineData(Screen.GroupDetail, nameof(IScreenNavigator.ShowGroups))]
    [InlineData(Screen.Warehouse, nameof(IScreenNavigator.ShowInventory))]
    [InlineData(Screen.TaskList, nameof(IScreenNavigator.ShowTasks))]
    [InlineData(Screen.NotificationSettings, nameof(IScreenNavigator.ShowNotifications))]
    [InlineData(Screen.Diagnostics, nameof(IScreenNavigator.ShowAccount))]
    public void A_screen_opened_from_a_list_goes_back_to_that_list(Screen from, string expected)
    {
        var navigator = new RecordingScreenNavigator();
        var upNavigation = new UpNavigation(navigator);
        upNavigation.Showing(from);

        Assert.True(upNavigation.GoUp());
        Assert.Equal(expected, navigator.LastDestination);
    }

    [Theory]
    [InlineData(Screen.Notes)]
    [InlineData(Screen.Tasks)]
    [InlineData(Screen.Calendar)]
    [InlineData(Screen.Inventory)]
    [InlineData(Screen.Contacts)]
    [InlineData(Screen.Map)]
    [InlineData(Screen.Notifications)]
    [InlineData(Screen.Account)]
    [InlineData(Screen.ChatKeyGate)]
    public void A_section_goes_back_to_the_dashboard(Screen from)
    {
        var navigator = new RecordingScreenNavigator();
        var upNavigation = new UpNavigation(navigator);
        upNavigation.Showing(from);

        Assert.True(upNavigation.GoUp());
        Assert.Equal(nameof(IScreenNavigator.ShowDashboard), navigator.LastDestination);
    }

    [Fact]
    public void Registering_goes_back_to_signing_in()
    {
        var navigator = new RecordingScreenNavigator();
        var upNavigation = new UpNavigation(navigator);
        upNavigation.Showing(Screen.Register);

        Assert.True(upNavigation.GoUp());
        Assert.Equal(nameof(IScreenNavigator.ShowSignIn), navigator.LastDestination);
    }

    /// <summary>
    /// The three screens the app is left from. The startup screen matters most: a build the server has
    /// retired stops there, and going anywhere from it would be a way past the block.
    /// </summary>
    [Theory]
    [InlineData(Screen.Dashboard)]
    [InlineData(Screen.SignIn)]
    [InlineData(Screen.Startup)]
    public void There_is_nowhere_above_the_screens_the_app_is_left_from(Screen from)
    {
        var navigator = new RecordingScreenNavigator();
        var upNavigation = new UpNavigation(navigator);
        upNavigation.Showing(from);

        Assert.False(upNavigation.GoUp());
        Assert.Empty(navigator.Destinations);
    }

    /// <summary>
    /// The app opens on the startup screen without the navigator putting it there, so anything that
    /// asked before the first navigation would be measuring from a screen nobody set.
    /// </summary>
    [Fact]
    public void Before_anything_has_navigated_there_is_nowhere_to_go()
    {
        var navigator = new RecordingScreenNavigator();

        Assert.False(new UpNavigation(navigator).GoUp());
        Assert.Empty(navigator.Destinations);
    }

    /// <summary>
    /// Every parent has to be a screen that can be shown without an argument. A hierarchy naming one
    /// that cannot - a conversation, say, which needs to know whose - would throw at the moment the
    /// reader swiped, which is the worst place to find out.
    /// </summary>
    [Fact]
    public void Every_screen_can_be_left_without_throwing()
    {
        foreach (var screen in Enum.GetValues<Screen>())
        {
            var upNavigation = new UpNavigation(new RecordingScreenNavigator());
            upNavigation.Showing(screen);

            upNavigation.GoUp();
        }
    }
}
