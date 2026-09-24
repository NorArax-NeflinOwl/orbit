using System.Text.Json;
using Orbit.Contracts.Chat;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Chat;

/// <summary>
/// Asking whoever owns something to let you change it. Sent as an ordinary chat message, like the share
/// offers, so it travels through the same end-to-end encryption and the server learns nothing about it.
///
/// Deliberately a request rather than a grant: only the owner can widen access, so all this does is ask.
/// They answer by sharing it again at a level that permits editing.
/// </summary>
public sealed record EditAccessRequest(SharedItemKind Kind, Guid ItemId, string Name)
{
    /// <summary>The message that asks, which is the same shape Orbit.Web sends.</summary>
    public string ToMessage()
        => JsonSerializer.Serialize(
            new EditAccessRequestPayload(Kind.ToString(), ItemId, Name),
            ChatPayloadSerializerContext.Default.EditAccessRequestPayload);

    /// <summary>
    /// What the bubble says the ask is about - "Asked to edit a note", as Orbit.Web's own line says it
    /// (Chat.razor's BuildEditAccessRequestNotice). The browser puts the kind and the name in one
    /// sentence; the phone draws the name on its own line under this, which is the shape every other
    /// structured bubble there has. Until 2026-09-24 the phone said only "Asked to edit", so a reader
    /// with two requests waiting had the names and not what either of them was.
    ///
    /// Worded here rather than in the converter that draws it, because Orbit.Maui is outside the test
    /// suite and a sentence nothing can read is a sentence nobody checks.
    /// </summary>
    public string AskedToEdit(Translations translations)
        => translations.Format("Asked to edit {0}", NameOfTheKind(translations));

    /// <remarks>
    /// The four the browser tells apart, in the accusative Polish needs after that sentence - the same
    /// words a share notice is written from. A place is named rather than falling through: nothing
    /// asks to edit one today, since a place is shared rather than worked on by somebody else, but the
    /// payload can carry any kind and calling a place an inventory would be worse than saying nothing.
    /// </remarks>
    private string NameOfTheKind(Translations translations) => Kind switch
    {
        SharedItemKind.Note => translations["a note"],
        SharedItemKind.TaskList => translations["a task list"],
        SharedItemKind.CalendarEvent => translations["an event"],
        SharedItemKind.Place => translations["a place"],
        _ => translations["an inventory"]
    };

    /// <inheritdoc cref="SharedItemInvitation.TryRead"/>
    public static EditAccessRequest? TryRead(string plainText)
    {
        if (plainText.Length == 0 || plainText[0] != '{')
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(plainText, ChatPayloadSerializerContext.Default.EditAccessRequestPayload)
                is { Type: EditAccessRequestPayload.MessageType } payload
                && Enum.TryParse<SharedItemKind>(payload.ItemType, out var kind)
                ? new EditAccessRequest(kind, payload.ItemId, payload.ItemTitle)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
