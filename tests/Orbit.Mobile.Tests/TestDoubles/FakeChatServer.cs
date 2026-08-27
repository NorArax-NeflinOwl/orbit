using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Orbit.Contracts.Chat;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Orbit's chat endpoints, in memory. Stores what it is given without ever being able to read it, which
/// is what the real server does.
///
/// The group half enforces the one rule the client cannot be trusted with and the plan is built around:
/// a group message must carry exactly one copy per current member, no more and no fewer (see
/// SendGroupMessageCommandHandler). A fake that accepted anything would let a broken fan-out pass.
/// </summary>
internal sealed class FakeChatServer : HttpMessageHandler
{
    private readonly TimeProvider _timeProvider;
    private readonly List<ChatMessageDto> _messages = [];
    private readonly List<GroupMessage> _groupMessages = [];
    private readonly HashSet<Guid> _readMessageIds = [];
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public FakeChatServer(TimeProvider timeProvider) => _timeProvider = timeProvider;

    public List<ContactDto> Contacts { get; } = [];

    public List<ChatGroupDto> Groups { get; } = [];

    /// <summary>Every message the server accepted, in the order it accepted them.</summary>
    public IReadOnlyList<ChatMessageDto> Messages => _messages;

    /// <summary>Every copy of every group message, which is what the fan-out is judged by.</summary>
    public IReadOnlyList<ChatMessageDto> GroupMessageCopies => _groupMessages.Select(stored => stored.Message).ToList();

    /// <summary>When each member opened their copy, for the receipts endpoint. Absent means delivered.</summary>
    private readonly Dictionary<(Guid GroupMessageId, Guid RecipientUserId), DateTimeOffset> _groupReads = [];

    /// <summary>Somebody opened their copy. A member with no entry has one and has not opened it.</summary>
    public void MarkGroupMessageRead(Guid groupMessageId, Guid recipientUserId, DateTimeOffset readAtUtc)
        => _groupReads[(groupMessageId, recipientUserId)] = readAtUtc;

    /// <summary>
    /// One receipt per stored copy, which is what a receipt is: a member who joined after the message
    /// was sent has no copy and so appears in none.
    /// </summary>
    private List<GroupMessageReceiptDto> ReadReceiptsFor(Guid groupMessageId)
        => [.. _groupMessages
            .Where(stored => stored.Message.GroupMessageId == groupMessageId
                && stored.Message.RecipientUserId != CallerUserId)
            .Select(stored => new GroupMessageReceiptDto(
                stored.Message.RecipientUserId,
                _groupReads.TryGetValue((groupMessageId, stored.Message.RecipientUserId), out var readAt)
                    ? readAt
                    : null))]; 

    /// <summary>Whose token the requests are carrying, which the real server reads from the claim.</summary>
    public Guid CallerUserId { get; set; }

    public bool IsUnreachable { get; set; }

    /// <summary>
    /// Set to make every request come back refused - 401 is an expired session, which the app has to
    /// survive rather than crash on, since the screens start their loads without awaiting them.
    /// </summary>
    public HttpStatusCode? RefuseEverythingWith { get; set; }

    /// <summary>Set to make the server refuse sends - 403 is "they haven't approved this conversation".</summary>
    public HttpStatusCode? RefuseSendsWith { get; set; }

    /// <summary>Everyone this caller has allowed to chat with them, as approving records.</summary>
    public List<Guid> ApprovedConversations { get; } = [];

    public ContactDto AddContact(Guid userId, string? publicKeyBase64)
    {
        var contact = new ContactDto(
            userId, "someone", "Someone", "someone@orbit.example", publicKeyBase64,
            _timeProvider.GetUtcNow(), false, false);

        Contacts.Add(contact);
        return contact;
    }

    /// <summary>A group the caller is in, with the caller as its admin.</summary>
    public ChatGroupDto AddGroup(string name, params Guid[] otherMemberUserIds)
    {
        var members = otherMemberUserIds
            .Select(userId => new ChatGroupMemberDto(userId, "Member", _timeProvider.GetUtcNow()))
            .Prepend(new ChatGroupMemberDto(CallerUserId, "Admin", _timeProvider.GetUtcNow()))
            .ToList();

        var group = new ChatGroupDto(Guid.NewGuid(), name, CallerUserId, _timeProvider.GetUtcNow(), "Admin", members);
        Groups.Add(group);
        return group;
    }

    /// <summary>Somebody joining a group that already exists - what stale fan-out is judged against.</summary>
    public void AddMember(Guid groupId, Guid userId)
    {
        var index = Groups.FindIndex(group => group.Id == groupId);
        var joined = new ChatGroupMemberDto(userId, "Member", _timeProvider.GetUtcNow());
        Groups[index] = Groups[index] with { Members = [.. Groups[index].Members, joined] };
    }

