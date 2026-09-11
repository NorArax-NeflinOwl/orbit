using System.Net;
using System.Net.Http.Json;
using System.Web;
using Orbit.Contracts.Config;
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

    /// <summary>
    /// The password the deletion endpoint accepts, for an account that has one. Whether it is asked for
    /// at all is <see cref="AccountDto.HasPassword"/> on <see cref="Account"/>, as it is on the server: an
    /// account with none - a Google-only one - is let through unchecked, and one with a password is
    /// refused anything but this, including when this was never set.
    /// </summary>
    public string? DeletionPassword { get; set; }

    /// <summary>Whether the account was actually deleted, so a refusal can be told from a deletion.</summary>
    public bool AccountDeleted { get; private set; }

    /// <summary>How many deletions were asked for, so a test can say nothing was sent at all.</summary>
    public int DeletionRequests { get; private set; }

    /// <summary>
    /// What GET /users/me answers with. An unverified account with no Google behind it by default, which
    /// is the state that hides the Google extras - see GoogleIntegrationAccess.
    /// </summary>
    public AccountDto Account { get; set; } = new(
        Guid.NewGuid(), "me@orbit.example", "me", "Me",
        IsEmailVerified: false, HasPassword: true, IsGoogleLinked: false);

    /// <summary>
    /// Every account gone, for the case a lookup has to answer "nobody" - which is also how the server
    /// answers for somebody who has made themselves unfindable.
    /// </summary>
    public void ForgetEverybody() => _users.Clear();

    /// <summary>
    /// An account this server knows. The login is derived from the name unless a test says otherwise -
    /// most do not care, and the ones that do are about the difference between the two.
    /// </summary>
    public void Add(Guid userId, string displayName, string? publicKeyBase64, string? userName = null)
        => _users[userId] = new UserSearchResultDto(
            userId, userName ?? displayName.ToLowerInvariant(), displayName, publicKeyBase64);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsUnreachable)
        {
            throw new HttpRequestException("No such host is known.");
        }

        if (request.Method == HttpMethod.Get
            && request.RequestUri!.AbsolutePath.EndsWith("/users/me", StringComparison.Ordinal))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Account)
            });
        }

        if (request.Method == HttpMethod.Delete
            && request.RequestUri!.AbsolutePath.EndsWith("/users/me", StringComparison.Ordinal))
        {
            return DeleteAccountAsync(request, cancellationToken);
        }

        // Asked by the account screen's Google row, which is absent unless a client id comes back -
        // see GoogleAccountLink. GoogleAccountLinkTests is where the offered case is exercised.
        if (request.RequestUri!.AbsolutePath.EndsWith("/config/client-flags", StringComparison.Ordinal))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new ClientFlagsDto(
                    ExceptionDetailsAllowed: false, GoogleClientId: string.Empty, WebAddress: string.Empty,
                    GoogleAndroidClientId: string.Empty, GoogleIosClientId: string.Empty))
            });
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
    /// Mirrors DeleteAccountCommandHandler: an account with a password has to prove it, one without -
    /// signed in with Google and never given one - does not.
    /// </summary>
    private async Task<HttpResponseMessage> DeleteAccountAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        DeletionRequests++;
        var body = await request.Content!.ReadFromJsonAsync<DeleteAccountRequest>(cancellationToken);
        if (Account.HasPassword && (DeletionPassword is null || body?.Password != DeletionPassword))
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        AccountDeleted = true;
        return new HttpResponseMessage(HttpStatusCode.NoContent);
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
