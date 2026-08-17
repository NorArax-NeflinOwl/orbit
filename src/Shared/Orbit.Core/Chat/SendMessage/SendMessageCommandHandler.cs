using Orbit.Core.Abstractions;
using Orbit.Core.Users;

namespace Orbit.Core.Chat.SendMessage;

public sealed class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, ChatMessage?>
{
    private readonly IUserRepository _userRepository;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly IContactRepository _contactRepository;

    public SendMessageCommandHandler(
        IUserRepository userRepository, IChatMessageRepository chatMessageRepository, IContactRepository contactRepository)
    {
        _userRepository = userRepository;
        _chatMessageRepository = chatMessageRepository;
        _contactRepository = contactRepository;
    }

    /// <summary>
    /// Returns null when the recipient doesn't exist, so the API can turn that into a 404. On success,
    /// also makes sure a Contact row exists for both the sender and the recipient - this is what turns
    /// "sent the first message" into "shows up in each other's chat list" (see ContactRepository).
    /// </summary>
    public async Task<ChatMessage?> HandleAsync(SendMessageCommand request, CancellationToken cancellationToken)
    {
        if (await _userRepository.GetByIdAsync(request.RecipientUserId, cancellationToken) is null)
        {
            return null;
        }

        var message = ChatMessage.Create(request.SenderUserId, request.RecipientUserId, request.CiphertextBase64, request.NonceBase64);
        await _chatMessageRepository.AddAsync(message, cancellationToken);

        await _contactRepository.EnsureContactAsync(request.SenderUserId, request.RecipientUserId, message.SentAtUtc, cancellationToken);
        await _contactRepository.EnsureContactAsync(request.RecipientUserId, request.SenderUserId, message.SentAtUtc, cancellationToken);

        return message;
    }
}
