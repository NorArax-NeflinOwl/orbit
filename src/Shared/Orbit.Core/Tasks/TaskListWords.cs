using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks;

/// <summary>One entry as the clipboard needs to know it: its words, and whether it is done.</summary>
public readonly record struct TickedLine(string Text, TickState State);

/// <summary>
/// A task list as plain words - what "copy the text" puts on the clipboard, and the counterpart of
/// <see cref="Orbit.Core.Notes.NoteWords"/> for the other kind of list. Shared rather than written once
/// per client for the reason that one is: the two would drift, and a list copied on a phone would paste
/// differently from the same list copied in a browser.
///
/// <b>It writes the same format a note does</b>, which is the whole point: the user asked for a
/// filtered copy so that what comes out of one thing can go into another, and a note's paste reads
/// "[x] " and "- " back as boxes (NoteSurfaceEdits.ReadPastedLine). So the errands still to do, copied
/// from a list, arrive in a note as the same errands rather than as a page of brackets.
///
/// It takes <see cref="TickedLine"/> rather than <see cref="TaskItem"/> because no server ever copies
/// anything: both callers hold their own row - the browser a DTO, the phone a screen row - and what
/// this needs of an entry is its words and its box. An entry is its description and nothing else; a
/// deadline, what it stands for and how much it needs are all real, and none of them survives being
/// pasted anywhere, so the format carries what a box and its words can carry and no more.
/// </summary>
public static class TaskListWords
{
    /// <summary>
    /// The list as it reads, name first - the same shape a note is copied in, so the name is a line
    /// like any other. <paramref name="what"/> narrows it to the entries in one state, and the name
    /// stays whichever is asked for: a handful of errands with nothing saying which list they came
    /// from is a handful nobody can place.
    /// </summary>
    public static string Of(string title, IEnumerable<TickedLine> entries, WhatToCopy what = WhatToCopy.Everything)
        => string.Join(
            "\n",
            new[] { title }.Concat(entries.Where(entry => what.Keeps(entry.State)).Select(AsALine)));

    /// <summary>
    /// One entry as the clipboard carries it. A crossed-out entry goes out as an unticked box, for the
    /// reason NoteWords gives: the format has three states' worth of meaning and a paste reads two.
    /// </summary>
    private static string AsALine(TickedLine entry)
        => entry.State.IsCompleted() ? $"[x] {entry.Text}" : $"- {entry.Text}";
}
