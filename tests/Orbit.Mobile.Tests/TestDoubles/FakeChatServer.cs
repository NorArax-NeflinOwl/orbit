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
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public FakeChatServer(TimeProvider timeProvider) => _timeProvider = timeProvider;

    public List<ContactDto> Contacts { get; } = [];

    public List<ChatGroupDto> Groups { get; } = [];

    /// <summary>Every message the server accepted, in the order it accepted them.</summary>
    public IReadOnlyList<ChatMessageDto> Messages => _messages;

    /// <summary>Every copy of every group message, which is what the fan-out is judged by.</summary>
    public IReadOnlyList<ChatMessageDto> GroupMessageCopies => _groupMessages.Select(stored => stored.Message).ToList();

    /// <summary>Whose token the requests are carrying, which the real server reads from the claim.</summary>
    public Guid CallerUserId { get; set; }

    public bool IsUnreachable { get; set; }

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

        // api/chat/groups/{id}/messages
        var groupId = Guid.Parse(segments[3]);
        var group = Groups.FirstOrDefault(candidate => candidate.Id == groupId);
        if (group is null || group.Members.All(member => member.UserId != CallerUserId))
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        return request.Method == HttpMethod.Post
            ? await AcceptGroupMessageAsync(request, group, cancellationToken)
            : Json(ReadGroupConversation(groupId));
    }

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
        HttpRequestMessage request, ChatGroupDto group, CancellationToken cancellationToken)
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

        var groupMessageId = Guid.NewGuid();
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

    private static HttpResponseMessage Json<T>(T payload)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(payload) };

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };

    /// <summary>Which group a stored copy belongs to - ChatMessageDto doesn't carry it.</summary>
    private sealed record GroupMessage(Guid GroupId, ChatMessageDto Message);
}
