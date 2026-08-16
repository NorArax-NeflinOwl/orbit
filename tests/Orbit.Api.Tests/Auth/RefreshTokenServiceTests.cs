using Orbit.Api.Auth;
using Orbit.Api.Tests.TestDoubles;
using Xunit;

namespace Orbit.Api.Tests.Auth;

public sealed class RefreshTokenServiceTests
{
    [Fact]
    public async Task IssueAsync_stores_only_the_hash_of_the_returned_token()
    {
        var repository = new InMemoryRefreshTokenRepository();
        var service = new RefreshTokenService(repository);
        var userId = Guid.NewGuid();

        var rawToken = await service.IssueAsync(userId, CancellationToken.None);

        Assert.Single(repository.All);
        Assert.NotEqual(rawToken, repository.All[0].TokenHash);
        Assert.Equal(userId, repository.All[0].UserId);
    }

    [Fact]
    public async Task RedeemAsync_returns_the_owning_user_and_a_new_token_for_a_valid_refresh_token()
    {
        var repository = new InMemoryRefreshTokenRepository();
        var service = new RefreshTokenService(repository);
        var userId = Guid.NewGuid();
        var rawToken = await service.IssueAsync(userId, CancellationToken.None);

        var redemption = await service.RedeemAsync(rawToken, CancellationToken.None);

        Assert.NotNull(redemption);
        Assert.Equal(userId, redemption!.UserId);
        Assert.NotEqual(rawToken, redemption.RefreshToken);
    }

    [Fact]
    public async Task RedeemAsync_revokes_the_redeemed_token_so_it_cannot_be_used_again()
    {
        var repository = new InMemoryRefreshTokenRepository();
        var service = new RefreshTokenService(repository);
        var rawToken = await service.IssueAsync(Guid.NewGuid(), CancellationToken.None);
        await service.RedeemAsync(rawToken, CancellationToken.None);

        var secondRedemption = await service.RedeemAsync(rawToken, CancellationToken.None);

        Assert.Null(secondRedemption);
    }

    [Fact]
    public async Task RedeemAsync_returns_null_for_a_token_that_was_never_issued()
    {
        var service = new RefreshTokenService(new InMemoryRefreshTokenRepository());

        var redemption = await service.RedeemAsync("not-a-real-token", CancellationToken.None);

        Assert.Null(redemption);
    }

    [Fact]
    public async Task RevokeAsync_makes_a_previously_valid_token_unredeemable()
    {
        var repository = new InMemoryRefreshTokenRepository();
        var service = new RefreshTokenService(repository);
        var rawToken = await service.IssueAsync(Guid.NewGuid(), CancellationToken.None);

        await service.RevokeAsync(rawToken, CancellationToken.None);

        Assert.Null(await service.RedeemAsync(rawToken, CancellationToken.None));
    }

    [Fact]
    public async Task RevokeAsync_does_nothing_for_a_token_that_was_never_issued()
    {
        var service = new RefreshTokenService(new InMemoryRefreshTokenRepository());

        // Should not throw - logout is best-effort, and a stale or unknown token is not an error.
        await service.RevokeAsync("not-a-real-token", CancellationToken.None);
    }
}