    /// <summary>
    /// Runs the moment a group message reaches the server, before it is validated - the one way to stage
    /// a membership change that lands between the app reading the member list and posting to it.
    /// </summary>
    public Action? WhenAGroupMessageArrives { get; set; }

    /// <summary>A message arriving from the other side, as a poll would find it.</summary>
    public ChatMessageDto AddIncoming(Guid senderUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64)
    {
        var message = new ChatMessageDto(
            Guid.NewGuid(), senderUserId, recipientUserId, ciphertextBase64, nonceBase64,
            _timeProvider.GetUtcNow(), false, null);

        _messages.Add(message);
        return message;
    }

    /// <summary>One copy of a group message written by somebody else, addressed to one member.</summary>
    public ChatMessageDto AddIncomingGroupCopy(
        Guid groupId, Guid groupMessageId, Guid senderUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64)
    {
        var message = new ChatMessageDto(
            Guid.NewGuid(), senderUserId, recipientUserId, ciphertextBase64, nonceBase64,
            _timeProvider.GetUtcNow(), false, null, groupMessageId);

        _groupMessages.Add(new GroupMessage(groupId, message));
        return message;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsUnreachable)
        {
            throw new HttpRequestException("No such host is known.");
        }

        if (RefuseEverythingWith is { } refusal)
        {
            return new HttpResponseMessage(refusal);
        }

        var segments = request.RequestUri!.AbsolutePath.Trim('/').Split('/');
        if (segments.Length > 2 && segments[2] == "groups")
        {
            return await HandleGroupsAsync(request, segments, cancellationToken);
        }

        if (segments[^1] == "contacts")
        {
            return Json(Contacts);
        }

        if (segments[^1] == "approve")
        {
            var otherUserId = Guid.Parse(segments[^2]);
            if (Contacts.All(contact => contact.UserId != otherUserId))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            ApprovedConversations.Add(otherUserId);
            RefuseSendsWith = null;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (segments.Length == 5 && segments[2] == "messages" && segments[4] is "read" or "read-receipt")
        {
            var otherUserId = Guid.Parse(segments[3]);
            return segments[4] == "read" ? MarkAsRead(otherUserId) : Json(new ReadReceiptDto(ReadUpToUtcFor(otherUserId)));
        }

        if (segments.Length == 4 && segments[2] == "messages" && Guid.TryParse(segments[3], out var messageId))
        {
            if (request.Method == HttpMethod.Delete)
            {
                return Remove(messageId);
            }

            if (request.Method == HttpMethod.Put)
            {
                return await RewriteAsync(messageId, request, cancellationToken);
            }
        }

        if (request.Method == HttpMethod.Post)
        {
            return await AcceptAsync(request, cancellationToken);
        }

        var since = HttpUtility.ParseQueryString(request.RequestUri.Query)["sinceUtc"];
        var messages = since is null
            ? _messages
            : _messages.Where(message => message.SentAtUtc > DateTimeOffset.Parse(since)).ToList();

        return Json(messages.ToList());
    }

    private async Task<HttpResponseMessage> HandleGroupsAsync(
        HttpRequestMessage request, string[] segments, CancellationToken cancellationToken)
    {
        // api/chat/groups
        if (segments.Length == 3)
        {
            return request.Method == HttpMethod.Post
                ? await CreateGroupAsync(request, cancellationToken)
                : Json(Groups);
        }

        var groupId = Guid.Parse(segments[3]);
        if (segments.Length >= 5 && segments[4] == "members")
        {
            return await ChangeMembershipAsync(request, segments, groupId, cancellationToken);
        }

        // api/chat/groups/{id}/messages
        var group = Groups.FirstOrDefault(candidate => candidate.Id == groupId);
        if (group is null || group.Members.All(member => member.UserId != CallerUserId))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        // api/chat/groups/{id}/messages/{groupMessageId}/receipts
        if (segments.Length == 7 && segments[6] == "receipts")
        {
            return Json(ReadReceiptsFor(Guid.Parse(segments[5])));
        }

        if (request.Method == HttpMethod.Put)
        {
            // api/chat/groups/{id}/messages/{groupMessageId} - the same fan-out as sending, over the top.
            return await AcceptGroupMessageAsync(request, group, cancellationToken, Guid.Parse(segments[5]));
        }

        return request.Method == HttpMethod.Post
            ? await AcceptGroupMessageAsync(request, group, cancellationToken)
            : Json(ReadGroupConversation(groupId));
    }

