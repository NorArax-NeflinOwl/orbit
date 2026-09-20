namespace Orbit.Contracts.Users;

/// <summary>The signed-in account as its owner sees it - unlike UserSearchResultDto, which is what *other* users may see.</summary>
public sealed record AccountDto(
    Guid Id,
    string Email,
    string UserName,
    string DisplayName,
    bool IsEmailVerified,
    /// <summary>False for a Google account that hasn't set one - it can sign in, but can't use chat yet.</summary>
    bool HasPassword,
    bool IsGoogleLinked,
    /// <summary>Null until the user records one - see UserLocationDto.</summary>
    UserLocationDto? Location = null,
    /// <summary>What this account chose to be: "Available" or "DoNotDisturb" - see Orbit.Core.Users.PresenceAvailability.</summary>
    string Availability = "Available",
    /// <summary>What everybody else currently sees: "Available", "Away", "DoNotDisturb" or "Offline" - see Orbit.Core.Users.PresenceStatus.</summary>
    string PresenceStatus = "Offline",
    /// <summary>
    /// Whether this account has asked that nothing about it reach anybody but Orbit - the footer's
    /// "Do not share my personal information". See Orbit.Core.Users.User.KeepsThirdPartiesOut.
    /// </summary>
    bool KeepsThirdPartiesOut = false,
    /// <summary>
    /// Whether this account has set a PIN for what is private - the only thing any client is ever told
    /// about it. See Orbit.Core.Users.User.PrivatePinHash.
    /// </summary>
    bool HasPrivatePin = false);

/// <summary>Changes what the caller chose to be - see Orbit.Core.Users.PresenceAvailability for the accepted names.</summary>
public sealed record SetAvailabilityRequest(string Availability);

/// <summary>Answers the footer's "Do not share my personal information" - see AccountDto.KeepsThirdPartiesOut.</summary>
public sealed record SetPrivacyChoiceRequest(bool KeepsThirdPartiesOut);

/// <summary>
/// A point a user recorded for themselves: coordinates, the address reverse geocoding resolved if it
/// managed to, and when it was taken. Orbit stores one per user and no history - see
/// Orbit.Core.Users.UserLocation.
/// </summary>
public sealed record UserLocationDto(string? Address, double Latitude, double Longitude, DateTimeOffset RecordedAtUtc);

/// <summary>Records where the caller is. Latitude/longitude come from the browser; Address is best-effort reverse geocoding.</summary>
public sealed record SaveOwnLocationRequest(string? Address, double Latitude, double Longitude);

public sealed record UpdateProfileRequest(string DisplayName, string UserName);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>
/// Sets, changes or takes away the PIN a client asks for before it shows what is private - see
/// Orbit.Core.Users.User.PrivatePinHash. Proved with the account's password rather than with the PIN
/// being replaced: four digits are a thing people forget, and one that can only be changed by somebody
/// who still remembers it locks its own owner out.
/// </summary>
/// <param name="NewPin">Four to eight digits, or null to be asked no more.</param>
public sealed record SetPrivatePinRequest(string Password, string? NewPin);

/// <summary>Answers "is this my PIN" - see Orbit.Core.Users.VerifyPrivatePin.</summary>
public sealed record VerifyPrivatePinRequest(string Pin);

/// <param name="GoogleIdToken">
/// A fresh Google sign-in proving the account's owner is the one asking - optional, so every phone build
/// installed before it existed, which sends the password alone, still binds. Checked when sent; see
/// DeleteAccountCommandHandler for what it proves and info/future-plan.md for when it becomes required.
/// </param>
public sealed record DeleteAccountRequest(string Password, string? GoogleIdToken = null);

/// <summary>Passing the account's current address re-verifies it; passing a different one starts an email change that only completes on confirmation.</summary>
public sealed record RequestEmailVerificationRequest(string EmailAddress);

public sealed record ConfirmEmailVerificationRequest(string Code);

public sealed record RequestPasswordResetRequest(string EmailOrUserName);

public sealed record ResetPasswordRequest(string EmailOrUserName, string Code, string NewPassword);

/// <summary>The Google ID token the browser obtained from Google Identity Services.</summary>
public sealed record GoogleSignInRequest(string IdToken);

public sealed record SetPasswordRequest(string NewPassword);
