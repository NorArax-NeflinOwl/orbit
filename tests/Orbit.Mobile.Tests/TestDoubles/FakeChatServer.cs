using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Orbit.Contracts.Chat;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Orbit's one-to-one chat endpoints, in memory. Stores what it is given without ever being able to read
/// it, which is what the real server does.
/// </summary>
internal sealed class FakeChatServer : HttpMessageHandler
{
    private readonly TimeProvider _timeProvider;
    private readonly List<ChatMessageDto> _messages = [];

    public FakeChatServer(TimeProvider timeProvider) => _timeProvider = timeProvider;

    public List<ContactDto> Contacts { get; } = [];

    /// <summary>Every message the server accepted, in the order it accepted them.</summary>
    public IReadOnlyList<ChatMessageDto> Messages => _messages;

    public bool IsUnreachable { get; set; }

    /// <summary>Set to make the server refuse sends - 403 is "they haven't approved this conversation".</summary>
    public HttpStatusCode? RefuseSendsWith { get; set; }

    public ContactDto AddContact(Guid userId, string publicKeyBase64)
    {
        var contact = new ContactDto(
            userId, "someone", "Someone", "someone@orbit.example", publicKeyBase64,
            _timeProvider.GetUtcNow(), false, false);

        Contacts.Add(contact);
        return contact;
    }

    /// <summary>A message arriving from the other side, as a poll would find it.</summary>
    public ChatMessageDto AddIncoming(Guid senderUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64)
    {
        var message = new ChatMessageDto(
            Guid.NewGuid(), senderUserId, recipientUserId, ciphertextBase64, nonceBase64,
            _timeProvider.GetUtcNow(), false, null);

        _messages.Add(message);
        return message;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsUnreachable)
        {
            throw new HttpRequestException("No such host is known.");
        }

        var path = request.RequestUri!.AbsolutePath;

        if (path.EndsWith("/contacts", StringComparison.Ordinal))
        {
            return Json(Contacts);
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

    private async Task<HttpResponseMessage> AcceptAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (RefuseSendsWith is { } refusal)
        {
            return new HttpResponseMessage(refusal);
        }

        var sent = JsonSerializer.Deserialize<SendMessageRequest>(
            await request.Content!.ReadAsStringAsync(cancellationToken),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var message = new ChatMessageDto(
            Guid.NewGuid(), Guid.Empty, sent.RecipientUserId, sent.CiphertextBase64, sent.NonceBase64,
            _timeProvider.GetUtcNow(), false, null);

        _messages.Add(message);
        return Json(message);
    }

    private static HttpResponseMessage Json<T>(T payload)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(payload) };

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
