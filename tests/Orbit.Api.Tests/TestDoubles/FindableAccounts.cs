using Orbit.Core.Permissions;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// A <see cref="UserVisibility"/> in which the named accounts have unlocked Contacts, and everybody
/// else is invisible. Lets a test about finding people say who is findable without setting up the
/// permission model by hand each time.
/// </summary>
internal static class FindableAccounts
{
    public static UserVisibility Only(params Guid[] userIds)
    {
        var permissions = new InMemoryUserPermissionRepository();
        foreach (var userId in userIds)
        {
            permissions.GrantAsync(userId, ApplicationPermission.Contacts, CancellationToken.None).GetAwaiter().GetResult();
        }

        return new UserVisibility(permissions);
    }

    /// <summary>Nobody at all - every account is invisible.</summary>
    public static UserVisibility None() => Only();
}
