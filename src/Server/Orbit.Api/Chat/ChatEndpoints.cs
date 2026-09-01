using Orbit.Core.Chat.ClearConversationHistory;
using Orbit.Core.Chat.Groups.LeaveChatGroup;
using Orbit.Core.Chat.Groups.SetGroupArchived;
using Orbit.Core.Chat.SetConversationArchived;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Api.Permissions;
using Orbit.Contracts.Chat;
using Orbit.Core.Abstractions;
using Orbit.Core.Chat;
using Orbit.Core.Chat.ApproveConversation;
using Orbit.Core.Chat.EditMessage;
using Orbit.Core.Chat.Groups.SendGroupMessage;
using Orbit.Core.Chat.Groups.ShareGroupHistory;
using Orbit.Core.Chat.Groups.ManageChatGroupMembers;
using Orbit.Core.Chat.Groups.EditGroupMessage;
using Orbit.Core.Chat.Groups.GetGroupAnnouncements;
using Orbit.Core.Chat.Groups.GetGroupConversation;
using Orbit.Core.Chat.Groups.MarkGroupConversationAsRead;
using Orbit.Core.Chat.Groups.GetGroupMessageReceipts;
using Orbit.Core.Chat.Groups.GetChatGroups;
using Orbit.Core.Chat.Groups.CreateChatGroup;
using Orbit.Core.Chat.Groups;
using Orbit.Core.Chat.DeleteMessage;
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
        // One-to-one conversations and group conversations are separately unlocked (see
        // PermissionPolicies), so they are separate route groups rather than one - "/api/chat/groups"
        // still, but gated on its own.
        // The contact list is about other people existing; the conversations are about talking to them.
        // They unlock separately, so they are separate route groups - see PermissionPolicies.
        var contacts = app.MapGroup("/api/chat").RequireAuthorization(PermissionPolicies.Contacts);
        var chat = app.MapGroup("/api/chat").RequireAuthorization(PermissionPolicies.Chat);
        var groups = app.MapGroup("/api/chat/groups").RequireAuthorization(PermissionPolicies.Chat);

        contacts.MapGet("/contacts", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
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
                new SendMessageCommand(
                    GetUserId(user), request.RecipientUserId, request.CiphertextBase64, request.NonceBase64, request.IsShareInvitation),
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
        // ChatConversationAccess. Answers with 204 (rather than 200 with a JSON "null" body) when the
        // pair has never exchanged a message: Results.Ok(null) writes an empty response body instead of
        // the literal 4-byte "null", which made the client's GetFromJsonAsync throw on an empty body
        // instead of parsing null.
        // Putting a conversation away, and bringing it back. On the caller's own list only - see
        // SetConversationArchivedCommand for why there is no "archive for everybody".
        chat.MapPut("/conversations/{otherUserId:guid}/archived", async (
            Guid otherUserId, SetArchivedRequest request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var changed = await dispatcher.SendAsync(
                new SetConversationArchivedCommand(GetUserId(user), otherUserId, request.IsArchived), cancellationToken);
            return changed ? Results.NoContent() : Results.NotFound();
        });

        // Emptying a conversation, for the caller only. A DELETE on the messages rather than on the
        // conversation, because the conversation itself stays: the other party keeps every word, and
        // writing again starts it up where it left off - see ClearConversationHistoryCommand.
        chat.MapDelete("/conversations/{otherUserId:guid}/messages", async (
            Guid otherUserId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var cleared = await dispatcher.SendAsync(
                new ClearConversationHistoryCommand(GetUserId(user), otherUserId), cancellationToken);
            return cleared ? Results.NoContent() : Results.NotFound();
        });

        chat.MapGet("/conversations/{otherUserId:guid}/access", async (
            Guid otherUserId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var access = await dispatcher.SendAsync(new GetConversationAccessQuery(GetUserId(user), otherUserId), cancellationToken);
            return access is null ? Results.NoContent() : Results.Ok(ToDto(access));
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

        // Re-encrypts and overwrites an already-sent message's content - only its original sender may do
        // this (see EditMessageCommandHandler). Never offered for a share-notice message (an event/note/
        // task-list invite) - Chat.razor only shows the "Edit" option on the sender's own plain-text
        // bubbles.

        // Groups. Membership decides everything here, so a caller who isn't in a group gets the same
        // 404 as one asking about a group that doesn't exist - see IChatGroupRepository's comment.
        groups.MapPost("/", async (
            CreateChatGroupRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var groupId = await dispatcher.SendAsync(
                new CreateChatGroupCommand(GetUserId(user), request.Name, request.MemberUserIds), cancellationToken);
            return Results.Created($"/api/chat/groups/{groupId}", groupId);
        });

        groups.MapGet("/", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var callerId = GetUserId(user);
            var groups = await dispatcher.SendAsync(new GetChatGroupsQuery(callerId), cancellationToken);
            return Results.Ok(groups.Select(group => ToDto(group, callerId)));
        });

        groups.MapPost("/{groupId:guid}/members", async (
            Guid groupId, AddChatGroupMemberRequest request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var added = await dispatcher.SendAsync(
                new AddChatGroupMemberCommand(GetUserId(user), groupId, request.UserId), cancellationToken);
            return added ? Results.NoContent() : Results.NotFound();
        });

        groups.MapDelete("/{groupId:guid}/members/{userId:guid}", async (
            Guid groupId, Guid userId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var removed = await dispatcher.SendAsync(
                new RemoveChatGroupMemberCommand(GetUserId(user), groupId, userId), cancellationToken);
            return removed ? Results.NoContent() : Results.NotFound();
        });

        groups.MapPut("/{groupId:guid}/members/{userId:guid}/role", async (
            Guid groupId, Guid userId, ChangeChatGroupMemberRoleRequest request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var changed = await dispatcher.SendAsync(
                new ChangeChatGroupMemberRoleCommand(
                    GetUserId(user), groupId, userId, RequestEnum.Parse<ChatGroupRole>(request.Role, "role")),
                cancellationToken);
            return changed ? Results.NoContent() : Results.NotFound();
        });

        // sinceUtc matches the one-to-one conversation's own cursor: a client polling a group can ask
        // for what it has not seen instead of the whole conversation every tick.
        // The same for a group, and for the same reason: this is one member's view, not the group's.
        groups.MapPut("/{groupId:guid}/archived", async (
            Guid groupId, SetArchivedRequest request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var changed = await dispatcher.SendAsync(
                new SetGroupArchivedCommand(GetUserId(user), groupId, request.IsArchived), cancellationToken);
            return changed ? Results.NoContent() : Results.NotFound();
        });

        // Leaving a group and taking your copies of what was said in it with you. One endpoint for
        // both, because leaving and still holding every message is a state nobody asks for - see
        // LeaveChatGroupCommand. Separate from removing a member: that one is an admin acting on
        // somebody else, and it is refused for anybody but an admin.
        groups.MapDelete("/{groupId:guid}/membership", async (
            Guid groupId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var left = await dispatcher.SendAsync(
                new LeaveChatGroupCommand(GetUserId(user), groupId), cancellationToken);
            return left ? Results.NoContent() : Results.NotFound();
        });

        groups.MapGet("/{groupId:guid}/messages", async (
            Guid groupId, DateTimeOffset? sinceUtc, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var messages = await dispatcher.SendAsync(
                new GetGroupConversationQuery(GetUserId(user), groupId, sinceUtc), cancellationToken);
            return Results.Ok(messages.Select(ToDto).ToList());
        });

        // Who this message reached and who has read it, for the message's own info view. Separate from
        // the conversation because it is opened per message, not drawn for every one of them.
        groups.MapGet("/{groupId:guid}/messages/{groupMessageId:guid}/receipts", async (
            Guid groupId, Guid groupMessageId, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var receipts = await dispatcher.SendAsync(
                new GetGroupMessageReceiptsQuery(GetUserId(user), groupId, groupMessageId), cancellationToken);
            return Results.Ok(receipts.Select(ToDto).ToList());
        });

        // Marks everything addressed to this reader in the group as read - the group counterpart of the
        // one-to-one route above.
        groups.MapPut("/{groupId:guid}/read", async (
            Guid groupId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var marked = await dispatcher.SendAsync(
                new MarkGroupConversationAsReadCommand(GetUserId(user), groupId), cancellationToken);
            return marked ? Results.NoContent() : Results.NotFound();
        });

        groups.MapPost("/{groupId:guid}/messages", async (
            Guid groupId, SendGroupMessageRequest request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var copies = request.Copies
                .Select(copy => new GroupMessageCopy(copy.RecipientUserId, copy.CiphertextBase64, copy.NonceBase64))
                .ToList();
            var sent = await dispatcher.SendAsync(new SendGroupMessageCommand(GetUserId(user), groupId, copies), cancellationToken);
            return sent ? Results.NoContent() : Results.NotFound();
        });

        // The conversation's own "somebody joined" lines. A route of their own rather than folded into
        // the messages: they are a different shape, and every client already installed reads that
        // response as a plain list of messages.
        groups.MapGet("/{groupId:guid}/announcements", async (
            Guid groupId, DateTimeOffset? sinceUtc, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var announcements = await dispatcher.SendAsync(
                new GetGroupAnnouncementsQuery(GetUserId(user), groupId, sinceUtc), cancellationToken);
            return Results.Ok(announcements.Select(ToDto));
        });

        // The past, re-encrypted for somebody who joined after it happened - the server holds no key to
        // any of it, so the copies arrive already sealed by whoever is sharing. Answers with how many
        // were actually stored: a message the sharer cannot read is not theirs to pass on, and one the
        // recipient already has is not stored twice.
        groups.MapPost("/{groupId:guid}/history", async (
            Guid groupId, ShareGroupHistoryRequest request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var copies = request.Copies
                .Select(copy => new SharedHistoryCopy(copy.GroupMessageId, copy.CiphertextBase64, copy.NonceBase64))
                .ToList();
            var shared = await dispatcher.SendAsync(
                new ShareGroupHistoryCommand(GetUserId(user), groupId, request.RecipientUserId, copies), cancellationToken);
            return Results.Ok(shared);
        });

        groups.MapPut("/{groupId:guid}/messages/{groupMessageId:guid}", async (
            Guid groupId, Guid groupMessageId, SendGroupMessageRequest request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var copies = request.Copies
                .Select(copy => new GroupMessageCopy(copy.RecipientUserId, copy.CiphertextBase64, copy.NonceBase64))
                .ToList();
            var edited = await dispatcher.SendAsync(
                new EditGroupMessageCommand(GetUserId(user), groupMessageId, copies), cancellationToken);
            return edited ? Results.NoContent() : Results.NotFound();
        });

        chat.MapDelete("/messages/{messageId:guid}", async (
            Guid messageId, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var deleted = await dispatcher.SendAsync(new DeleteChatMessageCommand(GetUserId(user), messageId), cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

        chat.MapPut("/messages/{messageId:guid}", async (
            Guid messageId, EditMessageRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new EditMessageCommand(messageId, GetUserId(user), request.CiphertextBase64, request.NonceBase64), cancellationToken);

            return result.Outcome switch
            {
                EditMessageOutcome.Success => Results.Ok(ToDto(result.Message!)),
                EditMessageOutcome.MessageNotFound => Results.NotFound(),
                EditMessageOutcome.Forbidden => Results.StatusCode(StatusCodes.Status403Forbidden),
                _ => throw new InvalidOperationException($"Unhandled {nameof(EditMessageOutcome)}: {result.Outcome}")
            };
        });
    }

    /// <summary>
    /// Reads the authenticated user's id out of the JWT's "sub" claim. Safe to assume it's present and
    /// valid: the group requires authorization, and Orbit.Api only ever issues tokens with this claim
    /// (see TokenService).
    /// </summary>

    private static ChatGroupAnnouncementDto ToDto(ChatGroupAnnouncement announcement)
        => new(
            announcement.Id, announcement.JoinedUserId, announcement.AddedByUserId, announcement.HistoryShared,
            announcement.AnnouncedAtUtc);

    private static ChatGroupDto ToDto(ChatGroup group, Guid callerUserId)
        => new(
            group.Id, group.Name, group.CreatedByUserId, group.CreatedAtUtc,
            group.FindMember(callerUserId)?.Role.ToString() ?? ChatGroupRole.Member.ToString(),
            group.Members.Select(member => new ChatGroupMemberDto(member.UserId, member.Role.ToString(), member.JoinedAtUtc)).ToList(),
            group.LastMessageAtUtc,
            // The caller's own membership, not the group's - archiving is one member's view of it.
            group.FindMember(callerUserId)?.IsArchived ?? false);

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated request is missing a 'sub' claim.");
        return Guid.Parse(subject);
    }

    private static ContactDto ToDto(ContactSummary contact)
        => new(
            contact.User.Id, contact.User.UserName, contact.User.DisplayName, contact.User.Email, contact.User.PublicKeyBase64,
            contact.LastMessageAtUtc, contact.RequiresApprovalFromCurrentUser, contact.IsPendingApprovalFromOtherParty,
            contact.UnreadCount, contact.User.Presence.StatusAt(DateTimeOffset.UtcNow).ToString(),
            contact.IsArchived);

    private static ChatMessageDto ToDto(GroupConversationEntry entry)
        => ToDto(entry.Message) with { ReadByEveryone = entry.ReadByEveryone };

    private static GroupMessageReceiptDto ToDto(GroupMessageReceipt receipt)
        => new(receipt.RecipientUserId, receipt.ReadAtUtc);

    private static ChatMessageDto ToDto(ChatMessage message)
        => new(
            message.Id, message.SenderUserId, message.RecipientUserId, message.CiphertextBase64, message.NonceBase64, message.SentAtUtc,
            message.IsEdited, message.EditedAtUtc, message.GroupMessageId);

    private static ChatConversationAccessDto ToDto(ChatConversationAccess access)
        => new(access.InitiatedByUserId, access.IsApproved);
}
