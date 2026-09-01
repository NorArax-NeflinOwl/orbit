using Orbit.Contracts.Users;

namespace Orbit.Web.Services;

/// <summary>
/// Answers whether the signed-in account may use the Google extras - the calendar and maps links that
/// hand something off to Google (see GoogleCalendarEventLink, GoogleMapsLink).
///
/// Offered to an account that has either confirmed its email address or connected Google, because both
/// mean the same thing here: somebody stood behind the account rather than typing an address nobody has
/// ever read. Scoped and cached, so the pages that ask don't each fetch the account again.
///
/// Whoever is at this browser has the last word: qualifying only says the links may be offered, and
/// DevicePreferences.AllowGoogleExtras says whether they are wanted.
/// </summary>
public sealed class GoogleIntegrationAccess
{
    private readonly UsersApiClient _usersApiClient;
    private readonly DevicePreferences _devicePreferences;
    private readonly ILogger<GoogleIntegrationAccess> _logger;
    private bool? _isAvailable;

    public GoogleIntegrationAccess(
        UsersApiClient usersApiClient, DevicePreferences devicePreferences, ILogger<GoogleIntegrationAccess> logger)
    {
        _usersApiClient = usersApiClient;
        _devicePreferences = devicePreferences;
        _logger = logger;
    }

    /// <summary>
    /// False when this browser has turned the extras off, false when the account qualifies for neither
    /// route, and false when the account can't be read at all - a page that can't tell should offer
    /// nothing rather than offer something that may not work.
    /// </summary>
    public async ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        // Asked first and never cached: it is a switch somebody can flip while the app is open, and the
        // pages read this again when they next load.
        if (!_devicePreferences.AllowGoogleExtras)
        {
            return false;
        }

        if (_isAvailable is { } cached)
        {
            return cached;
        }

        try
        {
            var account = await _usersApiClient.GetAccountAsync(cancellationToken);
            _isAvailable = Qualifies(account);
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Could not read the account to decide on the Google extras");
            _isAvailable = false;
        }

        return _isAvailable.Value;
    }

    /// <summary>Lets a page that already holds the account answer without a second call.</summary>
    public bool Qualifies(AccountDto? account)
        => account is not null && (account.IsEmailVerified || account.IsGoogleLinked);

    /// <summary>
    /// Forgets the cached answer, so verifying an address or connecting Google takes effect without a
    /// reload - Options calls this after either.
    /// </summary>
    public void Forget() => _isAvailable = null;
}
