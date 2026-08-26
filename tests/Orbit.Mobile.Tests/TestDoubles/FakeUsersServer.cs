using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Users;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Looking an account up by id. Group chat needs it and one-to-one chat did not: a group can hold people
/// the sender has no contact row for, and their public key has to come from somewhere.
/// </summary>
internal sealed class FakeUsersServer : HttpMessageHandler
{
    private readonly Dictionary<Guid, UserSearchResultDto> _users = [];

    public bool IsUnreachable { get; set; }

    /// <summary>How many lookups have been served, so a test can pin down what a sync costs.</summary>
    public int LookupCount { get; private set; }

    public void Add(Guid userId, string displayName, string? publicKeyBase64)
        => _users[userId] = new UserSearchResultDto(userId, displayName.ToLowerInvariant(), displayName, publicKeyBase64);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsUnreachable)
        {
            throw new HttpRequestException("No such host is known.");
        }

        LookupCount++;
        var userId = Guid.Parse(request.RequestUri!.Segments[^1]);
        return Task.FromResult(_users.TryGetValue(userId, out var user)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(user) }
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
