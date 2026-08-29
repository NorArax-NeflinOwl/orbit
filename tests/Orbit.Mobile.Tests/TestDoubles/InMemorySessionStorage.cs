using Orbit.Mobile.Authentication;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>What the Keychain holds, without one.</summary>
internal sealed class InMemorySessionStorage : ISessionStorage
{
    public InMemorySessionStorage(UserSession? stored = null) => Stored = stored;

    public UserSession? Stored { get; private set; }

    /// <summary>How often the store went to the platform - the thing SessionStore exists to keep down.</summary>
    public int ReadCount { get; private set; }

    public Task<UserSession?> ReadAsync()
    {
        ReadCount++;
        return Task.FromResult(Stored);
    }

    public Task WriteAsync(UserSession session)
    {
        Stored = session;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Stored = null;
        return Task.CompletedTask;
    }
}