    /// <summary>
    /// The membership rules, as ChatGroup enforces them: only an admin may change anything, adding
    /// somebody you have no conversation with is refused, and the last admin can be neither removed nor
    /// demoted. A fake that waved these through would let a screen offering the impossible pass its
    /// tests - which is exactly what these tests are for.
    /// </summary>
    private async Task<HttpResponseMessage> ChangeMembershipAsync(
        HttpRequestMessage request, string[] segments, Guid groupId, CancellationToken cancellationToken)
    {
        var index = Groups.FindIndex(candidate => candidate.Id == groupId);
        if (index < 0)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var group = Groups[index];
        if (group.Members.All(member => member.UserId != CallerUserId))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        if (group.Members.Single(member => member.UserId == CallerUserId).Role != "Admin")
        {
            return Refused("Only an admin can change who is in a group.");
        }

        if (request.Method == HttpMethod.Post)
        {
            var added = JsonSerializer.Deserialize<AddChatGroupMemberRequest>(
                await request.Content!.ReadAsStringAsync(cancellationToken), _json)!;

            if (group.Members.Any(member => member.UserId == added.UserId))
            {
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            if (Contacts.All(contact => contact.UserId != added.UserId))
            {
                return Refused("You can only add people you already have a chat with.");
            }

            AddMember(groupId, added.UserId);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        var subjectUserId = Guid.Parse(segments[5]);
        var subject = group.Members.FirstOrDefault(member => member.UserId == subjectUserId);
        if (subject is null)
        {
            return Refused("That person isn't in this group.");
        }

        var adminCount = group.Members.Count(member => member.Role == "Admin");

        if (request.Method == HttpMethod.Delete)
        {
            if (subject.Role == "Admin" && adminCount == 1)
            {
                return Refused("A group needs at least one admin - promote someone else first.");
            }

            Groups[index] = group with { Members = [.. group.Members.Where(member => member.UserId != subjectUserId)] };
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        var role = JsonSerializer.Deserialize<ChangeChatGroupMemberRoleRequest>(
            await request.Content!.ReadAsStringAsync(cancellationToken), _json)!.Role;

        if (subject.Role == "Admin" && role != "Admin" && adminCount == 1)
        {
            return Refused("A group needs at least one admin - promote someone else first.");
        }

        Groups[index] = group with
        {
            Members = [.. group.Members.Select(member => member.UserId == subjectUserId ? member with { Role = role } : member)],
            OwnRole = subjectUserId == CallerUserId ? role : group.OwnRole
        };

        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    /// <summary>A refusal the caller is entitled to hear about - see InvalidRequestExceptionHandler.</summary>
    private static HttpResponseMessage Refused(string message)
        => new(HttpStatusCode.BadRequest) { Content = JsonContent.Create(new { message }) };

    private async Task<HttpResponseMessage> CreateGroupAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var created = JsonSerializer.Deserialize<CreateChatGroupRequest>(
            await request.Content!.ReadAsStringAsync(cancellationToken), _json)!;

        var group = AddGroup(created.Name, [.. created.MemberUserIds]);
        return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(group.Id) };
    }

    /// <summary>
    /// The rule the whole design rests on: exactly one copy per current member other than the sender.
    /// A missing copy would cut somebody out of a conversation they are in, and an extra one would
    /// deliver into a group the recipient has no part in - so both are 400, as the real handler does.
    /// </summary>
    private async Task<HttpResponseMessage> AcceptGroupMessageAsync(
        HttpRequestMessage request, ChatGroupDto group, CancellationToken cancellationToken, Guid? replacing = null)
    {
        var sent = JsonSerializer.Deserialize<SendGroupMessageRequest>(
            await request.Content!.ReadAsStringAsync(cancellationToken), _json)!;

        WhenAGroupMessageArrives?.Invoke();
        group = Groups.First(candidate => candidate.Id == group.Id);

        var expected = group.Members.Select(member => member.UserId).Where(userId => userId != CallerUserId).ToHashSet();
        if (!expected.SetEquals(sent.Copies.Select(copy => copy.RecipientUserId)))
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        }

        if (replacing is { } edited)
        {
            // Every copy of the posting is replaced, so nobody is left reading the words it had before.
            if (_groupMessages.RemoveAll(stored => stored.Message.GroupMessageId == edited) == 0)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }
        }

        var groupMessageId = replacing ?? Guid.NewGuid();
        foreach (var copy in sent.Copies)
        {
            AddIncomingGroupCopy(
                group.Id, groupMessageId, CallerUserId, copy.RecipientUserId, copy.CiphertextBase64, copy.NonceBase64);
        }

        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// One row per message the caller can read, not one per stored copy - the same collapse the real
    /// query does, without which the sender sees their own message once per recipient.
    /// </summary>
    private List<ChatMessageDto> ReadGroupConversation(Guid groupId)
        => _groupMessages
            .Where(stored => stored.GroupId == groupId)
            .Select(stored => stored.Message)
            .Where(message => message.SenderUserId == CallerUserId || message.RecipientUserId == CallerUserId)
            .GroupBy(message => message.GroupMessageId ?? message.Id)
            .Select(copies => copies.OrderBy(copy => copy.Id).First())
            .OrderBy(message => message.SentAtUtc)
            .ToList();

    private async Task<HttpResponseMessage> AcceptAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (RefuseSendsWith is { } refusal)
        {
            return new HttpResponseMessage(refusal);
        }

        var sent = JsonSerializer.Deserialize<SendMessageRequest>(
            await request.Content!.ReadAsStringAsync(cancellationToken), _json)!;

        var message = new ChatMessageDto(
            Guid.NewGuid(), CallerUserId, sent.RecipientUserId, sent.CiphertextBase64, sent.NonceBase64,
            _timeProvider.GetUtcNow(), false, null);

        _messages.Add(message);
        return Json(message);
    }

