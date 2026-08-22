namespace Orbit.Core.Chat.EditMessage;

public enum EditMessageOutcome
{
    Success,
    MessageNotFound,

    /// <summary>The requesting user isn't the message's original sender - only the sender may edit it.</summary>
    Forbidden
}

public sealed class EditMessageResult
{
    public EditMessageOutcome Outcome { get; }
    public ChatMessage? Message { get; }

    private EditMessageResult(EditMessageOutcome outcome, ChatMessage? message)
    {
        Outcome = outcome;
        Message = message;
    }

    public static EditMessageResult Success(ChatMessage message) => new(EditMessageOutcome.Success, message);
    public static EditMessageResult MessageNotFound() => new(EditMessageOutcome.MessageNotFound, message: null);
    public static EditMessageResult Forbidden() => new(EditMessageOutcome.Forbidden, message: null);
}
