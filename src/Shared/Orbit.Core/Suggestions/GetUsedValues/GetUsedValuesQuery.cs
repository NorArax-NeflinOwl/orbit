using Orbit.Core.Abstractions;

namespace Orbit.Core.Suggestions.GetUsedValues;

public sealed record GetUsedValuesQuery(Guid UserId, UsedValueKind Kind) : IRequest<IReadOnlyList<string>>;
