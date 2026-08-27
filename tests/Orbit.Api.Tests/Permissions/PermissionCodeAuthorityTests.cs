using Orbit.Core.Permissions;
using Xunit;

namespace Orbit.Api.Tests.Permissions;

public sealed class PermissionCodeAuthorityTests
{
    private const string Secret = "a-deployment-secret-that-only-the-server-has";

    [Fact]
    public void Every_permission_gets_its_own_code()
    {
        var authority = new PermissionCodeAuthority(Secret);

        var codes = Enum.GetValues<ApplicationPermission>().Select(authority.CodeFor).ToList();

        // The point of four codes is that handing somebody one hands them one thing.
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void A_code_unlocks_the_permission_it_was_made_for()
    {
        var authority = new PermissionCodeAuthority(Secret);

        foreach (var permission in Enum.GetValues<ApplicationPermission>())
        {
            Assert.Equal(permission, authority.Match(authority.CodeFor(permission)));
        }
    }

    [Fact]
    public void Anything_else_unlocks_nothing()
    {
        var authority = new PermissionCodeAuthority(Secret);

        Assert.Null(authority.Match("NOPENOPENOPE"));
        Assert.Null(authority.Match(string.Empty));
        Assert.Null(authority.Match(null));
    }

    [Fact]
    public void A_code_read_off_a_screen_and_typed_back_still_works()
    {
        var authority = new PermissionCodeAuthority(Secret);
        var code = authority.CodeFor(ApplicationPermission.Chat);

        // Case and stray spacing are the typist's, not a different code.
        Assert.Equal(ApplicationPermission.Chat, authority.Match($"  {code.ToLowerInvariant()} "));
    }

    [Fact]
    public void Rotating_the_secret_invalidates_every_code()
    {
        var before = new PermissionCodeAuthority(Secret);
        var after = new PermissionCodeAuthority("a-different-deployment-secret-entirely");

        foreach (var permission in Enum.GetValues<ApplicationPermission>())
        {
            Assert.Null(after.Match(before.CodeFor(permission)));
        }
    }

    [Fact]
    public void The_derivation_is_pinned_to_exact_values()
    {
        // The release workflow derives the same codes in shell (openssl + awk) to print them in its run
        // summary, because the codes have to be readable by whoever deployed Orbit and the secret they
        // come from never leaves the server. Two implementations of one algorithm drift silently, so
        // these values are the contract between them: if this test fails, .github/workflows/main_orbit.yml
        // is now printing codes that this server will refuse.
        var authority = new PermissionCodeAuthority("a-known-secret-for-checking-the-derivation");

        Assert.Equal("J6HJCF9VRCQT", authority.CodeFor(ApplicationPermission.Location));
        Assert.Equal("9C0B3Z0G3RTD", authority.CodeFor(ApplicationPermission.Chat));
        Assert.Equal("TMF60HJ602HC", authority.CodeFor(ApplicationPermission.GroupChat));
        Assert.Equal("ZTFTXQEBR0E8", authority.CodeFor(ApplicationPermission.Sharing));
    }

    [Fact]
    public void A_code_can_be_read_aloud_without_ambiguity()
    {
        var authority = new PermissionCodeAuthority(Secret);

        foreach (var permission in Enum.GetValues<ApplicationPermission>())
        {
            // No I/L/O/U, so nothing in a code can be confused with 1, 0 or V while copying it.
            Assert.DoesNotContain(authority.CodeFor(permission), character => "ILOU".Contains(character));
        }
    }
}
