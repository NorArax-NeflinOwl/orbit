namespace Orbit.Core.Folders;

/// <summary>
/// Which folder something is in, said in one value: either one of the three built-in folders or a
/// folder somebody made. Both are tabs on the same row - entries in the same menu, on the phone - so
/// both have to be the same kind of thing; otherwise every page would carry two answers to "what am I
/// looking at" and a rule about which wins.
///
/// A record struct so equality is free and cheap: comparing the chosen tab against each card's own
/// folder is the innermost thing every page made of cards now does. Equality is not the same question
/// as <see cref="Holds"/>, though - see it for why one tab is wider than the folder of the same name.
/// </summary>
public readonly record struct FolderKey(BuiltInFolder? BuiltIn, Guid? FolderId)
{
    public static FolderKey Of(BuiltInFolder builtIn) => new(builtIn, null);

    public static FolderKey Of(Guid folderId) => new(null, folderId);

    /// <summary>Where a page opens: <b>All</b>, which is now everything except what is sealed or put away.</summary>
    public static FolderKey Default => Of(BuiltInFolder.All);

    public bool IsBuiltIn => BuiltIn is not null;

    /// <summary>
    /// Whether a card placed in <paramref name="placement"/> belongs under this tab - the question every
    /// page made of cards asks of every card it holds, where it used to compare the two for equality.
    ///
    /// They differ for one tab, and that is the point of it (the user's decision of 2026-09-24, "Public
    /// becomes All"). <b>All</b> holds everything from every folder except what is sealed and what has
    /// been put away, so a note filed under "Work" is under "Work" <em>and</em> under All - which is
    /// what a reader means by wanting to see the lot at once, and what the old Public tab could not do:
    /// filing something anywhere took it off the tab the page opens on.
    ///
    /// Every other tab still means exactly itself. Private and Archived are what All is defined against,
    /// and a folder somebody made is a place they put something rather than a way of gathering it.
    /// Finished is inside All for the same reason "Work" is: a finished list has not been sealed and has
    /// not been put away, so it is still one of the things the account holds.
    /// </summary>
    public bool Holds(FolderKey placement)
        => BuiltIn == BuiltInFolder.All
            ? placement.BuiltIn is not (BuiltInFolder.Private or BuiltInFolder.Archived)
            : this == placement;
}
