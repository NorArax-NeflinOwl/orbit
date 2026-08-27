using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Permissions;
using Xunit;

namespace Orbit.Api.Tests.Permissions;

/// <summary>
/// The codes are rows, made once and kept. What matters is that starting again never changes one, and
/// that a typed code finds exactly the permission it belongs to.
/// </summary>
public sealed class PermissionCodeStoreTests
{
    private static PermissionCodeStore AStore(out InMemoryPermissionCodeRepository repository)
    {
        repository = new InMemoryPermissionCodeRepository();
        return new PermissionCodeStore(repository);
    }

    [Fact]
    public async Task Every_permission_gets_a_code()
    {
        var store = AStore(out _);

        var codes = await store.EnsureEveryPermissionHasOneAsync(CancellationToken.None);

        Assert.Equal(Enum.GetValues<ApplicationPermission>().Length, codes.Count);
        Assert.Equal(codes.Count, codes.Select(code => code.Code).Distinct().Count());
    }

    [Fact]
    public async Task Starting_again_leaves_every_code_alone()
    {
        var store = AStore(out _);
        var first = await store.EnsureEveryPermissionHasOneAsync(CancellationToken.None);

        var second = await store.EnsureEveryPermissionHasOneAsync(CancellationToken.None);

        // A code that changed under whoever was told it would be worse than no code at all.
        Assert.Equal(
            first.OrderBy(code => code.Permission).Select(code => (code.Permission, code.Code)),
            second.OrderBy(code => code.Permission).Select(code => (code.Permission, code.Code)));
    }

    [Fact]
    public async Task A_permission_added_later_gets_one_without_disturbing_the_rest()
    {
        var repository = new InMemoryPermissionCodeRepository();
        var store = new PermissionCodeStore(repository);
        await repository.AddIfAbsentAsync(
            new PermissionCode(ApplicationPermission.Contacts, "KEPTKEPTKEPT", DateTimeOffset.UtcNow), CancellationToken.None);

        var codes = await store.EnsureEveryPermissionHasOneAsync(CancellationToken.None);

        Assert.Equal("KEPTKEPTKEPT", codes.Single(code => code.Permission == ApplicationPermission.Contacts).Code);
        Assert.Equal(Enum.GetValues<ApplicationPermission>().Length, codes.Count);
    }

    [Fact]
    public async Task A_code_finds_the_permission_it_belongs_to()
    {
        var store = AStore(out _);
        var codes = await store.EnsureEveryPermissionHasOneAsync(CancellationToken.None);

        foreach (var code in codes)
        {
            Assert.Equal(code.Permission, await store.MatchAsync(code.Code, CancellationToken.None));
        }
    }

    [Fact]
    public async Task A_code_read_off_a_screen_and_typed_back_still_works()
    {
        var store = AStore(out _);
        var codes = await store.EnsureEveryPermissionHasOneAsync(CancellationToken.None);
        var chat = codes.Single(code => code.Permission == ApplicationPermission.Chat);

        // Case and stray spacing are the typist's, not a different code.
        Assert.Equal(ApplicationPermission.Chat, await store.MatchAsync($"  {chat.Code.ToLowerInvariant()} ", CancellationToken.None));
    }

    [Fact]
    public async Task Anything_else_finds_nothing()
    {
        var store = AStore(out _);
        await store.EnsureEveryPermissionHasOneAsync(CancellationToken.None);

        Assert.Null(await store.MatchAsync("NOTACODEATALL", CancellationToken.None));
        Assert.Null(await store.MatchAsync(string.Empty, CancellationToken.None));
        Assert.Null(await store.MatchAsync(null, CancellationToken.None));
    }

    [Fact]
    public void A_code_can_be_read_aloud_without_ambiguity()
    {
        var code = PermissionCode.Mint(ApplicationPermission.Chat, DateTimeOffset.UtcNow).Code;

        // No I/L/O/U, so nothing in a code can be confused with 1, 0 or V while copying it.
        Assert.Equal(12, code.Length);
        Assert.DoesNotContain(code, character => "ILOU".Contains(character));
    }
}
