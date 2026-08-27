namespace Orbit.Core.Permissions;

/// <summary>
/// The codes as they stand, and the one place that makes a missing one. Called at startup so a fresh
/// deployment has a full set, and again by redemption to match what somebody typed.
///
/// Stored rather than derived, so they can be read straight out of the database:
///
///     SELECT "Permission", "Code" FROM "PermissionCodes";
///
/// and so they survive a restart, a redeploy and a rotated secret - which a derived code does not.
/// </summary>
public sealed class PermissionCodeStore
{
    private readonly IPermissionCodeRepository _permissionCodeRepository;

    public PermissionCodeStore(IPermissionCodeRepository permissionCodeRepository)
    {
        _permissionCodeRepository = permissionCodeRepository;
    }

    /// <summary>
    /// Makes a code for every permission that has none, and leaves every existing one alone. Safe to
    /// call on every start: it is what fills in a permission added after the last deployment.
    /// </summary>
    public async Task<IReadOnlyList<PermissionCode>> EnsureEveryPermissionHasOneAsync(CancellationToken cancellationToken)
    {
        var existing = await _permissionCodeRepository.GetAllAsync(cancellationToken);
        var covered = existing.Select(code => code.Permission).ToHashSet();

        foreach (var permission in Enum.GetValues<ApplicationPermission>().Where(permission => !covered.Contains(permission)))
        {
            await _permissionCodeRepository.AddIfAbsentAsync(
                PermissionCode.Mint(permission, DateTimeOffset.UtcNow), cancellationToken);
        }

        return await _permissionCodeRepository.GetAllAsync(cancellationToken);
    }

    /// <summary>
    /// The permission the given code unlocks, or null if it unlocks nothing. Every candidate is compared
    /// even after one matches, so the time taken says nothing about which code a typed one was close to.
    /// </summary>
    public async Task<ApplicationPermission?> MatchAsync(string? typed, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return null;
        }

        var normalized = PermissionCode.Normalize(typed);
        ApplicationPermission? matched = null;
        foreach (var stored in await _permissionCodeRepository.GetAllAsync(cancellationToken))
        {
            if (FixedTimeEquals(normalized, stored.Code))
            {
                matched = stored.Permission;
            }
        }

        return matched;
    }

    /// <summary>Compares without letting the number of matching characters show in how long it took.</summary>
    private static bool FixedTimeEquals(string left, string right)
        => System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(left), System.Text.Encoding.UTF8.GetBytes(right));
}
