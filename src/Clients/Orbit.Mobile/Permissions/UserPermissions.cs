using Microsoft.EntityFrameworkCore;
using Orbit.Core.Permissions;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Permissions;

/// <summary>
/// What this account has been unlocked for, in one place so the navigation bar, the screens behind it
/// and the account screen agree about it. The mobile counterpart of Orbit.Web's UserPermissionState,
/// and presentation only for the same reason: hiding what the API would refuse saves a pointless tap,
/// and the refusal itself is the server's (see PermissionPolicies in Orbit.Api).
///
/// The one real difference from the web is offline. The web waits for a fresh answer before deciding
/// anything and can, because it is never without one; a phone that did the same would hide chat and the
/// map on every cold start with no signal. So the last answer is kept in the local database and used
/// until the server gives another - and until any answer has arrived at all, nothing is hidden, because
/// hiding what somebody is entitled to is the worse of the two mistakes.
/// </summary>
public sealed class UserPermissions
{
    private readonly UsersClient _usersClient;
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;

    private HashSet<ApplicationPermission>? _granted;

    /// <summary>
    /// The in-flight or finished first read, so every screen asking at once shares one request rather
    /// than each making its own - and so a screen that loads before another has finished still decides
    /// against the same answer.
    /// </summary>
    private Task? _firstRead;

    public UserPermissions(UsersClient usersClient, IDbContextFactory<OrbitLocalDbContext> dbContextFactory)
    {
        _usersClient = usersClient;
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>Raised after what is held changes, so whatever is on screen can be shown again.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Whether this account may use something. True while nothing is known yet - see the class comment
    /// for why that direction, and note it costs nothing: the server still refuses.
    /// </summary>
    public bool Has(ApplicationPermission permission)
        => _granted is not { } granted || permission.IsEffective(granted);

    /// <summary>True once an answer - from the server or from the last launch - is actually held.</summary>
    public bool IsKnown => _granted is not null;

    /// <summary>
    /// Exactly what is held, for the account screen's list. Not the same question as <see cref="Has"/>:
    /// that one answers "may this be used", which also depends on the prerequisite, while the list has
    /// to show a permission that is held but not yet effective as held.
    /// </summary>
    public IReadOnlySet<ApplicationPermission> Granted => _granted ?? [];

    /// <summary>
    /// What the phone remembers, then one shared read from the server. Every screen that gates on a
    /// permission awaits this before it decides anything.
    /// </summary>
    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
        => _firstRead ??= LoadAsync(cancellationToken);

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        await LoadStoredAsync(cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    /// <summary>Reads what the phone remembers, so a screen can decide before any request finishes.</summary>
    public async Task LoadStoredAsync(CancellationToken cancellationToken = default)
    {
        if (_granted is not null)
        {
            return;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var stored = await dbContext.Permissions.AsNoTracking().ToListAsync(cancellationToken);
        if (stored.Count == 0)
        {
            return;
        }

        Show(Parse(stored.Select(permission => permission.Name)));
    }

    /// <summary>
    /// Asks the server. Leaves what is held in place when the call fails: a dropped request is not
    /// evidence that somebody lost a permission, and emptying the navigation bar would say it was.
    /// </summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> granted;
        try
        {
            granted = await _usersClient.GetPermissionsAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return;
        }

        await StoreAsync(granted, cancellationToken);
        Show(Parse(granted));
        _firstRead ??= Task.CompletedTask;
    }

    /// <summary>Forgets everything, for a phone that has stopped being this account's.</summary>
    public void Forget()
    {
        _granted = null;
        _firstRead = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Show(HashSet<ApplicationPermission> granted)
    {
        _granted = granted;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static HashSet<ApplicationPermission> Parse(IEnumerable<string> names)
        => [.. names
            .Select(name => Enum.TryParse<ApplicationPermission>(name, out var permission) ? permission : (ApplicationPermission?)null)
            .Where(permission => permission is not null)
            .Select(permission => permission!.Value)];

    /// <summary>
    /// Replaced whole rather than merged: the server owns this list, so a permission missing from its
    /// answer is one this account no longer has.
    /// </summary>
    private async Task StoreAsync(IReadOnlyList<string> granted, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.Permissions.ExecuteDeleteAsync(cancellationToken);
        dbContext.Permissions.AddRange(granted.Select(name => new LocalPermission { Name = name }));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
