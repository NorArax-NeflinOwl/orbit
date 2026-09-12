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
public sealed record NameSuggestion(string Name, double Similarity)
{
    /// <summary>
    /// The things this name is the name of, which a task entry can be made the same thing as - picked,
    /// the entry takes on everything the thing says rather than only its words. Empty for a name that is
    /// only words here: a note's title, an event's, or any name asked for by a field other than a task
    /// entry's. See Orbit.Core.Tasks.TaskItem.ReferencesTaskItemId.
    /// </summary>
    public IReadOnlyList<NameSuggestionSource> Sources { get; init; } = [];
}

/// <summary>What a suggested name can be the name of, for an entry to be made the same thing as.</summary>
public enum NameSuggestionSourceKind
{
    /// <summary>An entry on one of the reader's lists - a reference to it joins its group.</summary>
    TaskItem,

    /// <summary>A product on one of the reader's shelves - an entry standing for it is the existing shelf link.</summary>
    InventoryItem
}

/// <summary>
/// One thing a suggested name is the name of - see <see cref="NameSuggestion.Sources"/>.
/// </summary>
/// <param name="Name">The name as that thing writes it.</param>
/// <param name="Id">
/// What an entry picking this would point at: the group's source entry for a task entry (so a reference
/// never points at another reference), the shelf item itself for a product.
/// </param>
/// <param name="ItemId">The entry or shelf item that was found by the name - where its details can be read.</param>
/// <param name="ContainerId">The list or inventory <paramref name="ItemId"/> is on.</param>
/// <param name="ContainerName">What that list or inventory is called, which tells two things of one name apart.</param>
/// <param name="EntryKind">The kind of entry it is, by name - "Inventory" for a shelf item.</param>
public sealed record NameSuggestionSource(
    string Name, NameSuggestionSourceKind Kind, Guid Id, Guid ItemId, Guid ContainerId, string ContainerName, string EntryKind);
