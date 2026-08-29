using Microsoft.Extensions.Logging;
using Orbit.Mobile.Api;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;

namespace Orbit.Mobile.Location;

/// <summary>One person's position as the screen shows it, already opened.</summary>
/// <param name="Position">Null when it could not be opened - their key changed, most often.</param>
public sealed record ReceivedPosition(Guid SharerUserId, string SharerDisplayName, bool IsContinuous, SharedPosition? Position)
{
    public bool CannotBeOpened => Position is null;
}

/// <summary>
/// Sharing a position, and opening the ones other people share.
///
/// <b>The key is the pairwise one the two already use for chat</b>, so a shared position is readable by
/// exactly those two people. That is not a shortcut: giving locations their own key pair would mean a
/// second thing to back up, restore and lose, for no gain in who can read what. The counterpart in
/// Orbit.Web is SharedLocationSender, and the sealed payload is byte-compatible with it - see
/// <see cref="SharedPosition"/>, where the property names are the contract.
/// </summary>
public sealed class SharedLocations
{
    private readonly LocationClient _locationClient;
    private readonly UsersClient _usersClient;
    private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;
    private readonly ILogger<SharedLocations> _logger;

    public SharedLocations(
        LocationClient locationClient, UsersClient usersClient, OwnEncryptionKeyProvider encryptionKeyProvider,
        ILogger<SharedLocations> logger)
    {
        _locationClient = locationClient;
        _usersClient = usersClient;
        _encryptionKeyProvider = encryptionKeyProvider;
        _logger = logger;
    }

    /// <summary>
    /// Seals a position for one person and shares it. The recipient's key is fetched now rather than
    /// taken from anything cached, for the reason sending a message does the same: a key they have since
    /// replaced would seal a position nobody can open.
    /// </summary>
    /// <returns>False when they have published no key, so there is nothing to seal for.</returns>
    public async Task<bool> ShareAsync(
        Guid recipientUserId, SharedPosition position, bool isContinuous, CancellationToken cancellationToken = default)
    {
        using var identity = await _encryptionKeyProvider.OpenAsync(cancellationToken);
        if (await _usersClient.FindAsync(recipientUserId, cancellationToken) is not { PublicKeyBase64: { } recipientKey })
        {
            _logger.LogWarning("No public key for {RecipientUserId}; not sharing a position with them", recipientUserId);
            return false;
        }

        var sealedPosition = identity.Encrypt(recipientKey, position.ToJson());
        await _locationClient.ShareAsync(
            recipientUserId, sealedPosition.CiphertextBase64, sealedPosition.NonceBase64, isContinuous, cancellationToken);

        return true;
    }

    /// <summary>
    /// What other people are sharing, opened. A position that cannot be opened is still listed, with the
    /// sharer named, rather than dropped - "their key changed" is worth seeing, and silently showing one
    /// fewer person than are actually sharing would be worse.
    /// </summary>
    public async Task<IReadOnlyList<ReceivedPosition>> ReadSharedWithMeAsync(CancellationToken cancellationToken = default)
    {
        using var identity = await _encryptionKeyProvider.OpenAsync(cancellationToken);
        var shares = await _locationClient.GetSharedWithMeAsync(cancellationToken);

        var received = new List<ReceivedPosition>(shares.Count);
        foreach (var share in shares)
        {
            var sharer = await _usersClient.FindAsync(share.SharerUserId, cancellationToken);
            var plainText = sharer?.PublicKeyBase64 is { } sharerKey
                ? identity.Decrypt(sharerKey, new EncryptedText(share.CiphertextBase64, share.NonceBase64))
                : null;

            received.Add(new ReceivedPosition(
                share.SharerUserId,
                sharer?.DisplayName ?? "Someone",
                share.IsContinuous,
                plainText is null ? null : SharedPosition.FromJson(plainText)));
        }

        return received;
    }
}
