using System.Text.RegularExpressions;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// That every control on the phone has something to say to a screen reader.
///
/// A button says its own text, and a box says its placeholder. A switch, a picker, a date or time
/// picker and a checkbox say nothing at all: their meaning is in a label beside them, which a screen
/// reader reads as a separate thing entirely - so landing on the control announced "switch, off" and
/// left the reader to guess which of the six switches on the screen they had reached.
///
/// Forty of them were in that state. Checked against the markup because that is where the omission
/// lives, and because the alternative is finding out from somebody who cannot use the app.
/// </summary>
public sealed class SpokenNameTests
{
    /// <summary>What a platform reads out for itself, given text or a placeholder to read.</summary>
    private const string SpeaksForItself = @"\b(Text|Placeholder)=""[^""]";

    private const string Controls =
        "Button|ImageButton|SearchBar|Switch|Picker|Entry|Editor|DatePicker|TimePicker|Slider|Stepper|CheckBox";

    [Fact]
    public void Every_control_has_a_name_a_screen_reader_can_read()
    {
        var silent = new List<string>();

        foreach (var page in Markup())
        {
            var text = File.ReadAllText(page);
            // A description set in code-behind counts: PinButton names itself after what it will do,
            // which changes with its state and so cannot be written in the markup.
            var namedInCode = File.Exists(Path.ChangeExtension(page, ".xaml.cs"))
                && File.ReadAllText(Path.ChangeExtension(page, ".xaml.cs")).Contains("SemanticProperties.SetDescription");

            // "<Tag " or "<Tag/>", never "<Tag.Something>" - that is a property element, not a control.
            foreach (Match match in Regex.Matches(text, $@"<({Controls})(?![\w.])(.*?)(/?>)", RegexOptions.Singleline))
            {
                var body = match.Groups[2].Value;
                if (namedInCode
                    || body.Contains("SemanticProperties.Description")
                    || Regex.IsMatch(body, SpeaksForItself))
                {
                    continue;
                }

                var line = text[..match.Index].Count(character => character == '\n') + 1;
                silent.Add($"{Path.GetFileName(page)}:{line} <{match.Groups[1].Value}>");
            }
        }

        Assert.Empty(silent);
    }

    /// <summary>
    /// Every page and control of the app head, found by walking up from the test binary rather than by
    /// a path relative to the working directory - which differs between a run from the IDE and one from
    /// the command line.
    /// </summary>
    private static IEnumerable<string> Markup()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Directory.EnumerateFiles(
            Path.Combine(directory!.FullName, "src", "Clients", "Orbit.Maui"), "*.xaml", SearchOption.AllDirectories)
            .Where(page => !page.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !page.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
    }
}
