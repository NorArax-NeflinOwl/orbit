using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Contracts.Chat;
using Orbit.Core.Abstractions;
using Orbit.Core.Chat;
using Orbit.Core.Chat.GetContacts;
using Orbit.Core.Chat.GetConversation;
using Orbit.Core.Chat.GetReadReceipt;
using Orbit.Core.Chat.MarkConversationAsRead;
using Orbit.Core.Chat.SendMessage;

namespace Orbit.Api.Chat;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this WebApplication app)
    {
        // Every contact list and conversation belongs to exactly one user (see GetUserId below), so the
        // whole group requires a valid, authenticated caller.
        var chat = app.MapGroup("/api/chat").RequireAuthorization();

        chat.MapGet("/contacts", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetContactsQuery(GetUserId(user)), cancellationToken);
            return Results.Ok(result.Select(ToDto));
        });

        chat.MapGet("/messages/{otherUserId:guid}", async (
            Guid otherUserId, DateTimeOffset? sinceUtc, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new GetConversationQuery(GetUserId(user), otherUserId, sinceUtc), cancellationToken);
            return Results.Ok(result.Select(ToDto));
        });

        chat.MapPost("/messages", async (
            SendMessageRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var message = await dispatcher.SendAsync(
                new SendMessageCommand(GetUserId(user), request.RecipientUserId, request.CiphertextBase64, request.NonceBase64),
                cancellationToken);
            return message is null ? Results.NotFound() : Results.Ok(ToDto(message));
        });

        // Called by the recipient's chat window on every poll tick while it's open - see Chat.razor's
        // SyncReadStateAsync for the (currently coarse) definition of "read" this drives.
        chat.MapPut("/messages/{otherUserId:guid}/read", async (
            Guid otherUserId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            await dispatcher.SendAsync(new MarkConversationAsReadCommand(GetUserId(user), otherUserId), cancellationToken);
            return Results.NoContent();
        });

        // Called by the sender's chat window to find out whether the other party has read what was sent
        // to them, so it can show a single vs. double checkmark.
        chat.MapGet("/messages/{otherUserId:guid}/read-receipt", async (
            Guid otherUserId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var readUpToUtc = await dispatcher.SendAsync(new GetReadReceiptQuery(GetUserId(user), otherUserId), cancellationToken);
            return Results.Ok(new ReadReceiptDto(readUpToUtc));
        });
    }

    /// <summary>
    /// Reads the authenticated user's id out of the JWT's "sub" claim. Safe to assume it's present and
    /// valid: the group requires authorization, and Orbit.Api only ever issues tokens with this claim
    /// (see TokenService).
    /// </summary>
    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated request is missing a 'sub' claim.");
        return Guid.Parse(subject);
    }

    private static ContactDto ToDto(ContactSummary contact)
        => new(contact.User.Id, contact.User.UserName, contact.User.DisplayName, contact.User.Email, contact.User.PublicKeyBase64, contact.LastMessageAtUtc);

    private static ChatMessageDto ToDto(ChatMessage message)
        => new(message.Id, message.SenderUserId, message.RecipientUserId, message.CiphertextBase64, message.NonceBase64, message.SentAtUtc);
}
