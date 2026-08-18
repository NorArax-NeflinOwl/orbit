using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Contracts.Chat;
using Orbit.Core.Abstractions;
using Orbit.Core.Chat;
using Orbit.Core.Chat.ApproveConversation;
using Orbit.Core.Chat.GetContacts;
using Orbit.Core.Chat.GetConversation;
using Orbit.Core.Chat.GetConversationAccess;
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
            var result = await dispatcher.SendAsync(
                new SendMessageCommand(GetUserId(user), request.RecipientUserId, request.CiphertextBase64, request.NonceBase64),
                cancellationToken);

            return result.Outcome switch
            {
                SendMessageOutcome.Success => Results.Ok(ToDto(result.Message!)),
                SendMessageOutcome.RecipientNotFound => Results.NotFound(),
                // The sender is replying to a chat request someone else sent them that they haven't
                // approved yet (see ChatConversationAccess) - Chat.razor normally prevents ever reaching
                // this by hiding the compose box until the recipient approves, so this is a defense-in-
                // depth 403 rather than a state the client is expected to handle specially.
                SendMessageOutcome.ConversationNotApproved => Results.StatusCode(StatusCodes.Status403Forbidden),
                _ => throw new InvalidOperationException($"Unhandled {nameof(SendMessageOutcome)}: {result.Outcome}")
            };
        });

        // Looked up by the chat window on load and on every poll tick, so it can show a "chat request"
        // banner (and disable the compose box) as soon as either party's approval state changes - see
        // ChatConversationAccess. Returns no body (null) when the pair has never exchanged a message.
        chat.MapGet("/conversations/{otherUserId:guid}/access", async (
            Guid otherUserId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var access = await dispatcher.SendAsync(new GetConversationAccessQuery(GetUserId(user), otherUserId), cancellationToken);
            return Results.Ok(access is null ? null : ToDto(access));
        });

        // Lets the non-initiating party in a brand-new conversation explicitly allow chatting with
        // whoever started it - see ChatConversationAccess and SendMessageCommandHandler for what this
        // unblocks.
        chat.MapPost("/conversations/{otherUserId:guid}/approve", async (
            Guid otherUserId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var approved = await dispatcher.SendAsync(new ApproveConversationCommand(GetUserId(user), otherUserId), cancellationToken);
            return approved ? Results.NoContent() : Results.NotFound();
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
        => new(
            contact.User.Id, contact.User.UserName, contact.User.DisplayName, contact.User.Email, contact.User.PublicKeyBase64,
            contact.LastMessageAtUtc, contact.RequiresApprovalFromCurrentUser);

    private static ChatMessageDto ToDto(ChatMessage message)
        => new(message.Id, message.SenderUserId, message.RecipientUserId, message.CiphertextBase64, message.NonceBase64, message.SentAtUtc);

    private static ChatConversationAccessDto ToDto(ChatConversationAccess access)
        => new(access.InitiatedByUserId, access.IsApproved);
}
