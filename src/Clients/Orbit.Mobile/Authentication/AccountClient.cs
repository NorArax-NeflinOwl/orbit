using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Users;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Authentication;

/// <summary>
/// Registering an account, and changing the three things that identify it: username, email address, and
/// password.
///
/// <b>Every one of these requires a connection, and none of them is ever queued.</b> That is a decision
/// rather than a limitation of the sync layer. Each needs a verdict only the server can give - whether
/// a username is free, whether an email address is already registered, whether the current password is
/// right - and each has effects beyond this device, including on how the user signs in everywhere else.
/// A queued account change would tell someone their password had changed while the old one still
/// worked, possibly for days. Notes can wait in an outbox because nothing else depends on when they
/// land; an identity cannot.
///
/// Registration in particular writes the user to the server first and only then keeps anything locally,
/// so a local account can never exist that the server has never heard of.
/// </summary>
public sealed class AccountClient
{
    private readonly HttpClient _httpClient;
    private readonly INetworkStatus _networkStatus;
    private readonly SessionStore _sessionStore;

    public AccountClient(HttpClient httpClient, INetworkStatus networkStatus, SessionStore sessionStore)
    {
        _httpClient = httpClient;
        _networkStatus = networkStatus;
        _sessionStore = sessionStore;
    }

    /// <summary>
    /// Creates the account on the server and, only once that succeeded, stores the session locally.
    /// There is no offline path: an account that exists on one phone and nowhere else is not an account.
    /// </summary>
    public async Task<AccountOperationResult> RegisterAsync(
        string emailAddress, string userName, string displayName, string password,
        CancellationToken cancellationToken = default)
    {
        if (!_networkStatus.IsOnline)
        {
            return AccountOperationResult.RequiresConnection;
        }

        var response = await _httpClient.PostAsJsonAsync(
            "api/auth/register", new RegisterUserRequest(emailAddress, userName, displayName, password), cancellationToken);

        if (response.StatusCode is HttpStatusCode.Conflict)
        {
            return AccountOperationResult.Refused(
                await ReadServerMessageAsync(response, "That email address or username is already taken.", cancellationToken));
        }

        response.EnsureSuccessStatusCode();

        if (await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken) is not { } registered)
        {
            return AccountOperationResult.Refused("Orbit accepted the account but sent nothing back.");
        }

        await _sessionStore.SetAsync(UserSession.FromAuthResponse(registered));
        return AccountOperationResult.Applied;
    }

    /// <summary>
    /// The signed-in account as its owner sees it. The chat key gate needs three things from it: whether
    /// this account has a password at all (a Google account may not), the address a reset code would go
    /// to, and whether that address has been confirmed.
    /// </summary>
    public Task<AccountDto?> GetAccountAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<AccountDto>("api/users/me", cancellationToken);

    /// <summary>
    /// The first password on an account that has none - a Google account reaching chat. Separate from a
    /// change because there is no current password to prove.
    /// </summary>
    public Task<AccountOperationResult> SetFirstPasswordAsync(
        string newPassword, CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Post, "api/users/me/password", new SetPasswordRequest(newPassword),
            "This account already has a password - enter it instead.", cancellationToken);

    /// <summary>
    /// Asks for a reset code by email. Always succeeds as far as the caller can tell, whether or not
    /// that account exists - the server refuses to be an account-existence oracle.
    /// </summary>
    public Task<AccountOperationResult> RequestPasswordResetAsync(
        string emailOrUserName, CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Post, "api/auth/password-reset", new RequestPasswordResetRequest(emailOrUserName),
            "Couldn't send a reset code.", cancellationToken);

    public Task<AccountOperationResult> ResetPasswordAsync(
        string emailOrUserName, string code, string newPassword, CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Post, "api/auth/password-reset/confirm", new ResetPasswordRequest(emailOrUserName, code, newPassword),
            "That code isn't valid any more. Request a new one.", cancellationToken);

    /// <summary>
    /// Changes the username, which is one of the things the user signs in with - so only the server can
    /// say whether it is still free, and it has to say so before the change counts.
    /// </summary>
    public Task<AccountOperationResult> ChangeUserNameAsync(
        string userName, string displayName, CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Put, "api/users/me/profile", new UpdateProfileRequest(displayName, userName),
            "That username is already taken.", cancellationToken);

    /// <summary>
    /// Starts an email change. It does not take effect here: the server sends a code to the new address
    /// and only <see cref="ConfirmEmailAddressAsync"/> completes it, so an address nobody can receive
    /// mail at never becomes the one the account is recovered through.
    /// </summary>
    public Task<AccountOperationResult> RequestEmailAddressChangeAsync(
        string emailAddress, CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Post, "api/users/me/email-verification", new RequestEmailVerificationRequest(emailAddress),
            "An account with this email address already exists.", cancellationToken);

    public Task<AccountOperationResult> ConfirmEmailAddressAsync(
        string code, CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Post, "api/users/me/email-verification/confirm", new ConfirmEmailVerificationRequest(code),
            "That code isn't valid any more. Request a new one.", cancellationToken);

    /// <summary>
    /// The current password is checked by the server, never here - the phone has no way to verify it,
    /// and a change accepted locally would be a change that had not happened.
    /// </summary>
    public Task<AccountOperationResult> ChangePasswordAsync(
        string currentPassword, string newPassword, CancellationToken cancellationToken = default)
        => SendAsync(
            HttpMethod.Put, "api/users/me/password", new ChangePasswordRequest(currentPassword, newPassword),
            "That current password isn't right.", cancellationToken);

    private async Task<AccountOperationResult> SendAsync<TRequest>(
        HttpMethod method, string path, TRequest body, string refusalMessage, CancellationToken cancellationToken)
    {
        if (!_networkStatus.IsOnline)
        {
            return AccountOperationResult.RequiresConnection;
        }

        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
        {
            return AccountOperationResult.Refused(
                await ReadServerMessageAsync(response, refusalMessage, cancellationToken));
        }

        response.EnsureSuccessStatusCode();
        return AccountOperationResult.Applied;
    }

    /// <summary>
    /// The server's own wording where it gave one, since it knows more about the refusal than the
    /// client's guess does - falling back to <paramref name="fallback"/> rather than showing raw JSON.
    /// </summary>
    private static async Task<string> ReadServerMessageAsync(
        HttpResponseMessage response, string fallback, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ServerMessage>(cancellationToken);
            return string.IsNullOrWhiteSpace(problem?.Message) ? fallback : problem.Message;
        }
        catch (Exception exception) when (exception is System.Text.Json.JsonException or HttpRequestException)
        {
            return fallback;
        }
    }

    private sealed record ServerMessage(string? Message);
}
