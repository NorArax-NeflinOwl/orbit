using Orbit.Core.Users;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="IRefreshTokenRepository"/> stub for unit tests that need real add/lookup/update
/// behavior without spinning up SQLite.
/// </summary>
internal sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly List<RefreshToken> _refreshTokens = [];

    public IReadOnlyList<RefreshToken> All => _refreshTokens;

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        => Task.FromResult(_refreshTokens.FirstOrDefault(refreshToken => refreshToken.TokenHash == tokenHash));

    public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        _refreshTokens.Add(refreshToken);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        var index = _refreshTokens.FindIndex(existing => existing.Id == refreshToken.Id);
        if (index >= 0)
        {
            _refreshTokens[index] = refreshToken;
        }

        return Task.CompletedTask;
    }
}
