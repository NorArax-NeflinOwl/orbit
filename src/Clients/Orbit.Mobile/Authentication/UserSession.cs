using Orbit.Contracts.Users;

namespace Orbit.Mobile.Authentication;

/// <summary>
/// Everything the app holds about the signed-in user. One object rather than four loose values because
/// they are always written, read and cleared together - a half-cleared session, where the identity
/// outlives the tokens, would show a signed-in user who cannot make a single request.
/// </summary>
public sealed record UserSession(
    string AccessToken, string RefreshToken, Guid UserId, string Email, string DisplayName)
{
    public static UserSession FromAuthResponse(AuthResponse response)
        => new(response.Token, response.RefreshToken, response.UserId, response.Email, response.DisplayName);
}

/// <summary>
/// Where the session is kept between launches. The implementation must be backed by the platform's
/// secure store - Keychain on iOS, Keystore on Android - and never by Preferences, which is readable on
/// a rooted or jailbroken device: a refresh token is good for thirty days.
/// </summary>
public interface ISessionStorage
{
    Task<UserSession?> ReadAsync();

    Task WriteAsync(UserSession session);

    Task ClearAsync();
}
