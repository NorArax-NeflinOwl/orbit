namespace Orbit.Core.Suggestions;

/// <summary>
/// Which of the four name fields a suggestion is for. Three read from a single place each, and mixing
/// them would offer an inventory's name where a product's was being typed - except
/// <see cref="TaskItemDescription"/>, which reads from all of them plus notes and events; see
/// NameSuggestionRepository.NamesFor for why that one field is different.
/// </summary>
public enum NameSuggestionKind
{
    /// <summary>A product on a shelf - the field this matters most in, since the same thing gets typed in twenty ways.</summary>
    InventoryItemName,

    InventoryName,

    TaskListTitle,

    /// <summary>
    /// The one field that reads across every other kind: a task entry is where a product, a note's
    /// title or an event's title most often gets typed again as an errand - "Milk" is "Milk" whichever
    /// of those it started as.
    /// </summary>
    TaskItemDescription
}

/// <summary>
/// One name the reader has already used, and how close it is to what they are typing. Similarity comes
/// back so the caller can tell "the same thing spelled differently" from "something else that shares a
/// few letters" - the first is a duplicate to propose merging, the second is only a completion to offer.
/// </summary>
public sealed record NameSuggestion(string Name, double Similarity);
