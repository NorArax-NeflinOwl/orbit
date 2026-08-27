using System.Net;
using System.Net.Http.Json;
using System.Web;
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

    /// <summary>What this account has been unlocked for - see UserPermissions on the phone's side.</summary>
    public List<string> Granted { get; } = [];

    /// <summary>What a redeemed code answers with. Null means the code matched nothing.</summary>
    public RedeemPermissionCodeResultDto? RedeemResult { get; set; }

    public void Add(Guid userId, string displayName, string? publicKeyBase64)
        => _users[userId] = new UserSearchResultDto(userId, displayName.ToLowerInvariant(), displayName, publicKeyBase64);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsUnreachable)
        {
            throw new HttpRequestException("No such host is known.");
        }

        if (request.RequestUri!.AbsolutePath.EndsWith("/search", StringComparison.Ordinal))
        {
            return Task.FromResult(Search(HttpUtility.ParseQueryString(request.RequestUri.Query)["query"]!));
        }

        if (request.RequestUri.AbsolutePath.EndsWith("/permissions/redeem", StringComparison.Ordinal))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(RedeemResult ?? new RedeemPermissionCodeResultDto(Granted: null))
            });
        }

        if (request.RequestUri.AbsolutePath.EndsWith("/permissions", StringComparison.Ordinal))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new UserPermissionsDto(Granted))
            });
        }

        LookupCount++;
        var userId = Guid.Parse(request.RequestUri!.Segments[^1]);
        return Task.FromResult(_users.TryGetValue(userId, out var user)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(user) }
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    /// <summary>
    /// Exact match on the username or the email address, and never the searcher themselves - the rule
    /// SearchUserQueryHandler enforces so the search cannot be used to enumerate accounts. A fake that
    /// matched loosely would let a client that leaked that ability pass its tests.
    /// </summary>
    private HttpResponseMessage Search(string identifier)
    {
        var wanted = identifier.Trim().ToLowerInvariant();
        var found = _users.Values.FirstOrDefault(user =>
            user.Id != SearcherUserId
            && (user.UserName.Equals(wanted, StringComparison.OrdinalIgnoreCase)
                || EmailFor(user).Equals(wanted, StringComparison.OrdinalIgnoreCase)));

        return found is null
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(found) };
    }

    /// <summary>Whoever is searching, so the server can refuse to hand them back themselves.</summary>
    public Guid SearcherUserId { get; set; }

    private static string EmailFor(UserSearchResultDto user) => $"{user.UserName}@orbit.example";

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
