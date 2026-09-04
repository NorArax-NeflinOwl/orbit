namespace Orbit.Core.Suggestions;

/// <summary>
/// Which field's own vocabulary is being asked for. Different question from
/// <see cref="NameSuggestionKind"/>, and the difference is the whole reason this exists: a name
/// suggestion is a similarity search over what somebody is typing, while this is the complete list of
/// what the reader has filed things under - shown before a character is typed, because the point of a
/// category is to reuse one rather than invent a twenty-first.
/// </summary>
public enum UsedValueKind
{
    /// <summary>What task entries are filed under - see Orbit.Core.Tasks.TaskItem.Categories.</summary>
    TaskItemCategory,

    /// <summary>What shelf items are filed under.</summary>
    InventoryItemCategory,

    /// <summary>What kind of thing a shelf item is - one answer per item, unlike the categories beside it.</summary>
    InventoryItemProductType
}
