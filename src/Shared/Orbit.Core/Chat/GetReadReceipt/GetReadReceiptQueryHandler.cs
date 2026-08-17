using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.GetReadReceipt;

public sealed class GetReadReceiptQueryHandler : IRequestHandler<GetReadReceiptQuery, DateTimeOffset?>
{
    private readonly IChatMessageRepository _chatMessageRepository;

    public GetReadReceiptQueryHandler(IChatMessageRepository chatMessageRepository)
    {
        _chatMessageRepository = chatMessageRepository;
    }

    public Task<DateTimeOffset?> HandleAsync(GetReadReceiptQuery request, CancellationToken cancellationToken)
        => _chatMessageRepository.GetReadUpToUtcAsync(request.SenderUserId, request.RecipientUserId, cancellationToken);
}
