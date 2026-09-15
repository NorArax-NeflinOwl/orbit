using System.Globalization;
using Orbit.Contracts.Notes;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// A rule across the note on the phone's screen - see Orbit.Core.Notes.NoteSeparatorLine. What the
/// surface itself decides about one (where it lands, that a key takes it away, that nothing joins it) is
/// tested in NoteSurfaceSeparatorTests; here it is that the screen reaches those rules, draws what they
/// said, and writes the stamp exactly once.
/// </summary>
public sealed partial class NoteDetailScreenTests
{
    private static NoteContentLineDto ARuleLine(string stamp)
        => new(string.Empty, IsChecklistItem: false, IsChecked: false, Separator: new NoteSeparatorLineDto(stamp));

    [Fact]
    public async Task A_rule_is_drawn_with_what_is_written_on_it()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync(
            "Kitchen", [new("bought the paint", false, false), ARuleLine("Tuesday, 15 September 2026 11:20")]);

        var screen = await context.OpenAsync(note.LocalId);

        var line = screen.Lines[1];
        Assert.True(line.IsASeparator);
        Assert.True(line.IsAnElement);
        Assert.False(line.IsOpenForWriting);
        Assert.True(line.ShowsSeparatorStamp);
        Assert.Equal("Tuesday, 15 September 2026 11:20", line.SeparatorStamp);
    }

    /// <summary>A rule with nothing on it draws the rule and nothing else - it is still a rule.</summary>
    [Fact]
    public async Task A_plain_rule_draws_no_words()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync("Kitchen", [ARuleLine(string.Empty)]);

        var screen = await context.OpenAsync(note.LocalId);

        Assert.True(screen.Lines[0].IsASeparator);
        Assert.False(screen.Lines[0].ShowsSeparatorStamp);
    }

    /// <summary>
    /// The dated rule is stamped at the moment of the press, from the device's own clock, and in the
    /// reader's own format - see NoteSeparatorLine.Stamp, which says why that moment and no other.
    /// </summary>
    [Fact]
    public async Task The_dated_rule_is_stamped_when_it_is_made()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync("Kitchen", [new("bought the paint", false, false)]);
        var screen = await context.OpenAsync(note.LocalId);

        var madeAt = context.Clock.GetLocalNow().DateTime;
        screen.InsertSeparator(screen.Lines[0], isDated: true);

        var rule = screen.Lines[1];
        Assert.True(rule.IsASeparator);
        Assert.Equal(madeAt.ToString("f", CultureInfo.CurrentCulture), rule.SeparatorStamp);
    }

    /// <summary>
    /// And the rule keeps saying it: the clock moves on, the note is saved and read back, and the stamp
    /// is still the moment the rule was made. Worked out at draw time it would say now instead, which is
    /// the one thing putting a date in a note must not do.
    /// </summary>
    [Fact]
    public async Task A_rule_still_says_when_it_was_made_after_the_note_is_read_again()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync("Kitchen", [new("bought the paint", false, false)]);
        var screen = await context.OpenAsync(note.LocalId);

        screen.InsertSeparator(screen.Lines[0], isDated: true);
        var stamped = screen.Lines[1].SeparatorStamp;
        await screen.SaveLinesCommand.ExecuteAsync(null);

        context.Clock.Advance(TimeSpan.FromDays(3));
        var readAgain = await context.OpenAsync(note.LocalId);

        Assert.Equal(stamped, readAgain.Lines[1].SeparatorStamp);
    }

    /// <summary>The plain rule writes nothing on itself, which is the whole difference between the two.</summary>
    [Fact]
    public async Task The_plain_rule_is_stamped_with_nothing()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync("Kitchen", [new("bought the paint", false, false)]);
        var screen = await context.OpenAsync(note.LocalId);

        screen.InsertSeparator(screen.Lines[0], isDated: false);

        Assert.True(screen.Lines[1].IsASeparator);
        Assert.Equal(string.Empty, screen.Lines[1].SeparatorStamp);
    }

    /// <summary>Both rules are offered, in the reader's own language - see NoteDetailViewModel.SeparatorChoices.</summary>
    [Fact]
    public async Task Both_rules_are_offered()
    {
        using var context = new ScreenContext();
        var note = await context.Notes.CreateAsync("Kitchen", [new("bought the paint", false, false)]);
        var screen = await context.OpenAsync(note.LocalId);

        Assert.Equal(["Date and time", "Plain line"], screen.SeparatorChoices.Select(choice => choice.Name));
        Assert.True(screen.SeparatorChoices[0].IsDated);
        Assert.False(screen.SeparatorChoices[1].IsDated);
    }
}
