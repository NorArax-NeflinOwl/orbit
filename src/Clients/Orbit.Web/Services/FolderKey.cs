using Orbit.Core.Folders;

namespace Orbit.Web.Services;

/// <summary>
/// Which folder something is in, said in one value: either one of the three built-in folders or a
/// folder somebody made. Both are tabs on the same row, so both have to be the same kind of thing -
/// otherwise every page would carry two answers to "what am I looking at" and a rule about which wins.
///
/// A record struct so equality is free and cheap: comparing the chosen tab against each card's own
/// folder is the innermost thing every page made of cards now does.
/// </summary>
public readonly record struct FolderKey(BuiltInFolder? BuiltIn, Guid? FolderId)
{
    public static FolderKey Of(BuiltInFolder builtIn) => new(builtIn, null);

    public static FolderKey Of(Guid folderId) => new(null, folderId);

    /// <summary>Where a page opens: the ordinary folder, which is where anything unfiled already is.</summary>
    public static FolderKey Default => Of(BuiltInFolder.Public);

    public bool IsBuiltIn => BuiltIn is not null;
}
