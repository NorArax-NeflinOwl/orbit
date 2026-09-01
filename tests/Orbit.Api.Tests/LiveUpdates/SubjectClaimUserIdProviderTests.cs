using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Orbit.Api.LiveUpdates;
using Xunit;

namespace Orbit.Api.Tests.LiveUpdates;

/// <summary>
/// Which claim decides who a live connection belongs to.
///
/// Worth a test of its own because both halves of this are easy to get wrong and neither says so.
/// SignalR looks for ClaimTypes.NameIdentifier by default; Orbit's tokens carry the account in "sub"
/// and keep it there (Program.cs sets MapInboundClaims = false, so nothing rewrites it). Read the wrong
/// one and every announcement is addressed to nobody - which throws nothing, logs nothing, and leaves
/// the app quietly polling exactly as it did before.
/// </summary>
public sealed class SubjectClaimUserIdProviderTests
{
    [Fact]
    public void The_account_is_read_from_the_sub_claim()
    {
        var userId = Guid.NewGuid();

        var read = SubjectClaimUserIdProvider.UserIdFrom(
            APrincipalWith(new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())));

        Assert.Equal(userId.ToString(), read);
    }

    /// <summary>
    /// The claim SignalR would have used on its own. Orbit's tokens do not carry it, so finding an
    /// account through it would mean something other than this provider had answered.
    /// </summary>
    [Fact]
    public void A_name_identifier_claim_is_not_what_this_reads()
    {
        var read = SubjectClaimUserIdProvider.UserIdFrom(
            APrincipalWith(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())));

        Assert.Null(read);
    }

    /// <summary>An unauthenticated connection belongs to nobody, rather than to an empty string.</summary>
    [Fact]
    public void A_connection_with_no_identity_belongs_to_nobody()
        => Assert.Null(SubjectClaimUserIdProvider.UserIdFrom(null));

    private static ClaimsPrincipal APrincipalWith(Claim claim) => new(new ClaimsIdentity([claim]));
}
