using Orbit.Core.Abstractions;
using Orbit.Core.Users;

namespace Orbit.Core.Chat.SendMessage;

public sealed class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, SendMessageResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly IContactRepository _contactRepository;
    private readonly IChatConversationAccessRepository _chatConversationAccessRepository;

    public SendMessageCommandHandler(
        IUserRepository userRepository,
        IChatMessageRepository chatMessageRepository,
        IContactRepository contactRepository,
        IChatConversationAccessRepository chatConversationAccessRepository)
    {
        _userRepository = userRepository;
        _chatMessageRepository = chatMessageRepository;
        _contactRepository = contactRepository;
        _chatConversationAccessRepository = chatConversationAccessRepository;
    }

    /// <summary>
    /// Fails with RecipientNotFound when the recipient doesn't exist (the API turns that into a 404),
    /// or with ConversationNotApproved when the sender is replying to a conversation someone else
    /// started with them that they haven't approved yet (turned into a 403 - see ChatConversationAccess;
    /// Chat.razor normally prevents ever reaching this by hiding the compose box until the recipient
    /// approves). On success, also makes sure a Contact row exists for both the sender and the recipient
    /// - this is what turns "sent the first message" into "shows up in each other's chat list" (see
    /// ContactRepository) - and, for the very first message between these two users, creates the
    /// pending ChatConversationAccess row that gates the recipient's own replies until they approve.
    /// </summary>
    public async Task<SendMessageResult> HandleAsync(SendMessageCommand request, CancellationToken cancellationToken)
    {
        if (await _userRepository.GetByIdAsync(request.RecipientUserId, cancellationToken) is null)
        {
            return SendMessageResult.RecipientNotFound();
        }

        var access = await _chatConversationAccessRepository.GetAsync(request.SenderUserId, request.RecipientUserId, cancellationToken);
        if (access is not null && !access.CanSend(request.SenderUserId))
        {
            return SendMessageResult.ConversationNotApproved();
        }

        await _chatConversationAccessRepository.EnsureCreatedAsync(request.SenderUserId, request.RecipientUserId, cancellationToken);

        var message = ChatMessage.Create(request.SenderUserId, request.RecipientUserId, request.CiphertextBase64, request.NonceBase64);
        await _chatMessageRepository.AddAsync(message, cancellationToken);

        await _contactRepository.EnsureContactAsync(request.SenderUserId, request.RecipientUserId, message.SentAtUtc, cancellationToken);
        await _contactRepository.EnsureContactAsync(request.RecipientUserId, request.SenderUserId, message.SentAtUtc, cancellationToken);

        return SendMessageResult.Success(message);
    }
}
