using System.Text.RegularExpressions;
using Orbit.Mobile.Screens.Notes;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// That every command the note screen's markup asks the view model for actually exists on it.
///
/// The same check `AvatarMenuBindingTests` makes of the shell, and for the same reason: a binding to a
/// name nothing answers to fails silently in MAUI - the control simply does nothing, and nothing anywhere
/// says why. The note screen is worth its own copy because it binds more commands than any other and
/// several of them are reached only by a key or a gesture, where "did nothing" is indistinguishable from
/// the key or the gesture never having arrived.
///
/// Read out of the markup rather than listed here, so a binding added later is covered by having been
/// written.
/// </summary>
public sealed class NoteDetailBindingTests
{
    [Fact]
    public void Every_command_the_note_screen_asks_the_view_model_for_exists_on_it()
    {
        var missing = CommandsAskedOfTheViewModel()
            .Where(name => typeof(NoteDetailViewModel).GetProperty(name) is null)
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>
    /// Guards the test above: markup that could not be found would make it pass by checking nothing,
    /// which is the failure mode that matters for a sweep. Two names rather than one, because the two
    /// shapes of binding are read by two different expressions.
    /// </summary>
    [Fact]
    public void The_markup_was_actually_read()
    {
        Assert.Contains("ToggleCheckedCommand", CommandsAskedOfTheViewModel());
        Assert.Contains("UndoCommand", CommandsAskedOfTheViewModel());
    }

    /// <summary>
    /// Every command name the markup asks the view model for, in both shapes it asks in: the page's own
    /// binding context is the view model, so "{Binding XCommand}", and a row's is the line it draws, so
    /// the template reaches back out with "{Binding Source=…, Path=ViewModel.XCommand}".
    ///
    /// Commands the <em>page</em> carries (Path=XCommand with no ViewModel in front of it) are left out
    /// on purpose: they belong to Orbit.Maui, and this assembly cannot reference a MAUI type to check
    /// them against.
    /// </summary>
    private static IReadOnlyList<string> CommandsAskedOfTheViewModel()
    {
        var markup = Markup();
        return
        [
            .. Regex.Matches(markup, @"Path=ViewModel\.(\w+Command)\b")
                .Select(match => match.Groups[1].Value)
                .Concat(Regex.Matches(markup, @"\{Binding (\w+Command)\}").Select(match => match.Groups[1].Value))
                .Distinct()
        ];
    }

    /// <summary>
    /// The same check for the properties a line's own template binds - `{Binding Text}`, the drawing
    /// properties a style brought with it - which are the view model's rows rather than the view model.
    /// A line is drawn by a template with `x:DataType="screens:NoteLineRow"`, so a name nothing answers
    /// to is a control that quietly draws nothing.
    /// </summary>
    [Fact]
    public void Every_property_a_line_is_drawn_from_exists_on_the_row()
    {
        var missing = PropertiesAskedOfALine()
            .Where(name => typeof(NoteLineRow).GetProperty(name) is null)
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>Guards the sweep above the way The_markup_was_actually_read guards the first one.</summary>
    [Fact]
    public void The_line_template_was_actually_read()
    {
        Assert.Contains("IsOpenForWriting", PropertiesAskedOfALine());
        Assert.Contains("ListMark", PropertiesAskedOfALine());
    }

    /// <summary>
    /// Every plain `{Binding Name}` inside the template that draws one line. Commands are left out -
    /// the sweep above covers those, and a row's template reaches the view model's commands through a
    /// Source of its own rather than by name here.
    /// </summary>
    private static IReadOnlyList<string> PropertiesAskedOfALine()
    {
        var markup = Markup();
        var start = markup.IndexOf("x:DataType=\"screens:NoteLineRow\"", StringComparison.Ordinal);
        Assert.True(start > 0, "The line's own template was not found in the note screen's markup.");

        var end = markup.IndexOf("</DataTemplate>", start, StringComparison.Ordinal);
        Assert.True(end > start, "The line's own template is not closed.");

        return
        [
            .. Regex.Matches(markup[start..end], @"\{Binding (\w+)\}")
                .Select(match => match.Groups[1].Value)
                .Where(name => !name.EndsWith("Command", StringComparison.Ordinal))
                .Distinct()
        ];
    }

    /// <summary>
    /// Found by walking up from the test binary rather than by a path relative to the working directory,
    /// which differs between a run from the IDE and one from the command line - see AvatarMenuBindingTests,
    /// which finds the shell's markup the same way.
    /// </summary>
    private static string Markup()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(
            directory!.FullName, "src", "Clients", "Orbit.Maui", "Features", "Notes", "NoteDetailPage.xaml"));
    }
}
