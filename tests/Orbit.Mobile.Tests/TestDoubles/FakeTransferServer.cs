using System.Net;
using System.Net.Http.Json;
using Orbit.Core.Transfer;
using Orbit.Core.Transfer.ImportArchive;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// The account's whole archive, out and back. Records what an import was handed, so a test can check
/// the file was read rather than only that the request went.
/// </summary>
internal sealed class FakeTransferServer : HttpMessageHandler
{
    public bool IsUnreachable { get; set; }

    /// <summary>Refuses to build one, as the server would if the export could not be assembled.</summary>
    public bool RefusesToExport { get; set; }

    /// <summary>What an export answers with.</summary>
    public OrbitArchive Archive { get; set; } = new(
        OrbitArchive.CurrentVersion, DateTimeOffset.UtcNow, [], [], [], []);

    /// <summary>The archive the last import was given, or null if none has arrived.</summary>
    public OrbitArchive? Imported { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (IsUnreachable)
        {
            throw new HttpRequestException("No such host is known.");
        }

        if (request.Method == HttpMethod.Get)
        {
            return RefusesToExport
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Archive) };
        }

        Imported = await request.Content!.ReadFromJsonAsync<OrbitArchive>(cancellationToken);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new ImportArchiveResult(
                Imported!.Notes.Count, Imported.TaskLists.Count, Imported.CalendarEvents.Count,
                Imported.Warehouses.Count))
        };
    }

    public HttpClient ToHttpClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("https://orbit.example/") };
}
