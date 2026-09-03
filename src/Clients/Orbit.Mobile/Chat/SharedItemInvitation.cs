using System.Text.Json;
using Orbit.Contracts.Chat;

namespace Orbit.Mobile.Chat;

/// <summary>Which kind of thing was offered, which decides where accepting it sends the copy.</summary>
public enum SharedItemKind
{
    Note,
    TaskList,
    CalendarEvent,
    Inventory
}

/// <summary>
/// Somebody offering to share something. It arrives as an ordinary chat message whose plaintext happens
/// to be a structured payload, so it travels through the same end-to-end encryption as anything else -
/// the server holds ciphertext and learns nothing about the offer. What the server does rely on is the
/// share row created before the message was sent; this only carries its id to whoever can read it.
///
/// The phone understood none of these, so a note shared from the browser arrived on a phone as a blob
/// of JSON. Recognising them is what turns that back into an offer somebody can accept.
/// </summary>
public sealed record SharedItemInvitation(SharedItemKind Kind, Guid ShareId, string Name)
{
    /// <summary>
    /// The offer inside this plaintext, or null when it is ordinary text. Text that happens to be JSON
    /// but carries no marker is ordinary text - a message reading "{}" is a message reading "{}".
    /// </summary>
    public static SharedItemInvitation? TryRead(string plainText)
    {
        if (plainText.Length == 0 || plainText[0] != '{')
        {
            return null;
        }

        try
        {
            return ReadNote(plainText)
                ?? ReadTaskList(plainText)
                ?? ReadEvent(plainText)
                ?? ReadInventory(plainText);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SharedItemInvitation? ReadNote(string plainText)
        => JsonSerializer.Deserialize(plainText, ChatPayloadSerializerContext.Default.NoteShareMessagePayload)
            is { Type: NoteShareMessagePayload.MessageType } payload
            ? new SharedItemInvitation(SharedItemKind.Note, payload.ShareId, payload.NoteTitle)
            : null;

    private static SharedItemInvitation? ReadTaskList(string plainText)
        => JsonSerializer.Deserialize(plainText, ChatPayloadSerializerContext.Default.TaskListShareMessagePayload)
            is { Type: TaskListShareMessagePayload.MessageType } payload
            ? new SharedItemInvitation(SharedItemKind.TaskList, payload.ShareId, payload.TaskListTitle)
            : null;

    private static SharedItemInvitation? ReadEvent(string plainText)
        => JsonSerializer.Deserialize(plainText, ChatPayloadSerializerContext.Default.EventShareMessagePayload)
            is { Type: EventShareMessagePayload.MessageType } payload
            ? new SharedItemInvitation(SharedItemKind.CalendarEvent, payload.ShareId, payload.EventTitle)
            : null;

    private static SharedItemInvitation? ReadInventory(string plainText)
        => JsonSerializer.Deserialize(plainText, ChatPayloadSerializerContext.Default.InventoryShareMessagePayload)
            is { Type: InventoryShareMessagePayload.MessageType } payload
            ? new SharedItemInvitation(SharedItemKind.Inventory, payload.ShareId, payload.InventoryName)
            : null;
}
