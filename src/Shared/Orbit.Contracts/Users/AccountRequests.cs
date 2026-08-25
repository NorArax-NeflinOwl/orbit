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
    bool IsGoogleLinked);

public sealed record UpdateProfileRequest(string DisplayName, string UserName);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record DeleteAccountRequest(string Password);

/// <summary>Passing the account's current address re-verifies it; passing a different one starts an email change that only completes on confirmation.</summary>
public sealed record RequestEmailVerificationRequest(string EmailAddress);

public sealed record ConfirmEmailVerificationRequest(string Code);

public sealed record RequestPasswordResetRequest(string EmailOrUserName);

public sealed record ResetPasswordRequest(string EmailOrUserName, string Code, string NewPassword);

/// <summary>The Google ID token the browser obtained from Google Identity Services.</summary>
public sealed record GoogleSignInRequest(string IdToken);

public sealed record SetPasswordRequest(string NewPassword);
