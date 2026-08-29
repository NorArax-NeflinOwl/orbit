using Orbit.Contracts.Users;
using Orbit.Mobile.Authentication;

namespace Orbit.Mobile.Google;

/// <summary>
/// Answers whether the signed-in account may use the Google extras - the calendar and maps links that
/// hand something off to Google (see <see cref="GoogleCalendarEventLink"/>, <see cref="GoogleMapsLink"/>).
///
/// Offered to an account that has either confirmed its email address or connected Google, because both
/// mean the same thing here: somebody stood behind the account rather than typing an address nobody has
/// ever read. The same rule Orbit.Web applies, so a reader sees the same extras on both.
///
/// One instance for the app, holding the answer once it has one: the calendar and the map both ask, and
/// neither should cost another round trip.
/// </summary>
public sealed class GoogleIntegrationAccess
{
    private readonly AccountClient _accountClient;
    private bool? _isAvailable;

    public GoogleIntegrationAccess(AccountClient accountClient) => _accountClient = accountClient;

    /// <summary>
    /// False when the account qualifies for neither route, and false when the account cannot be read at
    /// all - offline, or the request failed. A screen that cannot tell should offer nothing rather than
    /// offer something that may not work.
    ///
    /// A "no" from being offline is not remembered, so the extras appear once the phone is back rather
    /// than staying hidden until the app is restarted.
    /// </summary>
    public async ValueTask<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (_isAvailable is { } cached)
        {
            return cached;
        }

        try
        {
            _isAvailable = Qualifies(await _accountClient.GetAccountAsync(cancellationToken));
        }
        catch (HttpRequestException)
        {
            return false;
        }

        return _isAvailable.Value;
    }

    /// <summary>Lets a screen that already holds the account answer without a second call.</summary>
    public static bool Qualifies(AccountDto? account)
        => account is not null && (account.IsEmailVerified || account.IsGoogleLinked);

    /// <summary>
    /// Forgets the cached answer, so verifying an address takes effect without restarting the app - the
    /// account screen calls this after a confirmation.
    /// </summary>
    public void Forget() => _isAvailable = null;
}
