namespace Orbit.Core.Chat.Groups.GetGroupConversation;

/// <summary>
/// One message in a group conversation as it looks to one reader.
///
/// <paramref name="ReadByEveryone"/> is null for a message the reader did not send: whether everybody
/// has caught up is the sender's business, and showing it on somebody else's message would be reporting
/// on people to a third party. It sits here rather than on <see cref="ChatMessage"/> because it is not a
/// property of the message - it is the answer to a question only this reader asked.
/// </summary>
public sealed record GroupConversationEntry(ChatMessage Message, bool? ReadByEveryone);
