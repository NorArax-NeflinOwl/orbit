using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.GetNoteShareStatus;

public sealed class GetNoteShareStatusQueryHandler : IRequestHandler<GetNoteShareStatusQuery, bool?>
{
    private readonly INoteShareRepository _noteShareRepository;

    public GetNoteShareStatusQueryHandler(INoteShareRepository noteShareRepository)
    {
        _noteShareRepository = noteShareRepository;
    }

    public async Task<bool?> HandleAsync(GetNoteShareStatusQuery request, CancellationToken cancellationToken)
    {
        var share = await _noteShareRepository.GetByIdAsync(request.RecipientUserId, request.ShareId, cancellationToken);
        return share?.IsAccepted;
    }
}
