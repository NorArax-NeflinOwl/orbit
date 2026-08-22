using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.EditMessage;

public sealed class EditMessageCommandHandler : IRequestHandler<EditMessageCommand, EditMessageResult>
{
    private readonly IChatMessageRepository _chatMessageRepository;

    public EditMessageCommandHandler(IChatMessageRepository chatMessageRepository)
    {
        _chatMessageRepository = chatMessageRepository;
    }

    /// <summary>
    /// Fails with MessageNotFound when no message with that id exists, or with Forbidden when the
    /// requesting user isn't the message's original sender - only the sender can ever edit their own
    /// message, never the recipient (Chat.razor only ever offers the "Edit" option on the sender's own
    /// bubbles, so reaching Forbidden here means either a stale UI state or a direct API call).
    /// </summary>
    public async Task<EditMessageResult> HandleAsync(EditMessageCommand request, CancellationToken cancellationToken)
    {
        var message = await _chatMessageRepository.GetByIdAsync(request.MessageId, cancellationToken);
        if (message is null)
        {
            return EditMessageResult.MessageNotFound();
        }

        if (message.SenderUserId != request.RequestingUserId)
        {
            return EditMessageResult.Forbidden();
        }

        var editedAtUtc = DateTimeOffset.UtcNow;
        await _chatMessageRepository.UpdateContentAsync(request.MessageId, request.CiphertextBase64, request.NonceBase64, editedAtUtc, cancellationToken);
        message.ApplyEdit(request.CiphertextBase64, request.NonceBase64, editedAtUtc);

        return EditMessageResult.Success(message);
    }
}
