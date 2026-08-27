using Orbit.Core.Permissions;
using Xunit;

namespace Orbit.Api.Tests.Permissions;

public sealed class PermissionPrerequisiteTests
{
    [Theory]
    [InlineData(ApplicationPermission.GroupChat)]
    [InlineData(ApplicationPermission.Sharing)]
    public void What_rests_on_chat_does_nothing_without_it(ApplicationPermission dependent)
    {
        // Held but not effective: the row exists, and the gate still refuses. Checked on every read
        // rather than only when a code is redeemed, so taking chat away stops these there and then.
        IReadOnlySet<ApplicationPermission> granted = new HashSet<ApplicationPermission> { dependent };

        Assert.False(dependent.IsEffective(granted));
        Assert.Empty(PermissionPrerequisites.Effective(granted));
    }

    [Theory]
    [InlineData(ApplicationPermission.GroupChat)]
    [InlineData(ApplicationPermission.Sharing)]
    public void What_rests_on_chat_works_with_it(ApplicationPermission dependent)
    {
        IReadOnlySet<ApplicationPermission> granted = new HashSet<ApplicationPermission> { ApplicationPermission.Chat, dependent };

        Assert.True(dependent.IsEffective(granted));
        Assert.Equal([ApplicationPermission.Chat, dependent], PermissionPrerequisites.Effective(granted));
    }

    [Theory]
    [InlineData(ApplicationPermission.Chat)]
    [InlineData(ApplicationPermission.Location)]
    public void The_ones_that_stand_alone_need_nothing_first(ApplicationPermission independent)
    {
        Assert.Null(independent.RequiredBefore());
        Assert.True(independent.IsEffective(new HashSet<ApplicationPermission> { independent }));
    }
}
