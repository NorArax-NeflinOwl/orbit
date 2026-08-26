namespace Orbit.Contracts.Chat;

/// <summary>
/// Structured payload sent as an otherwise-ordinary chat message's plaintext body when someone replies
/// to a particular message, so it travels through the same end-to-end encryption as any other message.
/// Mirrors ForwardedMessagePayload - see its class comment for why this shape exists: the server only
/// ever sees ciphertext, so it has no way to know (or need to know) that a message is a reply at all.
/// </summary>
/// <param name="ReplyToMessageId">
/// What is being answered. The reply renders a quote that scrolls to it, which is the whole point - a
/// long conversation otherwise leaves the reader guessing which message an answer belongs to.
/// </param>
/// <param name="ReplyToPreview">
/// A short copy of what that message said, carried rather than looked up: the original may have been
/// edited or deleted since, and a quote of something that is no longer there is still what the reply was
/// answering.
/// </param>
public sealed record ReplyMessagePayload(Guid ReplyToMessageId, string ReplyToPreview, string Content)
{
    public const string MessageType = "orbit/reply-message";

    /// <summary>Long enough to recognise which message is meant, short enough not to repeat it wholesale.</summary>
    public const int MaximumPreviewLength = 120;

    public string Type { get; init; } = MessageType;

    public static string Preview(string content)
        => content.Length <= MaximumPreviewLength ? content : content[..MaximumPreviewLength].TrimEnd() + "…";
}
