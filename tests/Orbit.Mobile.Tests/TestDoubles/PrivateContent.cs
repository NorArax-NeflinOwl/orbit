using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Sealers for tests. Most of them hold nothing private, and a sealer with no key is exactly right for
/// those: it is never asked for one, because nothing in the store needs opening.
/// </summary>
internal static class PrivateContent
{
    /// <summary>A device that holds no key - what a repository under test gets unless the test is about private items.</summary>
    public static PrivateContentSealer WithoutAKey()
        => new(new InMemoryChatKeyStorage(), new SessionStore(new InMemorySessionStorage()));

    /// <summary>A device signed in as <paramref name="userId"/> and holding that account's key.</summary>
    public static PrivateContentSealer HoldingAKeyFor(Guid userId)
    {
        using var identity = ChatIdentity.Create();
        var storage = new InMemoryChatKeyStorage();
        storage.WritePrivateKeyJwkAsync(userId, identity.ExportPrivateKeyJwk()).GetAwaiter().GetResult();

        return new PrivateContentSealer(storage, SignedInAs(userId));
    }

    /// <summary>
    /// A device holding a key for somebody - which account is nobody's business here. What a repository
    /// under test needs when the thing being stored is sealed whether the test is about sealing or not,
    /// which is every place: see Orbit.Core.Places.Place.IsPrivate.
    /// </summary>
    public static PrivateContentSealer WithAKey() => HoldingAKeyFor(Guid.NewGuid());

    /// <summary>Signed in, but with no key on this device - the case a sealed note cannot be read in.</summary>
    public static PrivateContentSealer SignedInWithoutAKey(Guid userId)
        => new(new InMemoryChatKeyStorage(), SignedInAs(userId));

    private static SessionStore SignedInAs(Guid userId)
        => new(new InMemorySessionStorage(
            new UserSession("access", "refresh", userId, "user@orbit.example", "A User")));
}
