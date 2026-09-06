using System.Text.Json;
using Orbit.Api.Instances;

namespace Orbit.Api.LiveUpdates;

/// <summary>
/// Takes an announcement made on another API instance and delivers it to the connections this one holds.
/// <see cref="PostgresLiveUpdateFanOut"/> is the half that sends.
/// </summary>
public sealed class LiveUpdateNoticeHandler(ILocalLiveUpdateFanOut local) : IInstanceNoticeHandler
{
    public string Channel => LiveUpdateAnnouncement.Channel;

    public async Task HandleAsync(string body, CancellationToken cancellationToken)
    {
        var announcement = JsonSerializer.Deserialize<LiveUpdateAnnouncement>(body);
        if (announcement is null)
        {
            return;
        }

        // The arguments cross as JSON and are handed to SignalR in that form, which serialises them to
        // the client - so what this instance sends is the JSON the announcing instance would have sent.
        await local.AnnounceAsync(
            announcement.Message,
            announcement.UserIds,
            [.. announcement.Arguments.Cast<object?>()],
            cancellationToken);
    }
}
