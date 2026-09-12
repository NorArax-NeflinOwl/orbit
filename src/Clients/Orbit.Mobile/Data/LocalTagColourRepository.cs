using Microsoft.EntityFrameworkCore;
using Orbit.Contracts.Tags;
using Orbit.Core.Tags;

namespace Orbit.Mobile.Data;

/// <summary>
/// The account's tag colours on this phone - see <see cref="LocalTagColour"/>. Set here first, the way every
/// other write on this phone is, and sent by TagColourSynchronizer; read here by every screen that draws a
/// tag. A colour belongs to the tag for the whole account, so there is one row per tag and no item in it.
/// </summary>
public sealed class LocalTagColourRepository
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;

    public LocalTagColourRepository(IDbContextFactory<OrbitLocalDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Every colour to draw with, by the tag's key (TagNames.KeyOf). A colour taken away and not sent yet is
    /// left out, as is anything that is not "#rrggbb" - it is handed to a brush, and only a colour is trusted
    /// there.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, string>> ColoursAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await dbContext.TagColours.AsNoTracking().ToListAsync(cancellationToken);
        return rows
            .Where(row => TagColour.IsAColour(row.Colour))
            .ToDictionary(row => row.NormalizedTag, row => row.Colour, StringComparer.Ordinal);
    }

    /// <summary>Every tag this account has given a colour, as it was written - offered as a tag to pick.</summary>
    public async Task<IReadOnlyList<string>> ColouredTagsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await dbContext.TagColours.AsNoTracking().ToListAsync(cancellationToken);
        return [.. rows.Where(row => TagColour.IsAColour(row.Colour)).Select(row => row.Tag)];
    }

    /// <summary>
    /// Gives the tag this colour, or takes it away with an empty one - on this phone now, and on the server
    /// at the next sync. Nothing to do for a tag with no name.
    /// </summary>
    public async Task SetAsync(string tag, string colour, CancellationToken cancellationToken = default)
    {
        var name = tag.Trim();
        if (name.Length == 0)
        {
            return;
        }

        var key = TagNames.KeyOf(name);
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.TagColours.FirstOrDefaultAsync(stored => stored.NormalizedTag == key, cancellationToken);
        if (row is null)
        {
            row = new LocalTagColour { NormalizedTag = key };
            dbContext.TagColours.Add(row);
        }

        row.Tag = name;
        row.Colour = colour.Trim().ToLowerInvariant();
        row.IsPending = true;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Every colour set here and not sent yet, in no order a reader relies on.</summary>
    public async Task<IReadOnlyList<LocalTagColour>> PendingAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.TagColours.AsNoTracking().Where(row => row.IsPending).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The server took this colour. Settled only if nothing changed it since it was sent: a colour picked
    /// again while the first was on its way is still waiting, and is sent next time.
    /// </summary>
    public Task SettleAsync(LocalTagColour sent, CancellationToken cancellationToken = default)
        => ForgetOrSettleAsync(sent, drop: false, cancellationToken);

    /// <summary>
    /// The server will never take this colour - it refused it. Dropped rather than kept waiting, where it
    /// would be sent and refused on every sync for ever; the next pull puts back whatever the server has.
    /// </summary>
    public Task DropAsync(LocalTagColour refused, CancellationToken cancellationToken = default)
        => ForgetOrSettleAsync(refused, drop: true, cancellationToken);

    /// <summary>
    /// What the server holds, over everything this phone is not still waiting to send: a colour set on
    /// another device arrives, one taken away there goes, and one picked here and not sent yet stays.
    /// </summary>
    public async Task ReplaceFromServerAsync(IReadOnlyList<TagColourDto> colours, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await dbContext.TagColours.ToDictionaryAsync(row => row.NormalizedTag, StringComparer.Ordinal, cancellationToken);
        var fromServer = colours
            .Where(colour => colour.Tag.Trim().Length > 0 && TagColour.IsAColour(colour.Colour))
            .GroupBy(colour => TagNames.KeyOf(colour.Tag), StringComparer.Ordinal)
            .ToDictionary(sameTag => sameTag.Key, sameTag => sameTag.Last(), StringComparer.Ordinal);

        foreach (var (key, row) in rows.Where(entry => !entry.Value.IsPending && !fromServer.ContainsKey(entry.Key)))
        {
            dbContext.TagColours.Remove(row);
        }

        foreach (var (key, colour) in fromServer)
        {
            if (rows.TryGetValue(key, out var row))
            {
                if (!row.IsPending)
                {
                    row.Tag = colour.Tag.Trim();
                    row.Colour = colour.Colour.ToLowerInvariant();
                }

                continue;
            }

            dbContext.TagColours.Add(new LocalTagColour
            {
                NormalizedTag = key,
                Tag = colour.Tag.Trim(),
                Colour = colour.Colour.ToLowerInvariant()
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ForgetOrSettleAsync(LocalTagColour sent, bool drop, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await dbContext.TagColours.FirstOrDefaultAsync(
            stored => stored.NormalizedTag == sent.NormalizedTag, cancellationToken);
        if (row is null || row.Colour != sent.Colour)
        {
            return;
        }

        if (drop || row.Colour.Length == 0)
        {
            dbContext.TagColours.Remove(row);
        }
        else
        {
            row.IsPending = false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
