using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Permissions;
using Orbit.Core.Permissions.RedeemPermissionCode;
using Xunit;

namespace Orbit.Api.Tests.Permissions;

public sealed class RedeemPermissionCodeCommandHandlerTests
{
    private static readonly PermissionCodeAuthority Authority = new("a-deployment-secret-that-only-the-server-has");

    /// <summary>Chat comes first for everything conversational, so most of these start from an account that has it.</summary>
    private static async Task<(InMemoryUserPermissionRepository Repository, RedeemPermissionCodeCommandHandler Handler, Guid UserId)>
        AnAccountWithChatAsync()
    {
        var repository = new InMemoryUserPermissionRepository();
        var userId = Guid.NewGuid();
        await repository.GrantAsync(userId, ApplicationPermission.Contacts, CancellationToken.None);
        return (repository, new RedeemPermissionCodeCommandHandler(repository, Authority), userId);
    }

    [Fact]
    public async Task A_valid_code_grants_the_permission_it_belongs_to()
    {
        var (repository, handler, userId) = await AnAccountWithChatAsync();

        var outcome = await handler.HandleAsync(
            new RedeemPermissionCodeCommand(userId, Authority.CodeFor(ApplicationPermission.Chat)), CancellationToken.None);

        Assert.Equal(ApplicationPermission.Chat, outcome.Granted);
        Assert.Contains(ApplicationPermission.Chat, await repository.GetForUserAsync(userId, CancellationToken.None));
    }

    [Fact]
    public async Task A_code_grants_only_its_own_permission()
    {
        var repository = new InMemoryUserPermissionRepository();
        var handler = new RedeemPermissionCodeCommandHandler(repository, Authority);
        var userId = Guid.NewGuid();

        await handler.HandleAsync(
            new RedeemPermissionCodeCommand(userId, Authority.CodeFor(ApplicationPermission.Contacts)), CancellationToken.None);

        // One-to-one chat and group chat are unlocked separately, which is the whole reason there are
        // four codes rather than one.
        var held = await repository.GetForUserAsync(userId, CancellationToken.None);
        Assert.DoesNotContain(ApplicationPermission.Chat, held);
    }

    [Fact]
    public async Task A_code_that_matches_nothing_grants_nothing()
    {
        var (repository, handler, userId) = await AnAccountWithChatAsync();

        var outcome = await handler.HandleAsync(new RedeemPermissionCodeCommand(userId, "NOPENOPENOPE"), CancellationToken.None);

        Assert.Null(outcome.Granted);
        Assert.Null(outcome.MissingPrerequisite);
        Assert.DoesNotContain(ApplicationPermission.Chat, await repository.GetForUserAsync(userId, CancellationToken.None));
    }

    [Fact]
    public async Task Redeeming_the_same_code_twice_is_not_an_error()
    {
        var (repository, handler, userId) = await AnAccountWithChatAsync();
        var code = Authority.CodeFor(ApplicationPermission.Sharing);

        await handler.HandleAsync(new RedeemPermissionCodeCommand(userId, code), CancellationToken.None);
        var outcome = await handler.HandleAsync(new RedeemPermissionCodeCommand(userId, code), CancellationToken.None);

        // The person typed a valid code and can use that part of Orbit, which is all they asked about.
        Assert.Equal(ApplicationPermission.Sharing, outcome.Granted);
        Assert.Equal(2, (await repository.GetForUserAsync(userId, CancellationToken.None)).Count);
    }

    [Theory]
    [InlineData(ApplicationPermission.Chat)]
    [InlineData(ApplicationPermission.Sharing)]
    public async Task What_rests_on_chat_is_refused_until_chat_is_unlocked(ApplicationPermission dependent)
    {
        var repository = new InMemoryUserPermissionRepository();
        var handler = new RedeemPermissionCodeCommandHandler(repository, Authority);
        var userId = Guid.NewGuid();

        var outcome = await handler.HandleAsync(
            new RedeemPermissionCodeCommand(userId, Authority.CodeFor(dependent)), CancellationToken.None);

        // Refused rather than stored and inert - a code that appeared to work and changed nothing would
        // be worse than being told what to unlock first.
        Assert.Null(outcome.Granted);
        Assert.Equal(ApplicationPermission.Contacts, outcome.MissingPrerequisite);
        Assert.Empty(await repository.GetForUserAsync(userId, CancellationToken.None));
    }

    [Fact]
    public async Task Chat_itself_needs_nothing_first()
    {
        var repository = new InMemoryUserPermissionRepository();
        var handler = new RedeemPermissionCodeCommandHandler(repository, Authority);

        var outcome = await handler.HandleAsync(
            new RedeemPermissionCodeCommand(Guid.NewGuid(), Authority.CodeFor(ApplicationPermission.Contacts)), CancellationToken.None);

        Assert.Equal(ApplicationPermission.Contacts, outcome.Granted);
    }

    [Fact]
    public async Task Location_stands_on_its_own()
    {
        var repository = new InMemoryUserPermissionRepository();
        var handler = new RedeemPermissionCodeCommandHandler(repository, Authority);

        // Where somebody is has nothing to do with whether they can talk to anyone.
        var outcome = await handler.HandleAsync(
            new RedeemPermissionCodeCommand(Guid.NewGuid(), Authority.CodeFor(ApplicationPermission.Location)), CancellationToken.None);

        Assert.Equal(ApplicationPermission.Location, outcome.Granted);
    }
}
