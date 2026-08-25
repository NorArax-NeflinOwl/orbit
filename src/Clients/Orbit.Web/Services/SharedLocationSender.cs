using System.Text.Json;
using Microsoft.JSInterop;
using Orbit.Contracts.Users;

namespace Orbit.Web.Services;

/// <summary>
/// Seals a position for one recipient and shares it. The key is the pairwise one the two already use
/// for chat, so a shared position is readable by exactly those two people and by nobody else - Orbit's
/// servers relay something they cannot open, the same way they relay a message.
///
/// The counterpart to <see cref="EncryptedChatMessageReader"/>, which opens what this seals.
/// </summary>
public sealed class SharedLocationSender
{
    private readonly UsersApiClient _usersApiClient;
    private readonly OwnEncryptionKeyProvider _ownEncryptionKeyProvider;
    private readonly IJSRuntime _jsRuntime;

    public SharedLocationSender(
        UsersApiClient usersApiClient, OwnEncryptionKeyProvider ownEncryptionKeyProvider, IJSRuntime jsRuntime)
    {
        _usersApiClient = usersApiClient;
        _ownEncryptionKeyProvider = ownEncryptionKeyProvider;
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Shares one point with one recipient, replacing whatever was shared with them before.
    /// <paramref name="isContinuous"/> only describes the intent - it is the caller's own timer that
    /// actually keeps refreshing (see MapPage), and stopping is a separate, explicit call.
    ///
    /// Throws <see cref="InvalidOperationException"/> when the recipient has never signed in, since
    /// there is no key to seal for them and sharing into nothing would look like it had worked.
    /// </summary>
    public async Task ShareAsync(
        Guid ownUserId, Guid recipientUserId, SharedPosition position, bool isContinuous, CancellationToken cancellationToken = default)
    {
        var recipient = await _usersApiClient.GetUserAsync(recipientUserId, cancellationToken);
        if (recipient?.PublicKeyBase64 is null)
        {
            throw new InvalidOperationException(
                "That person has never signed in, so there is no key to encrypt your location for them yet.");
        }

        await _ownEncryptionKeyProvider.EnsurePublicKeyAsync();
        await using var cryptoModule = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/e2eeChat.js");
        var sealedPosition = await cryptoModule.InvokeAsync<SealedPayload>(
            "encryptMessage", cancellationToken, ownUserId, recipient.PublicKeyBase64, JsonSerializer.Serialize(position));

        await _usersApiClient.ShareLocationAsync(
            new ShareLocationRequest(recipientUserId, sealedPosition.CiphertextBase64, sealedPosition.NonceBase64, isContinuous),
            cancellationToken);
    }

    /// <summary>What a shared position carries - the same shape whether it was sent once or is being refreshed.</summary>
    public sealed record SharedPosition(double Latitude, double Longitude, string? Address, DateTimeOffset RecordedAtUtc);

    /// <summary>Shape returned by e2eeChat.js's encryptMessage - matched by camelCase property name.</summary>
    private sealed record SealedPayload(string CiphertextBase64, string NonceBase64);
}
