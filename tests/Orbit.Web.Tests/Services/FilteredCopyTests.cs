using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Tasks;
using Orbit.Localization;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Copying only part of a list - "copy only what is done, only what is not, only what failed", as the
/// user asked for it. The four choices are one shared answer (<see cref="WhatToCopy"/>) so that a note's
/// boxes and a task list's entries are narrowed by the same rule and the menu says the same four things
/// about both.
///
/// The format is the one a paste reads back, which is what the feature is for: what comes out of a list
/// filtered goes into a note as those same lines.
/// </summary>
public sealed class FilteredCopyTests
{
    private static IReadOnlyList<NoteContentLine> ANote() =>
    [
        NoteContentLine.PlainText("For Sunday"),
        new NoteContentLine("Milk", IsChecklistItem: true, IsChecked: true),
        new NoteContentLine("Bread", IsChecklistItem: true, IsChecked: false),
        new NoteContentLine("Cake", IsChecklistItem: true, IsChecked: false, IsFailed: true)
    ];

    private static IReadOnlyList<TickedLine> AList() =>
    [
        new("Book the doctor", TickState.Completed),
        new("Waterproof the boots", TickState.None),
        new("Shooting range", TickState.Failed)
    ];

    [Fact]
    public void The_whole_note_is_what_copying_always_did()
    {
        var words = NoteWords.Of("Shopping", ANote(), WhatToCopy.Everything);

        Assert.Equal("Shopping\nFor Sunday\n[x] Milk\n- Bread\n- Cake", words);
        Assert.Equal(NoteWords.Of("Shopping", ANote()), words);
    }

    /// <summary>
    /// The three narrow choices are questions about boxes, so the writing between them is left out: a
    /// page of prose answering "what is still to do" with every sentence it holds is not an answer.
    /// </summary>
    [Fact]
    public void Narrowing_a_note_keeps_its_boxes_and_its_name_and_nothing_else()
    {
        Assert.Equal("Shopping\n[x] Milk", NoteWords.Of("Shopping", ANote(), WhatToCopy.Done));
        Assert.Equal("Shopping\n- Bread", NoteWords.Of("Shopping", ANote(), WhatToCopy.StillToDo));
        Assert.Equal("Shopping\n- Cake", NoteWords.Of("Shopping", ANote(), WhatToCopy.GivenUpOn));
    }

    /// <summary>A crossed-out line is finished with, so "still to do" is not about it.</summary>
    [Fact]
    public void What_was_given_up_on_is_not_still_to_do()
    {
        Assert.DoesNotContain("Cake", NoteWords.Of("Shopping", ANote(), WhatToCopy.StillToDo));
    }

    [Fact]
    public void A_task_list_is_copied_in_the_same_format_a_note_is()
    {
        var words = TaskListWords.Of("Sieradz", AList());

        Assert.Equal("Sieradz\n[x] Book the doctor\n- Waterproof the boots\n- Shooting range", words);
    }

    [Fact]
    public void A_task_list_narrows_by_the_same_four_choices()
    {
        Assert.Equal("Sieradz\n[x] Book the doctor", TaskListWords.Of("Sieradz", AList(), WhatToCopy.Done));
        Assert.Equal("Sieradz\n- Waterproof the boots", TaskListWords.Of("Sieradz", AList(), WhatToCopy.StillToDo));
        Assert.Equal("Sieradz\n- Shooting range", TaskListWords.Of("Sieradz", AList(), WhatToCopy.GivenUpOn));
    }

    /// <summary>
    /// What the whole thing is for: the errands still to do, copied out of a list, arrive in a note as
    /// those same errands rather than as a page of brackets.
    /// </summary>
    [Fact]
    public void What_a_list_writes_a_note_reads_back_as_boxes()
    {
        var copied = TaskListWords.Of("Sieradz", AList(), WhatToCopy.StillToDo);

        var pasted = copied.Split('\n').Select(NoteSurfaceEdits.ReadPastedLine).ToList();

        Assert.Equal("Sieradz", pasted[0].Text);
        Assert.False(pasted[0].IsChecklistItem);
        Assert.Equal("Waterproof the boots", pasted[1].Text);
        Assert.True(pasted[1].IsChecklistItem);
        Assert.False(pasted[1].IsChecked);
    }

    /// <summary>A list with nothing in the state asked for copies its name and stops - never nothing at all.</summary>
    [Fact]
    public void Asking_for_a_state_nothing_is_in_still_says_which_list_it_was()
    {
        Assert.Equal("Errands", TaskListWords.Of("Errands", [], WhatToCopy.Done));
    }

    /// <summary>
    /// Each choice's label has a Polish translation. Its own test because the sweep that checks every
    /// other string cannot see these: it reads literals out of the source, and these arrive through
    /// `T[what.Label()]`, so a fifth choice added without a translation would show the reader English
    /// and fail nothing.
    /// </summary>
    [Fact]
    public void Every_choice_the_menu_offers_is_translated()
    {
        var untranslated = CopiedParts.All
            .Select(what => what.Label())
            .Where(label => !PolishTranslations.ByEnglish.ContainsKey(label))
            .ToList();

        Assert.True(untranslated.Count == 0, $"No Polish for: {string.Join(" | ", untranslated)}");
    }

    /// <summary>The four are offered whole-thing first, then narrowing - the order the menu draws them in.</summary>
    [Fact]
    public void The_menu_offers_the_whole_thing_first()
    {
        Assert.Equal(
            [WhatToCopy.Everything, WhatToCopy.Done, WhatToCopy.StillToDo, WhatToCopy.GivenUpOn],
            CopiedParts.All);
    }

    /// <summary>
    /// Pasting a list back in, as the user settled it on 2026-09-16: every line an entry, "[x] " coming in
    /// done, and the list's own name - which a copy writes first - left out. So a copy of a list pasted
    /// back into a list is the entries it was.
    /// </summary>
    [Fact]
    public void A_copied_list_pasted_into_a_list_comes_back_as_its_entries()
    {
        var words = TaskListWords.Of("Errands", AList());

        var entries = TaskListWords.ReadBack(words, "Errands");

        // The crossed-out one comes back open: the format carries two states, as the copy says.
        Assert.Equal(
            [("Book the doctor", true), ("Waterproof the boots", false), ("Shooting range", false)],
            entries);
    }

    /// <summary>
    /// Words from anywhere: plain lines are entries too, blank ones are nothing, and a first line naming
    /// some other list is kept - it may be the errand itself.
    /// </summary>
    [Fact]
    public void Plain_lines_from_elsewhere_become_entries_and_blank_ones_do_not()
    {
        var entries = TaskListWords.ReadBack("Shopping\r\n\r\n  eggs \r\n- flour\r\n[x] sugar\r\n", "Errands");

        Assert.Equal([("Shopping", false), ("eggs", false), ("flour", false), ("sugar", true)], entries);
    }
}
