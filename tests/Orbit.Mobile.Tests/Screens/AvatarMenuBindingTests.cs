using System.Text.RegularExpressions;
using Orbit.Mobile.Screens.Navigation;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// That every command the app's shell binds to actually exists - the bar, the drawer under its three
/// lines, and the menu behind the avatar.
///
/// This is not pedantry: a binding to a name nothing answers to fails silently in MAUI. The About row
/// asked for "ToggleAboutAsyncCommand" while the toolkit generates "ToggleAboutCommand" - it strips the
/// Async suffix - so tapping About did nothing at all, and nothing anywhere said why.
///
/// Read out of the markup rather than asserted one by one, so a row added later is covered by having
/// been written.
/// </summary>
public sealed class AvatarMenuBindingTests
{
    [Fact]
    public void Every_command_the_avatar_menu_binds_to_exists_on_the_bar()
    {
        var markup = string.Concat(new[] { "NavigationBar.xaml", "AvatarMenu.xaml", "Drawer.xaml" }.Select(Read));
        var commands = Regex.Matches(markup, @"Binding (\w+Command)\}")
            .Select(match => match.Groups[1].Value)
            .Distinct();

        var missing = commands
            .Where(name => typeof(NavigationBarViewModel).GetProperty(name) is null)
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>
    /// All three bind to the same shared view model, and a binding that stops matching is exactly as
    /// silent in any of them. The drawer joined the list when the sections moved into it, taking the
    /// notification count and the About row with them.
    /// </summary>
    private static string Read(string file) => File.ReadAllText(Path.Combine(MarkupDirectory(), file));

    /// <summary>
    /// Where the three files live. Found by walking up from the test binary rather than by a path relative to
    /// the working directory, which differs between a run from the IDE and one from the command line.
    /// </summary>
    private static string MarkupDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "src", "Clients", "Orbit.Maui", "Controls");
    }
}
