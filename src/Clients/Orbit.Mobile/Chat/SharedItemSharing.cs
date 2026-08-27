using System.Text.Json;
using Orbit.Contracts.Chat;
using Orbit.Mobile.Api;

namespace Orbit.Mobile.Chat;

/// <summary>
/// What happened when something was offered to somebody. Named apart from
/// <c>Orbit.Core.Abstractions.SharingOutcome</c>, which is the server's word for a different question.
/// </summary>
public enum SharingOutcome
{
    Offered,

    /// <summary>They already had it. The offer is sent again, which reads as a reminder rather than a failure.</summary>
    AlreadyShared,

    /// <summary>The thing is gone, or was never this account's to give.</summary>
    Refused,

    /// <summary>The server could not be reached. Nothing was offered - sharing is not queued.</summary>
    Unreachable
}

/// <summary>
/// Offering something to another account. Two steps that both have to happen: the server records the
/// offer and returns its id, and then a chat message carries that id to the recipient.
///
/// The message is the client's job rather than the server's for one reason: it is end-to-end encrypted,
/// and only a client holds the key. That is why the server's own endpoint says nothing to anybody - see
/// the comment on NoteEndpoints' share route.
///
/// One place rather than four, for the same reason as <see cref="SharedItemAcceptance"/>: which endpoint
/// offers a thing follows from what kind of thing it is, and so does which payload announces it.
/// </summary>
public sealed class SharedItemSharing
{
    private readonly NotesClient _notes;
    private readonly TasksClient _tasks;
    private readonly CalendarClient _calendar;
    private readonly InventoryClient _inventory;
    private readonly EncryptedChatMessageSender _sender;

    public SharedItemSharing(
        NotesClient notes, TasksClient tasks, CalendarClient calendar, InventoryClient inventory,
        EncryptedChatMessageSender sender)
    {
        _notes = notes;
        _tasks = tasks;
        _calendar = calendar;
        _inventory = inventory;
        _sender = sender;
    }

    public async Task<SharingOutcome> ShareAsync(
        SharedItemKind kind, Guid itemId, string name, Guid recipientUserId, string accessLevel,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (await OfferAsync(kind, itemId, recipientUserId, accessLevel, cancellationToken) is not { } result)
            {
                return SharingOutcome.Refused;
            }

            await _sender.SendAsync(recipientUserId, Announce(kind, result.ShareId, name), cancellationToken);
            return result.AlreadyShared ? SharingOutcome.AlreadyShared : SharingOutcome.Offered;
        }
        catch (HttpRequestException)
        {
            return SharingOutcome.Unreachable;
        }
    }

    private Task<Orbit.Contracts.Sharing.ShareResultDto?> OfferAsync(
        SharedItemKind kind, Guid itemId, Guid recipientUserId, string accessLevel, CancellationToken cancellationToken)
        => kind switch
        {
            SharedItemKind.Note => _notes.ShareAsync(itemId, recipientUserId, accessLevel, cancellationToken),
            SharedItemKind.TaskList => _tasks.ShareAsync(itemId, recipientUserId, accessLevel, cancellationToken),
            SharedItemKind.CalendarEvent => _calendar.ShareAsync(itemId, recipientUserId, accessLevel, cancellationToken),
            _ => _inventory.ShareAsync(itemId, recipientUserId, accessLevel, cancellationToken)
        };

    /// <summary>
    /// Asks whoever owns something to let this account change it. No server call at all: only the owner
    /// can widen access, and they do it by sharing again at a higher level - see EditAccessRequest.
    /// </summary>
    public async Task<bool> AskToEditAsync(
        EditAccessRequest request, Guid ownerUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _sender.SendAsync(ownerUserId, request.ToMessage(), cancellationToken);
            return result is { ReachedTheServer: true, GivenUp: 0 };
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    /// <summary>The message that tells them, which is the same shape Orbit.Web sends - see SharedItemInvitation.</summary>
    private static string Announce(SharedItemKind kind, Guid shareId, string name) => kind switch
    {
        SharedItemKind.Note => JsonSerializer.Serialize(
            new NoteShareMessagePayload(shareId, name), ChatPayloadSerializerContext.Default.NoteShareMessagePayload),
        SharedItemKind.TaskList => JsonSerializer.Serialize(
            new TaskListShareMessagePayload(shareId, name), ChatPayloadSerializerContext.Default.TaskListShareMessagePayload),
        SharedItemKind.CalendarEvent => JsonSerializer.Serialize(
            new EventShareMessagePayload(shareId, name), ChatPayloadSerializerContext.Default.EventShareMessagePayload),
        _ => JsonSerializer.Serialize(
            new WarehouseShareMessagePayload(shareId, name), ChatPayloadSerializerContext.Default.WarehouseShareMessagePayload)
    };
}
