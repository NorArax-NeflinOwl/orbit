using System.Text;
using Orbit.Mobile.Crypto;
using Xunit;

namespace Orbit.Mobile.Tests.Crypto;

/// <summary>
/// A picture sealed for the account's own eyes - bytes rather than JSON, in the one buffer the browser's
/// sealBytesForSelf writes: the 12-byte nonce, then the ciphertext with WebCrypto's tag on its end. The
/// browser's own half is already pinned to the phone's by BrowserInteropTests through the text path, so
/// the way to know a picture crosses too is that the bytes path is that same cipher in that layout.
/// </summary>
public sealed class SealedBytesTests
{
    [Fact]
    public void Bytes_sealed_on_the_phone_open_on_the_phone()
    {
        using var identity = ChatIdentity.Create();
        var picture = Encoding.UTF8.GetBytes("not really a JPEG, but sealed like one");

        var sealedBytes = identity.SealBytesForSelf(picture);

        Assert.NotEqual(picture, sealedBytes);
        Assert.Equal(12 + picture.Length + 16, sealedBytes.Length);
        Assert.Equal(picture, identity.OpenBytesForSelf(sealedBytes));
    }

    /// <summary>
    /// The text path is what the browser vectors pin; laid out as the browser lays bytes out - nonce
    /// first, then what it calls the ciphertext - it must open through the bytes path, or a picture
    /// sealed in a browser would not open on a phone.
    /// </summary>
    [Fact]
    public void Bytes_laid_out_as_the_browser_lays_them_out_open()
    {
        using var identity = ChatIdentity.Create();
        var sealedText = identity.EncryptForSelf("a picture's worth of words");
        byte[] asTheBrowserSendsIt =
            [.. Convert.FromBase64String(sealedText.NonceBase64), .. Convert.FromBase64String(sealedText.CiphertextBase64)];

        var opened = identity.OpenBytesForSelf(asTheBrowserSendsIt);

        Assert.Equal("a picture's worth of words", Encoding.UTF8.GetString(opened!));
    }

    [Fact]
    public void Bytes_sealed_under_another_key_or_tampered_with_do_not_open()
    {
        using var identity = ChatIdentity.Create();
        using var somebodyElse = ChatIdentity.Create();
        var sealedBytes = identity.SealBytesForSelf([1, 2, 3, 4, 5]);
        var tampered = (byte[])sealedBytes.Clone();
        tampered[^1] ^= 0xFF;

        Assert.Null(somebodyElse.OpenBytesForSelf(sealedBytes));
        Assert.Null(identity.OpenBytesForSelf(tampered));
        Assert.Null(identity.OpenBytesForSelf([1, 2, 3]));
    }
}
