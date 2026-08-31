using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Orbit.Contracts.Users;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Orbit's location endpoints, in memory. Stores shared positions as the ciphertext it is given and can
/// read none of them, which is what the real server does.
/// </summary>
internal sealed class FakeLocationServer : HttpMessageHandler
{
    private readonly TimeProvider _timeProvider;
    private readonly List<SharedLocationDto> _shares = [];
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public FakeLocationServer(TimeProvider timeProvider) => _timeProvider = timeProvider;

    public Guid CallerUserId { get; set; }

    public bool IsUnreachable { get; set; }

    /// <summary>Set to make every request come back refused, which is not the same as unreachable.</summary>
    public HttpStatusCode? RefuseEverythingWith { get; set; }

    /// <summary>
    /// Set to refuse only the two reads about sharing, leaving the caller's own position alone. This is
    /// the shape the real server has for an account that unlocked Location but not Contacts: recording
    /// where you are is allowed, asking who is sharing with whom is not (see PermissionPolicies).
    /// </summary>
    public HttpStatusCode? RefuseShareReadsWith { get; set; }

    /// <summary>The caller's own recorded position - stored in the clear, as the real one does.</summary>
    public SaveOwnLocationRequest? OwnLocation { get; private set; }

    /// <summary>Every share the server holds, whoever it belongs to.</summary>
    public IReadOnlyList<SharedLocationDto> Shares => _shares;

    /// <summary>A position somebody else is sharing with the caller, already sealed for them.</summary>
    public void AddIncomingShare(Guid sharerUserId, string ciphertextBase64, string nonceBase64, bool isContinuous = false)
        => _shares.Add(new SharedLocationDto(
            sharerUserId, CallerUserId, ciphertextBase64, nonceBase64, isContinuous, _timeProvider.GetUtcNow()));

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

        var path = request.RequestUri!.AbsolutePath;

        if (RefuseShareReadsWith is { } shareRefusal && request.Method == HttpMethod.Get
            && (path.EndsWith("/location/shared-with-me", StringComparison.Ordinal)
                || path.EndsWith("/location/shares", StringComparison.Ordinal)))
        {
            return new HttpResponseMessage(shareRefusal);
        }

        if (path.EndsWith("/location/shared-with-me", StringComparison.Ordinal))
        {
            return Json(_shares.Where(share => share.RecipientUserId == CallerUserId).ToList());
        }

        if (path.EndsWith("/location/shares", StringComparison.Ordinal))
        {
            if (request.Method == HttpMethod.Get)
            {
                return Json(_shares.Where(share => share.SharerUserId == CallerUserId).ToList());
            }

            if (request.Method == HttpMethod.Delete)
            {
                _shares.RemoveAll(share => share.SharerUserId == CallerUserId);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            var shared = JsonSerializer.Deserialize<ShareLocationRequest>(
                await request.Content!.ReadAsStringAsync(cancellationToken), _json)!;

            // Replaces whatever that recipient had before, exactly as ShareLocationCommand does.
            _shares.RemoveAll(share => share.SharerUserId == CallerUserId && share.RecipientUserId == shared.RecipientUserId);
            _shares.Add(new SharedLocationDto(
                CallerUserId, shared.RecipientUserId, shared.CiphertextBase64, shared.NonceBase64,
                shared.IsContinuous, _timeProvider.GetUtcNow()));

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (path.Contains("/location/shares/", StringComparison.Ordinal))
        {
            var recipientUserId = Guid.Parse(path.Split('/')[^1]);
            _shares.RemoveAll(share => share.SharerUserId == CallerUserId && share.RecipientUserId == recipientUserId);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        if (path.EndsWith("/location", StringComparison.Ordinal))
        {
            OwnLocation = request.Method == HttpMethod.Delete
                ? null
                : JsonSerializer.Deserialize<SaveOwnLocationRequest>(
                    await request.Content!.ReadAsStringAsync(cancellationToken), _json);

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json<T>(T payload)
        => new(HttpStatusCode.OK) { Content = JsonContent.Create(payload) };

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
