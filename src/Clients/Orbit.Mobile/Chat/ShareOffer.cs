using System.Text.Json;
using Orbit.Contracts.Chat;
using Orbit.Core.Sync;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Chat;

/// <summary>
/// A share somebody has offered, unwrapped from the chat message that carried it.
///
/// Sharing is not something the server pushes at the recipient: the owner records the offer and then
/// tells them by sending an ordinary end-to-end-encrypted message whose plaintext is one of four
/// structured shapes - see <see cref="NoteShareMessagePayload"/> and its three counterparts. Only the
/// two clients hold the key, so recognising the offer is a client's job on both of them; this is
/// Orbit.Web's TryParseShare, in the project a test can reach.
/// </summary>
/// <param name="EntityType">
/// One of <see cref="SyncEntityType"/>, so the four kinds are named here as they are everywhere else the
/// phone talks about them - and so accepting one can switch on that same vocabulary.
/// </param>
public sealed record ShareOffer(string EntityType, Guid ShareId, string Title)
{
    /// <summary>
    /// The offer inside this plaintext, or null when it is an ordinary message. Text that happens to be
    /// JSON without carrying one of the four markers is ordinary text too, exactly as a forward is - see
    /// <see cref="ForwardedMessage.TryUnwrap"/>.
    ///
    /// Tried shape by shape rather than probed once for its type: the four payloads name their title
    /// differently, so one pass would have to read the property by a name worked out first anyway. The
    /// brace check in front keeps every ordinary message clear of all of it.
    /// </summary>
    public static ShareOffer? TryUnwrap(string? plainText)
    {
        if (plainText is not { Length: > 0 } text || text[0] != '{')
        {
            return null;
        }

        try
        {
            return FromNote(text) ?? FromTaskList(text) ?? FromCalendarEvent(text) ?? FromWarehouse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// How a conversation names this offer, in the reader's language. Orbit.Web builds the same line by
    /// interpolating an English string; the phone is translated throughout, so it goes through the
    /// dictionary like every other piece of copy.
    /// </summary>
    public string Describe(Translations translations)
        => translations.Format(
            EntityType switch
            {
                SyncEntityType.TaskList => "Shared a task list: {0}",
                SyncEntityType.CalendarEvent => "Shared an event: {0}",
                SyncEntityType.Warehouse => "Shared a warehouse: {0}",
                _ => "Shared a note: {0}"
            },
            Title);

    private static ShareOffer? FromNote(string text)
        => JsonSerializer.Deserialize(text, ChatPayloadSerializerContext.Default.NoteShareMessagePayload)
            is { Type: NoteShareMessagePayload.MessageType } payload
            ? new ShareOffer(SyncEntityType.Note, payload.ShareId, payload.NoteTitle)
            : null;

    private static ShareOffer? FromTaskList(string text)
        => JsonSerializer.Deserialize(text, ChatPayloadSerializerContext.Default.TaskListShareMessagePayload)
            is { Type: TaskListShareMessagePayload.MessageType } payload
            ? new ShareOffer(SyncEntityType.TaskList, payload.ShareId, payload.TaskListTitle)
            : null;

    private static ShareOffer? FromCalendarEvent(string text)
        => JsonSerializer.Deserialize(text, ChatPayloadSerializerContext.Default.EventShareMessagePayload)
            is { Type: EventShareMessagePayload.MessageType } payload
            ? new ShareOffer(SyncEntityType.CalendarEvent, payload.ShareId, payload.EventTitle)
            : null;

    private static ShareOffer? FromWarehouse(string text)
        => JsonSerializer.Deserialize(text, ChatPayloadSerializerContext.Default.WarehouseShareMessagePayload)
            is { Type: WarehouseShareMessagePayload.MessageType } payload
            ? new ShareOffer(SyncEntityType.Warehouse, payload.ShareId, payload.WarehouseName)
            : null;
}
