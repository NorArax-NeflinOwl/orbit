namespace Orbit.Contracts.Suggestions;

/// <summary>
/// One name the reader has already used, offered while they type another - see
/// Orbit.Core.Suggestions.GetNameSuggestions.
/// </summary>
/// <param name="Similarity">
/// How close this is to what was typed, 0 to 1. Above roughly 0.6 the two are the same thing spelled
/// differently, which is a duplicate to propose merging rather than a completion to offer.
/// </param>
/// <param name="Sources">
/// The things this name is the name of, which a task entry can be made the same thing as - see
/// Orbit.Core.Suggestions.NameSuggestion.Sources. Null or empty for a name that is only words.
/// </param>
public sealed record NameSuggestionDto(
    string Name, double Similarity, IReadOnlyList<NameSuggestionSourceDto>? Sources = null)
{
    /// <summary>The sources as something to read without a null check.</summary>
    public IReadOnlyList<NameSuggestionSourceDto> AllSources => Sources ?? [];
}

/// <summary>
/// One thing a suggested name is the name of - see Orbit.Core.Suggestions.NameSuggestionSource, whose
/// fields these are.
/// </summary>
/// <param name="Kind">"TaskItem" or "InventoryItem".</param>
/// <param name="Id">What an entry picking this points at: the group's source entry, or the shelf item.</param>
/// <param name="ItemId">The entry or shelf item found by the name, where its details can be read.</param>
/// <param name="ContainerId">The list or inventory that one is on.</param>
/// <param name="ContainerName">What that list or inventory is called.</param>
/// <param name="EntryKind">The kind of entry it is, by name - "Inventory" for a shelf item.</param>
public sealed record NameSuggestionSourceDto(
    string Kind, Guid Id, Guid ItemId, Guid ContainerId, string ContainerName, string EntryKind);
