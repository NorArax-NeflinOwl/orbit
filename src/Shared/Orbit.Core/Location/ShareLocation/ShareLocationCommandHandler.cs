using Orbit.Core.Abstractions;
using Orbit.Core.Chat;

namespace Orbit.Core.Location.ShareLocation;

public sealed class ShareLocationCommandHandler : IRequestHandler<ShareLocationCommand, bool>
{
    private readonly ISharedLocationRepository _sharedLocationRepository;
    private readonly IContactRepository _contactRepository;

    public ShareLocationCommandHandler(ISharedLocationRepository sharedLocationRepository, IContactRepository contactRepository)
    {
        _sharedLocationRepository = sharedLocationRepository;
        _contactRepository = contactRepository;
    }

    public async Task<bool> HandleAsync(ShareLocationCommand request, CancellationToken cancellationToken)
    {
        // Only someone the caller already chats with: a position is not something to be able to push at
        // a stranger who never agreed to hear from them, the same rule group membership follows.
        var contacts = await _contactRepository.GetAllForUserAsync(request.SharerUserId, cancellationToken);
        if (contacts.All(contact => contact.ContactUserId != request.RecipientUserId))
        {
            throw new InvalidRequestException("You can only share your location with someone you already have a chat with.");
        }

        var existing = await _sharedLocationRepository.FindAsync(request.SharerUserId, request.RecipientUserId, cancellationToken);
        if (existing is null)
        {
            var sharedLocation = SharedLocation.Create(
                request.SharerUserId, request.RecipientUserId, request.CiphertextBase64, request.NonceBase64, request.IsContinuous);
            await _sharedLocationRepository.AddAsync(sharedLocation, cancellationToken);
            return true;
        }

        // Overwritten in place rather than added beside: one row per pair is what "no history" means
        // here, and a refresh every minute would otherwise become exactly the trail this must not keep.
        existing.Refresh(request.CiphertextBase64, request.NonceBase64, request.IsContinuous);
        await _sharedLocationRepository.UpdateAsync(existing, cancellationToken);
        return true;
    }
}
