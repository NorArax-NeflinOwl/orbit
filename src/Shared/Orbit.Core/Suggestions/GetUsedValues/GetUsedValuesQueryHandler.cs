using Orbit.Core.Abstractions;

namespace Orbit.Core.Suggestions.GetUsedValues;

/// <summary>
/// The reader's own vocabulary for one field. Asked once when an editor opens rather than per keystroke:
/// the answer is a handful of short words that only change when somebody files something under a new
/// one, and a list that is shown before anything is typed cannot be looked up as it is typed.
/// </summary>
public sealed class GetUsedValuesQueryHandler(IUsedValueRepository repository)
    : IRequestHandler<GetUsedValuesQuery, IReadOnlyList<string>>
{
    public Task<IReadOnlyList<string>> HandleAsync(GetUsedValuesQuery request, CancellationToken cancellationToken)
        => repository.FindAllAsync(request.UserId, request.Kind, cancellationToken);
}
