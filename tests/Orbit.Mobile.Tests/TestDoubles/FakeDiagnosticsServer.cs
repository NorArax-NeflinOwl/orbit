using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Orbit.Contracts.Diagnostics;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>Orbit's diagnostics endpoint, in memory - it keeps what it was sent so a test can read it back.</summary>
internal sealed class FakeDiagnosticsServer : HttpMessageHandler
{
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public bool IsUnreachable { get; set; }

    /// <summary>Set to make every request come back refused, which is not the same as unreachable.</summary>
    public HttpStatusCode? RefuseEverythingWith { get; set; }

    /// <summary>How many entries the server claims to have read. Zero is a real answer - see DiagnosticLogParser.</summary>
    public int? StoredEntryCount { get; set; }

    public List<UploadDiagnosticLogRequest> Uploads { get; } = [];

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

        var upload = (await request.Content!.ReadFromJsonAsync<UploadDiagnosticLogRequest>(_json, cancellationToken))!;
        Uploads.Add(upload);

        // Stands in for the real parser: one entry per line that starts one.
        var parsed = StoredEntryCount
            ?? upload.FileContent.Split('\n').Count(line => line.StartsWith('['));

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new UploadDiagnosticLogResponse(parsed), options: _json)
        };
    }

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
