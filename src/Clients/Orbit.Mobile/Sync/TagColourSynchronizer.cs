using Microsoft.Extensions.Logging;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Sync;

/// <summary>
/// Brings this phone's tag colours and the server's into step - see LocalTagColourRepository. Colours set
/// here go first, so a colour picked offline reaches the server rather than being overwritten by the older
/// one it replaced; then the server's colours come back over everything that is no longer waiting.
///
/// No change feed and no need for one, for the reason FoldersClient gives about folders: an account has a
/// handful of coloured tags, and asking for all of them costs less than a cursor would.
/// </summary>
public sealed class TagColourSynchronizer
{
    private readonly LocalTagColourRepository _colours;
    private readonly TagsClient _tagsClient;
    private readonly ILogger<TagColourSynchronizer> _logger;

    public TagColourSynchronizer(
        LocalTagColourRepository colours, TagsClient tagsClient, ILogger<TagColourSynchronizer> logger)
    {
        _colours = colours;
        _tagsClient = tagsClient;
        _logger = logger;
    }

    /// <summary>Throws what the client throws when the server cannot be reached - see EverythingSynchronizer.TryAsync.</summary>
    public async Task<SyncResult> SynchroniseAsync(CancellationToken cancellationToken = default)
    {
        var sent = 0;
        var givenUp = 0;
        foreach (var pending in await _colours.PendingAsync(cancellationToken))
        {
            var outcome = await _tagsClient.SetColourAsync(pending.Tag, pending.Colour, cancellationToken);
            if (outcome is WriteOutcome.Applied)
            {
                await _colours.SettleAsync(pending, cancellationToken);
                sent++;
                continue;
            }

            _logger.LogWarning("The server refused the colour of tag {Tag}: {Outcome}", pending.Tag, outcome);
            await _colours.DropAsync(pending, cancellationToken);
            givenUp++;
        }

        var colours = await _tagsClient.GetColoursAsync(cancellationToken);
        await _colours.ReplaceFromServerAsync(colours, cancellationToken);
        return new SyncResult(sent, colours.Count, 0, givenUp, ReachedTheServer: true);
    }
}