    /// <summary>
    /// Everything the other party sent the caller counts as read from now on - reading is per
    /// conversation, stamped at the moment somebody looks, exactly as MarkConversationAsReadCommandHandler
    /// does it.
    /// </summary>
    private HttpResponseMessage MarkAsRead(Guid otherUserId)
    {
        for (var index = 0; index < _messages.Count; index++)
        {
            if (_messages[index] is { } message
                && message.SenderUserId == otherUserId && message.RecipientUserId == CallerUserId)
            {
                _readMessageIds.Add(message.Id);
            }
        }

        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// How far they have read: the send-time of the newest message of the caller's that has been marked,
    /// or null when none has. One timestamp for the conversation, not a flag per message.
    /// </summary>
    private DateTimeOffset? ReadUpToUtcFor(Guid otherUserId)
    {
        var read = _messages
            .Where(message => message.SenderUserId == CallerUserId
                && message.RecipientUserId == otherUserId
                && _readMessageIds.Contains(message.Id))
            .Select(message => message.SentAtUtc)
            .ToList();

        return read.Count == 0 ? null : read.Max();
    }

    /// <summary>What the other party's own client would see as their read receipt - the mirror image.</summary>
    public DateTimeOffset? ReadUpToUtcForTheOtherParty(Guid callersUserId)
    {
        var read = _messages
            .Where(message => message.RecipientUserId == callersUserId && _readMessageIds.Contains(message.Id))
            .Select(message => message.SentAtUtc)
            .ToList();

        return read.Count == 0 ? null : read.Max();
    }

    /// <summary>Marks what the other party has read, as the recipient's own client would.</summary>
    public void TheOtherPartyReadEverything(Guid otherUserId)
    {
        foreach (var message in _messages.Where(message => message.RecipientUserId == otherUserId))
        {
            _readMessageIds.Add(message.Id);
        }
    }

    /// <summary>
    /// Deletes for everyone, and a group copy takes every copy of the same posting with it - what the
    /// real handler does. Only the sender may; a recipient asking is refused.
    /// </summary>
    private HttpResponseMessage Remove(Guid messageId)
    {
        if (_messages.FirstOrDefault(message => message.Id == messageId) is { } oneToOne)
        {
            if (oneToOne.SenderUserId != CallerUserId)
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            }

            _messages.Remove(oneToOne);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (_groupMessages.FirstOrDefault(stored => stored.Message.Id == messageId) is not { } copy)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        if (copy.Message.SenderUserId != CallerUserId)
        {
            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        }

        _groupMessages.RemoveAll(stored => stored.Message.GroupMessageId == copy.Message.GroupMessageId);
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private async Task<HttpResponseMessage> RewriteAsync(
        Guid messageId, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var index = _messages.FindIndex(message => message.Id == messageId);
        if (index < 0)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        if (_messages[index].SenderUserId != CallerUserId)
        {
            return new HttpResponseMessage(HttpStatusCode.Forbidden);
        }

        var edit = JsonSerializer.Deserialize<EditMessageRequest>(
            await request.Content!.ReadAsStringAsync(cancellationToken), _json)!;

        _messages[index] = _messages[index] with
        {
            CiphertextBase64 = edit.CiphertextBase64,
            NonceBase64 = edit.NonceBase64,
            IsEdited = true,
            EditedAtUtc = _timeProvider.GetUtcNow()
        };

        return Json(_messages[index]);
    }

    private static HttpResponseMessage Json<T>(T payload)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(payload) };

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };

    /// <summary>Which group a stored copy belongs to - ChatMessageDto doesn't carry it.</summary>
    private sealed record GroupMessage(Guid GroupId, ChatMessageDto Message);
}
