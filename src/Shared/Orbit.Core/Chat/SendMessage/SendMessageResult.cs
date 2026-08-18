namespace Orbit.Core.Chat.SendMessage;

public enum SendMessageOutcome
{
    Success,
    RecipientNotFound,
    ConversationNotApproved
}

/// <summary>
/// Outcome of SendMessageCommand - mirrors AuthResult's shape (a single outcome plus a payload that
/// only applies to the success case), so ChatEndpoints can map each outcome to the right HTTP status
/// without SendMessageCommandHandler needing to know anything about HTTP.
/// </summary>
public sealed class SendMessageResult
{
    public SendMessageOutcome Outcome { get; }
    public ChatMessage? Message { get; }

    private SendMessageResult(SendMessageOutcome outcome, ChatMessage? message)
    {
        Outcome = outcome;
        Message = message;
    }

    public static SendMessageResult Success(ChatMessage message) => new(SendMessageOutcome.Success, message);
    public static SendMessageResult RecipientNotFound() => new(SendMessageOutcome.RecipientNotFound, message: null);
    public static SendMessageResult ConversationNotApproved() => new(SendMessageOutcome.ConversationNotApproved, message: null);
}
