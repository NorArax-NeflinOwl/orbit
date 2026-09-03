namespace Orbit.Core.Suggestions;

/// <summary>
/// Which of the four name fields a suggestion is for. Each reads from a different place, and mixing
/// them would offer an inventory's name where a product's was being typed.
/// </summary>
public enum NameSuggestionKind
{
    /// <summary>A product on a shelf - the field this matters most in, since the same thing gets typed in twenty ways.</summary>
    InventoryItemName,

    InventoryName,

    TaskListTitle,

    TaskItemDescription
}

/// <summary>
/// One name the reader has already used, and how close it is to what they are typing. Similarity comes
/// back so the caller can tell "the same thing spelled differently" from "something else that shares a
/// few letters" - the first is a duplicate to propose merging, the second is only a completion to offer.
/// </summary>
public sealed record NameSuggestion(string Name, double Similarity);
