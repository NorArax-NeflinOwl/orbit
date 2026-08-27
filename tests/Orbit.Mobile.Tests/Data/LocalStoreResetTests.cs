using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Notes;
using Orbit.Mobile.Data;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Data;

/// <summary>
/// What happens to the phone's cache when it stops belonging to the person holding it.
///
/// Found by signing out and signing in as somebody else: the dashboard showed the previous account's
/// notes, and the server had none of them. Everything Orbit caches locally survived a sign-out -
/// including decrypted chat messages - so a handed-over phone read as the previous owner's.
/// </summary>
public sealed class LocalStoreResetTests : IDisposable
{
    private readonly LocalStore _localStore = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-27T09:00:00Z"));

    public void Dispose() => _localStore.Dispose();

    [Fact]
    public async Task Signing_out_leaves_nothing_of_the_previous_account_behind()
    {
        await AddANoteAsync();

        await new LocalStoreReset(_localStore).ClearForAsync(Guid.Empty);

        Assert.Empty(await Notes().GetAllAsync());
    }

    [Fact]
    public async Task Somebody_else_signing_in_on_the_same_phone_starts_empty()
    {
        // The case a sign-out cannot cover: a session that expired drops the reader at the sign-in
        // screen with no chance to clear anything.
        var reset = new LocalStoreReset(_localStore);
        await reset.ClearForAsync(Guid.NewGuid());
        await AddANoteAsync();

        await reset.ClearIfSomebodyElsesAsync(Guid.NewGuid());

        Assert.Empty(await Notes().GetAllAsync());
    }

    [Fact]
    public async Task The_same_account_signing_in_again_keeps_what_it_had()
    {
        // Otherwise every sign-in would throw away the offline cache the whole app is built on, and a
        // reader coming back with no connection would find nothing.
        var userId = Guid.NewGuid();
        var reset = new LocalStoreReset(_localStore);
        await reset.ClearForAsync(userId);
        await AddANoteAsync();

        await reset.ClearIfSomebodyElsesAsync(userId);

        Assert.Single(await Notes().GetAllAsync());
    }

    [Fact]
    public async Task Clearing_forgets_where_syncing_had_got_to()
    {
        // A cursor kept from somebody else's session asks "what changed since" a moment that has
        // nothing to do with this account, and quietly receives nothing.
        var reset = new LocalStoreReset(_localStore);
        using (var dbContext = _localStore.CreateDbContext())
        {
            dbContext.SyncCursors.Add(new SyncCursor { EntityType = "notes", Value = "2026-08-27T09:00:00Z" });
            dbContext.SaveChanges();
        }

        await reset.ClearForAsync(Guid.NewGuid());

        using var after = _localStore.CreateDbContext();
        Assert.Empty(after.SyncCursors);
    }

    private LocalNoteRepository Notes() => new(_localStore, _clock, FixedNetworkStatus.Online);

    private async Task AddANoteAsync()
        => await Notes().CreateAsync("Something private", [new NoteContentLineDto("Body", false, false)]);
}
