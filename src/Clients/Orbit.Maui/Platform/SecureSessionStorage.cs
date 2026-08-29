using System.Text.Json;
using Orbit.Mobile.Authentication;

namespace Orbit.Maui.Platform;

/// <summary>
/// Keeps the signed-in session in the platform's secure store - Keychain on iOS, Keystore-backed
/// EncryptedSharedPreferences on Android - which is what MAUI's <see cref="SecureStorage"/> maps to.
/// Preferences would be the easy alternative and is wrong: a refresh token is good for thirty days.
///
/// Stored as a single JSON value under one key so the session is written and removed as a whole; five
/// separate keys could be left half-written by a crash, leaving an identity with no tokens behind it.
/// </summary>
public sealed class SecureSessionStorage : ISessionStorage
{
    private const string StorageKey = "orbit.session";

    private readonly ISecureStorage _secureStorage;

    public SecureSessionStorage(ISecureStorage secureStorage) => _secureStorage = secureStorage;

    public async Task<UserSession?> ReadAsync()
    {
        var stored = await _secureStorage.GetAsync(StorageKey);
        if (string.IsNullOrEmpty(stored))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(stored, SessionSerializerContext.Default.UserSession);
        }
        catch (JsonException)
        {
            // Written by a build that stored a different shape. Treat it as signed out rather than
            // failing to start - the user can log in again, but cannot repair a Keychain entry.
            await ClearAsync();
            return null;
        }
    }

    public Task WriteAsync(UserSession session)
        => _secureStorage.SetAsync(
            StorageKey, JsonSerializer.Serialize(session, SessionSerializerContext.Default.UserSession));

    public Task ClearAsync()
    {
        _secureStorage.Remove(StorageKey);
        return Task.CompletedTask;
    }
}
