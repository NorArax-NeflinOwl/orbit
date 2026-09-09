using Orbit.Contracts.Notifications;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// One notification, said the same way wherever it appears. The rule used to live in the bell's panel
/// and nowhere else, so the banner along the top of the screen printed the server's sentence raw - a
/// reminder that read properly in the panel slid past the top as 'The event "{0}" starts in {1} min.'
/// </summary>
public sealed class NotificationWordingTests
{
    private readonly Translations _translations = new(new StubJSRuntime());

    [Fact]
    public void What_fills_the_holes_is_put_into_them()
        => Assert.Equal(
            "The event \"Dentist\" starts in 30 min.",
            NotificationWording.Say(
                _translations, "The event \"{0}\" starts in {1} min.", ["Dentist", "30"]));

    /// <summary>
    /// An entry with no arguments is looked up whole: that is what a phrase like "New message" is, and
    /// what every entry written before the server split them apart looks like.
    /// </summary>
    [Fact]
    public void An_entry_with_nothing_to_fill_in_is_said_whole()
    {
        Assert.Equal("New message", NotificationWording.Say(_translations, "New message", arguments: null));
        Assert.Equal("New message", NotificationWording.Say(_translations, "New message", []));
    }

    /// <summary>Both halves of an entry follow the same rule - the banner shows both.</summary>
    [Fact]
    public void Both_halves_of_an_entry_are_said_the_same_way()
    {
        var entry = new NotificationEntryDto(
            Guid.NewGuid(), "EventReminder", "{0} needs you", "The event \"{0}\" starts in {1} min.",
            "/calendar/x", DateTimeOffset.UtcNow, IsRead: false,
            TitleArguments: ["Orbit"], BodyArguments: ["Dentist", "30"]);

        Assert.Equal("Orbit needs you", NotificationWording.Title(_translations, entry));
        Assert.Equal("The event \"Dentist\" starts in 30 min.", NotificationWording.Body(_translations, entry));
    }
}
