using Orbit.Core.Abstractions;

namespace Orbit.Core.Suggestions.GetNameSuggestions;

/// <summary>
/// What to offer somebody typing a name they have probably typed before.
///
/// Deliberately not a language model's job, and this is the place that decision is written down. This
/// is a similarity search over a list the reader already owns: the database answers it in single-digit
/// milliseconds for nothing, and it answers it *better* - a model does not know what is in this
/// warehouse, so it invents plausible names instead of offering real ones. The assistant is for
/// language, intent and explanation; this is for "you already have one of these".
/// </summary>
public sealed record GetNameSuggestionsQuery(Guid UserId, NameSuggestionKind Kind, string Typed)
    : IRequest<IReadOnlyList<NameSuggestion>>;
