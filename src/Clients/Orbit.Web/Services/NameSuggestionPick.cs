using Orbit.Contracts.Suggestions;

namespace Orbit.Web.Services;

/// <summary>
/// A suggested name picked for what it is the name of, rather than for its words - see
/// NameSuggestions.OnSourceChosen and Orbit.Core.Tasks.TaskItem.ReferencesTaskItemId.
/// </summary>
public sealed record NameSuggestionPick(string Name, NameSuggestionSourceDto Source);
