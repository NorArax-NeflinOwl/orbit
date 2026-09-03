using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Orbit.Contracts;

namespace Orbit.Mobile.Crypto;

/// <summary>
/// The account's own key, opened once and used for as many items as the caller has - see
/// <see cref="PrivateContentSealer"/> for why it is handed out rather than used one item at a time.
/// Dispose it when the batch is done: it holds the private key.
/// </summary>
public sealed class PrivateContentKey : IDisposable
{
    private readonly ChatIdentity _identity;

    internal PrivateContentKey(ChatIdentity identity) => _identity = identity;

    /// <summary>
    /// Seals <paramref name="content"/> into what the server is allowed to hold. Serialized through a
    /// source-generated type info rather than reflection, because release builds of the app trim and
    /// AOT-compile - see LocalStoreSerializerContext for the same reason.
    /// </summary>
    public EncryptedContentDto Seal<TContent>(TContent content, JsonTypeInfo<TContent> typeInfo)
    {
        var sealedContent = _identity.EncryptForSelf(JsonSerializer.Serialize(content, typeInfo));
        return new EncryptedContentDto(sealedContent.CiphertextBase64, sealedContent.NonceBase64);
    }

    /// <summary>
    /// Opens what <see cref="Seal{TContent}"/> or a browser produced. Null when it cannot be opened -
    /// content sealed under a key pair that has since been replaced, most often - so one unreadable item
    /// can be shown as unreadable instead of taking a whole list down with it.
    /// </summary>
    public TContent? Open<TContent>(EncryptedContentDto encryptedContent, JsonTypeInfo<TContent> typeInfo)
    {
        var plainText = _identity.DecryptForSelf(
            new EncryptedText(encryptedContent.Ciphertext, encryptedContent.Nonce));

        if (plainText is null)
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize(plainText, typeInfo);
        }
        catch (JsonException)
        {
            // Opened, but not into what this caller expected. Treated as unreadable for the same reason
            // a failed decryption is: there is nothing to show either way, and throwing here would take
            // out the whole list rather than the one item.
            return default;
        }
    }

    public void Dispose() => _identity.Dispose();
}
