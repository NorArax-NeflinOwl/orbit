using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Crypto;

/// <summary>
/// Getting this device a chat key. The failures here are unusually expensive: a key replaced by mistake
/// takes every message ever sent to that user with it, on every device, with no way back - the server
/// has never held anything that could decrypt them.
///
/// So the rule these pin down is narrow and absolute: <b>a key is only ever generated when the server has
/// said the account has no backup.</b> Orbit.Web is more relaxed - it treats a failed lookup as "none" so
/// a browser is never locked out - which is defensible there and is not here, where losing the network
/// mid-sign-in is ordinary rather than rare.
/// </summary>
public sealed class OwnEncryptionKeyProviderTests
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task With_no_backup_on_the_server_a_key_is_created_and_published()
    {
        var context = new KeyContext();

        var outcome = await context.Provider.UnlockOrCreateAsync(Password);

        Assert.Equal(EncryptionKeyOutcome.Created, outcome);
        Assert.True(context.Storage.HoldsAKeyFor(context.UserId));
        Assert.NotNull(context.Server.StoredBackup);
    }

    [Fact]
    public async Task A_backup_this_password_opens_is_restored_rather_than_replaced()
    {
        var context = new KeyContext();
        using var original = ChatIdentity.Create();
        context.Server.StoredBackup = original.WrapWithPassword(Password);

        var outcome = await context.Provider.UnlockOrCreateAsync(Password);

        Assert.Equal(EncryptionKeyOutcome.Unlocked, outcome);
        Assert.Equal(original.PublicKeyBase64, await context.LocalPublicKeyAsync());
    }

    [Fact]
    public async Task A_lookup_that_could_not_be_made_leaves_the_key_locked_rather_than_replacing_it()
    {
        var context = new KeyContext();
        using var original = ChatIdentity.Create();
        context.Server.StoredBackup = original.WrapWithPassword(Password);
        context.Server.IsUnreachable = true;

        var outcome = await context.Provider.UnlockOrCreateAsync(Password);

        // The account's real key still exists. Generating here - which is what treating "couldn't ask"
        // as "there is none" amounts to - would publish a new public key and orphan it permanently.
        Assert.Equal(EncryptionKeyOutcome.StillLocked, outcome);
        Assert.False(context.Storage.HoldsAKeyFor(context.UserId));
    }

    [Fact]
    public async Task A_server_error_is_not_mistaken_for_an_account_with_no_backup_either()
    {
        var context = new KeyContext();
        context.Server.ForcedFailure = System.Net.HttpStatusCode.InternalServerError;

        Assert.Equal(EncryptionKeyOutcome.StillLocked, await context.Provider.UnlockOrCreateAsync(Password));
        Assert.False(context.Storage.HoldsAKeyFor(context.UserId));
    }

    [Fact]
    public async Task A_backup_wrapped_under_an_older_password_leaves_the_key_locked()
    {
        var context = new KeyContext();
        using var original = ChatIdentity.Create();
        context.Server.StoredBackup = original.WrapWithPassword("the password it was really wrapped under");

        var outcome = await context.Provider.UnlockOrCreateAsync(Password);

        // The key inside that backup is still the account's real one. Replacing it because this password
        // does not happen to open it would destroy the only copy.
        Assert.Equal(EncryptionKeyOutcome.StillLocked, outcome);
        Assert.Null(context.Storage.Peek(context.UserId));
    }

    [Fact]
    public async Task A_device_that_already_holds_the_key_keeps_it_and_gets_a_fresh_backup()
    {
        var context = new KeyContext();
        using var existing = ChatIdentity.Create();
        await context.Storage.WritePrivateKeyJwkAsync(context.UserId, existing.ExportPrivateKeyJwk());

        var outcome = await context.Provider.UnlockOrCreateAsync(Password);

        Assert.Equal(EncryptionKeyOutcome.Unlocked, outcome);
        Assert.Equal(existing.PublicKeyBase64, await context.LocalPublicKeyAsync());
        // Backed up opportunistically, so a device that predates the backup feature ends up covered.
        Assert.NotNull(ChatIdentity.FromBackup(context.Server.StoredBackup!, Password));
    }

    [Fact]
    public async Task Changing_the_password_re_wraps_the_backup_under_the_new_one()
    {
        var context = new KeyContext();
        using var original = ChatIdentity.Create();
        context.Server.StoredBackup = original.WrapWithPassword(Password);
        await context.Storage.WritePrivateKeyJwkAsync(context.UserId, original.ExportPrivateKeyJwk());

        await context.Provider.RewrapAsync(Password, "a brand new password");

        // The whole point: without this the backup stays readable only with the old password, so the next
        // device fails to restore it, makes a fresh key, and loses every earlier message.
        using var restored = ChatIdentity.FromBackup(context.Server.StoredBackup!, "a brand new password");
        Assert.Equal(original.PublicKeyBase64, restored!.PublicKeyBase64);
    }

    [Fact]
    public async Task Changing_the_password_on_a_device_without_the_key_restores_it_first()
    {
        var context = new KeyContext();
        using var original = ChatIdentity.Create();
        context.Server.StoredBackup = original.WrapWithPassword(Password);

        await context.Provider.RewrapAsync(Password, "a brand new password");

        // Covers changing the password from a phone that has never held the key: the current password
        // opens the old backup, and the same key is re-wrapped rather than replaced.
        using var restored = ChatIdentity.FromBackup(context.Server.StoredBackup!, "a brand new password");
        Assert.Equal(original.PublicKeyBase64, restored!.PublicKeyBase64);
    }

    [Fact]
    public async Task Re_wrapping_without_being_able_to_get_the_key_says_so_instead_of_publishing_a_new_one()
    {
        var context = new KeyContext();
        using var original = ChatIdentity.Create();
        var untouchedBackup = original.WrapWithPassword(Password);
        context.Server.StoredBackup = untouchedBackup;
        context.Server.IsUnreachable = true;

        var outcome = await context.Provider.RewrapAsync(Password, "a brand new password");

        Assert.Equal(EncryptionKeyOutcome.StillLocked, outcome);
        // And the backup it could not open is left exactly as it was.
        Assert.Same(untouchedBackup, context.Server.StoredBackup);
    }

    [Fact]
    public async Task A_password_reset_replaces_the_key_the_user_can_no_longer_reach()
    {
        var context = new KeyContext();
        using var unreachable = ChatIdentity.Create();
        context.Server.StoredBackup = unreachable.WrapWithPassword("the password they have forgotten");

        var outcome = await context.Provider.ReplaceAfterPasswordResetAsync("the new password");

        // The one path allowed to discard a key the account still has, because the user chose it: after a
        // reset nobody can ever open that backup again, so refusing would lock chat permanently rather
        // than starting it over.
        Assert.Equal(EncryptionKeyOutcome.Created, outcome);
        Assert.NotEqual(unreachable.PublicKeyBase64, await context.LocalPublicKeyAsync());
        Assert.NotNull(ChatIdentity.FromBackup(context.Server.StoredBackup!, "the new password"));
    }

    [Fact]
    public async Task Only_the_reset_path_replaces_a_key_a_password_could_not_open()
    {
        var context = new KeyContext();
        using var original = ChatIdentity.Create();
        context.Server.StoredBackup = original.WrapWithPassword("an older password");

        // Same starting position as the reset test above; the difference is entirely that nobody asked.
        Assert.Equal(EncryptionKeyOutcome.StillLocked, await context.Provider.UnlockOrCreateAsync("today's password"));
        Assert.NotNull(ChatIdentity.FromBackup(context.Server.StoredBackup!, "an older password"));
    }

    [Fact]
    public async Task Opening_a_key_this_device_does_not_have_is_a_locked_state_not_a_new_key()
    {
        var context = new KeyContext();

        await Assert.ThrowsAsync<EncryptionKeyLockedException>(() => context.Provider.OpenAsync());
        Assert.False(context.Storage.HoldsAKeyFor(context.UserId));
    }

    [Fact]
    public async Task A_backup_that_could_not_be_published_still_leaves_a_working_key_on_the_device()
    {
        var context = new KeyContext();
        using var existing = ChatIdentity.Create();
        await context.Storage.WritePrivateKeyJwkAsync(context.UserId, existing.ExportPrivateKeyJwk());
        context.Server.IsUnreachable = true;

        var outcome = await context.Provider.UnlockOrCreateAsync(Password);

        // Publishing is best-effort and retried at the next sign-in; the key already works here.
        Assert.Equal(EncryptionKeyOutcome.Unlocked, outcome);
        Assert.Equal(existing.PublicKeyBase64, await context.LocalPublicKeyAsync());
    }

    private sealed class KeyContext
    {
        public KeyContext()
        {
            UserId = Guid.NewGuid();
            var session = new UserSession("access", "refresh", UserId, "user@orbit.example", "A User");
            Provider = new OwnEncryptionKeyProvider(
                Storage, new EncryptionKeyClient(Server.ToHttpClient()),
                new SessionStore(new InMemorySessionStorage(session)),
                NullLogger<OwnEncryptionKeyProvider>.Instance);
        }

        public Guid UserId { get; }
        public InMemoryChatKeyStorage Storage { get; } = new();
        public FakeEncryptionKeyServer Server { get; } = new();
        public OwnEncryptionKeyProvider Provider { get; }

        public async Task<string> LocalPublicKeyAsync()
        {
            using var identity = ChatIdentity.FromPrivateKeyJwk((await Storage.ReadPrivateKeyJwkAsync(UserId))!);
            return identity.PublicKeyBase64;
        }
    }
}
