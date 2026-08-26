using Orbit.Mobile.Crypto;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>What the Keychain holds, without one.</summary>
internal sealed class InMemoryChatKeyStorage : IChatKeyStorage
{
    private readonly Dictionary<Guid, string> _keys = [];

    public Task<string?> ReadPrivateKeyJwkAsync(Guid userId)
        => Task.FromResult(_keys.TryGetValue(userId, out var jwk) ? jwk : null);

    public Task WritePrivateKeyJwkAsync(Guid userId, string privateKeyJwk)
    {
        _keys[userId] = privateKeyJwk;
        return Task.CompletedTask;
    }

    public string? Peek(Guid userId) => _keys.GetValueOrDefault(userId);

    public bool HoldsAKeyFor(Guid userId) => _keys.ContainsKey(userId);
}
