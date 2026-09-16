namespace Orbit.Core.Abstractions;

/// <summary>
/// Which part of a list is put on the clipboard - the four choices the copy menu offers on a note and
/// on a task list alike (asked for by the user: "copy only what is done, only what is not, only what
/// failed"). Shared rather than written once per kind, so the menu says the same four things about a
/// note's boxes and a list's entries and each means the same thing.
///
/// The three narrow choices are about <see cref="TickState"/>, so they keep boxes and entries and
/// nothing else: a note's ordinary writing is not in one of these states and is left out of a filtered
/// copy, where <see cref="Everything"/> keeps the note whole.
/// </summary>
public enum WhatToCopy
{
    /// <summary>The whole thing, as it reads - what copying did before there was a choice.</summary>
    Everything,

    /// <summary>Only what is ticked off.</summary>
    Done,

    /// <summary>Only what nobody has answered yet - not the crossed-out, which are finished with.</summary>
    StillToDo,

    /// <summary>Only what was given up on - see Orbit.Core.Tasks.TaskItem.IsFailed.</summary>
    GivenUpOn
}

public static class CopiedParts
{
    /// <summary>
    /// Whether something in <paramref name="state"/> belongs in a copy of <paramref name="what"/>.
    /// <see cref="WhatToCopy.Everything"/> answers true for all three, which is what makes it the whole
    /// thing rather than a filter that happens to match everything.
    /// </summary>
    public static bool Keeps(this WhatToCopy what, TickState state)
        => what switch
        {
            WhatToCopy.Done => state.IsCompleted(),
            WhatToCopy.StillToDo => !state.IsResolved(),
            WhatToCopy.GivenUpOn => state.IsFailed(),
            _ => true
        };

    /// <summary>
    /// Whether a line with no tick box at all - a note's ordinary writing, a heading - belongs in the
    /// copy. Only in the whole thing: the other three are questions about boxes, and a page of prose
    /// answering "what is still to do" with every sentence it contains is not an answer.
    /// </summary>
    public static bool KeepsWhatHasNoBox(this WhatToCopy what) => what == WhatToCopy.Everything;

    /// <summary>The four, in the order a menu offers them - the whole thing first, then narrowing.</summary>
    public static IReadOnlyList<WhatToCopy> All =>
        [WhatToCopy.Everything, WhatToCopy.Done, WhatToCopy.StillToDo, WhatToCopy.GivenUpOn];

    /// <summary>
    /// What the menu entry for each says, as a key for the reader's own language - see
    /// Orbit.Localization. Here rather than in either client, so the browser's menu and the phone's
    /// sheet offer the same words.
    /// </summary>
    public static string Label(this WhatToCopy what)
        => what switch
        {
            WhatToCopy.Done => "Copy what is done",
            WhatToCopy.StillToDo => "Copy what is still to do",
            WhatToCopy.GivenUpOn => "Copy what was given up on",
            _ => "Copy the text"
        };
}
