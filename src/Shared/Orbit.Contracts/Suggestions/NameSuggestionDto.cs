namespace Orbit.Contracts.Suggestions;

/// <summary>
/// One name the reader has already used, offered while they type another - see
/// Orbit.Core.Suggestions.GetNameSuggestions.
/// </summary>
/// <param name="Similarity">
/// How close this is to what was typed, 0 to 1. Above roughly 0.6 the two are the same thing spelled
/// differently, which is a duplicate to propose merging rather than a completion to offer.
/// </param>
public sealed record NameSuggestionDto(string Name, double Similarity);
