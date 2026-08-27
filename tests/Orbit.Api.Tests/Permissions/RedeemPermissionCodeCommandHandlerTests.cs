using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Permissions;
using Orbit.Core.Permissions.RedeemPermissionCode;
using Xunit;

namespace Orbit.Api.Tests.Permissions;

public sealed class RedeemPermissionCodeCommandHandlerTests
{
    private static readonly PermissionCodeAuthority Authority = new("a-deployment-secret-that-only-the-server-has");

    [Fact]
    public async Task A_valid_code_grants_the_permission_it_belongs_to()
    {
        var repository = new InMemoryUserPermissionRepository();
        var handler = new RedeemPermissionCodeCommandHandler(repository, Authority);
        var userId = Guid.NewGuid();

        var granted = await handler.HandleAsync(
            new RedeemPermissionCodeCommand(userId, Authority.CodeFor(ApplicationPermission.GroupChat)), CancellationToken.None);

        Assert.Equal(ApplicationPermission.GroupChat, granted);
        Assert.Equal([ApplicationPermission.GroupChat], await repository.GetForUserAsync(userId, CancellationToken.None));
    }

    [Fact]
    public async Task A_code_grants_only_its_own_permission()
    {
        var repository = new InMemoryUserPermissionRepository();
        var handler = new RedeemPermissionCodeCommandHandler(repository, Authority);
        var userId = Guid.NewGuid();

        await handler.HandleAsync(
            new RedeemPermissionCodeCommand(userId, Authority.CodeFor(ApplicationPermission.Chat)), CancellationToken.None);

        // One-to-one chat and group chat are unlocked separately, which is the whole reason there are
        // four codes rather than one.
        var held = await repository.GetForUserAsync(userId, CancellationToken.None);
        Assert.DoesNotContain(ApplicationPermission.GroupChat, held);
    }

    [Fact]
    public async Task A_code_that_matches_nothing_grants_nothing()
    {
        var repository = new InMemoryUserPermissionRepository();
        var handler = new RedeemPermissionCodeCommandHandler(repository, Authority);
        var userId = Guid.NewGuid();

        var granted = await handler.HandleAsync(new RedeemPermissionCodeCommand(userId, "NOPENOPENOPE"), CancellationToken.None);

        Assert.Null(granted);
        Assert.Empty(await repository.GetForUserAsync(userId, CancellationToken.None));
    }

    [Fact]
    public async Task Redeeming_the_same_code_twice_is_not_an_error()
    {
        var repository = new InMemoryUserPermissionRepository();
        var handler = new RedeemPermissionCodeCommandHandler(repository, Authority);
        var userId = Guid.NewGuid();
        var code = Authority.CodeFor(ApplicationPermission.Sharing);

        await handler.HandleAsync(new RedeemPermissionCodeCommand(userId, code), CancellationToken.None);
        var granted = await handler.HandleAsync(new RedeemPermissionCodeCommand(userId, code), CancellationToken.None);

        // The person typed a valid code and can use that part of Orbit, which is all they asked about.
        Assert.Equal(ApplicationPermission.Sharing, granted);
        Assert.Single(await repository.GetForUserAsync(userId, CancellationToken.None));
    }
}
