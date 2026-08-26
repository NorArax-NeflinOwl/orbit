using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Orbit.Contracts.Users;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// The three encryption-key endpoints, in memory. The distinction that matters is between answering
/// "there is no backup" - which the API says with 204, deliberately, because having none is normal - and
/// not answering at all.
/// </summary>
internal sealed class FakeEncryptionKeyServer : HttpMessageHandler
{
    /// <summary>The backup the account currently has, if any.</summary>
    public WrappedPrivateKeyDto? StoredBackup { get; set; }

    public string? StoredPublicKeyBase64 { get; private set; }

    /// <summary>True while the server cannot be reached at all, as it is to a phone with no signal.</summary>
    public bool IsUnreachable { get; set; }

    /// <summary>Set to make the server answer badly rather than not at all.</summary>
    public HttpStatusCode? ForcedFailure { get; set; }

    public int PublishCount { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsUnreachable)
        {
            throw new HttpRequestException("No such host is known.");
        }

        if (ForcedFailure is { } failure)
        {
            return new HttpResponseMessage(failure);
        }

        if (request.Method == HttpMethod.Get)
        {
            return StoredBackup is null
                ? new HttpResponseMessage(HttpStatusCode.NoContent)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(StoredBackup) };
        }

        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        if (request.RequestUri!.AbsolutePath.EndsWith("public-key", StringComparison.Ordinal))
        {
            StoredPublicKeyBase64 = JsonSerializer.Deserialize<SetPublicKeyRequest>(body, options)!.PublicKeyBase64;
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        var published = JsonSerializer.Deserialize<SetEncryptionKeyRequest>(body, options)!;
        StoredPublicKeyBase64 = published.PublicKeyBase64;
        StoredBackup = published.WrappedPrivateKey;
        PublishCount++;
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
