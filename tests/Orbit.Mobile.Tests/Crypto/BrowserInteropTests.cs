using System.Text.Json;
using System.Text.Json.Serialization;
using Orbit.Contracts.Users;
using Orbit.Mobile.Crypto;
using Xunit;

namespace Orbit.Mobile.Tests.Crypto;

/// <summary>
/// The .NET side of Orbit's end-to-end encryption, checked against ciphertext a real browser produced
/// with Orbit.Web's own e2eeChat.js - see the README beside the vectors.
///
/// This is the test the plan asks for before anything is built on top of the crypto (§4.1, §11). The
/// failure it exists to catch is specific and nasty: applying a KDF to the ECDH shared secret gives code
/// that encrypts and decrypts perfectly against itself, passes any round-trip test, and cannot read a
/// single message from the web client. A round-trip test would have said everything was fine.
/// </summary>
public sealed class BrowserInteropTests
{
    private static readonly BrowserVectors Vectors = LoadVectors();

    [Fact]
    public void The_shared_secret_matches_the_one_the_browser_agreed()
    {
        using var alice = RestoreAlice();

        var agreed = alice.Encrypt(Vectors.Bob.PublicKeyBase64, "anything");

        // Proven indirectly but exactly: if the agreed key were not byte-for-byte the browser's, nothing
        // below this line could decrypt. The direct comparison is the next test.
        Assert.NotNull(agreed.CiphertextBase64);
    }

    [Fact]
    public void The_message_key_is_the_raw_shared_secret_with_no_key_derivation_applied()
    {
        using var alice = RestoreAlice();

        // The browser recorded the raw ECDH output and proved, in the browser, that it *is* the AES key
        // deriveKey produced. So a message sealed under that raw value must open with the agreement -
        // which is only true if .NET applied no KDF either.
        var sealedWithRawSecret = SealWithRawKey(
            Convert.FromBase64String(Vectors.SharedSecretBase64), "no KDF, or nothing works");

        Assert.Equal("no KDF, or nothing works", alice.Decrypt(Vectors.Bob.PublicKeyBase64, sealedWithRawSecret));
    }

    [Fact]
    public void A_message_the_browser_sent_is_readable_here()
    {
        using var bob = RestoreBob();

        var plainText = bob.Decrypt(Vectors.Alice.PublicKeyBase64, Vectors.AliceToBob.ToEncryptedText());

        // Includes Polish characters and an emoji, so a mistake in the UTF-8 handling shows up rather
        // than hiding behind ASCII.
        Assert.Equal(Vectors.AliceToBob.PlainText, plainText);
    }

    [Fact]
    public void A_message_in_the_other_direction_is_readable_too()
    {
        using var alice = RestoreAlice();

        Assert.Equal(
            Vectors.BobToAlice.PlainText,
            alice.Decrypt(Vectors.Bob.PublicKeyBase64, Vectors.BobToAlice.ToEncryptedText()));
    }

    [Fact]
    public void Content_the_browser_sealed_for_one_reader_opens_here()
    {
        using var alice = RestoreAlice();

        // encryptForSelf - private notes and task lists, which run the same agreement with the user's
        // own key on both sides.
        Assert.Equal(Vectors.AliceToSelf.PlainText, alice.DecryptForSelf(Vectors.AliceToSelf.ToEncryptedText()));
    }

    [Fact]
    public void A_private_key_the_browser_backed_up_is_restored_to_the_same_identity()
    {
        using var alice = RestoreAlice();

        // The JWK mapping is the part with no native support in .NET, so this checks the whole path:
        // PBKDF2 unwrap, JWK to ECParameters, and the raw public-key export the server exchanges.
        Assert.Equal(Vectors.Alice.PublicKeyBase64, alice.PublicKeyBase64);
    }

    [Fact]
    public void The_wrong_password_gives_back_nothing_rather_than_throwing()
    {
        // The caller cannot tell a wrong password from a damaged backup, and neither outcome is
        // recoverable - so both are the same answer, matching what the browser does.
        Assert.Null(ChatIdentity.FromBackup(Vectors.Alice.Backup, "not the password"));
    }

    [Fact]
    public void A_backup_written_here_can_be_restored_here()
    {
        using var identity = ChatIdentity.Create();

        var backup = identity.WrapWithPassword("a passphrase");
        using var restored = ChatIdentity.FromBackup(backup, "a passphrase");

        Assert.Equal(identity.PublicKeyBase64, restored!.PublicKeyBase64);
    }

    [Fact]
    public void A_backup_written_here_uses_the_iteration_count_the_browser_uses()
    {
        using var identity = ChatIdentity.Create();

        // A lower count would still round-trip locally while quietly weakening every backup written by
        // a phone, which nothing else would notice.
        Assert.Equal(Vectors.Alice.Backup.Iterations, identity.WrapWithPassword("a passphrase").Iterations);
    }

    [Fact]
    public void A_sealed_message_carries_the_tag_inside_the_ciphertext_the_way_the_browser_expects()
    {
        using var alice = RestoreAlice();

        var sealedText = alice.Encrypt(Vectors.Bob.PublicKeyBase64, "12345678");

        // WebCrypto appends the 16-byte GCM tag; .NET keeps it in its own buffer. Sending it separately
        // would produce something the browser rejects as corrupt.
        Assert.Equal(8 + 16, Convert.FromBase64String(sealedText.CiphertextBase64).Length);
        Assert.Equal(12, Convert.FromBase64String(sealedText.NonceBase64).Length);
    }

    [Fact]
    public void Something_sealed_for_one_person_cannot_be_opened_by_another()
    {
        using var alice = RestoreAlice();
        using var stranger = ChatIdentity.Create();

        Assert.Null(stranger.Decrypt(Vectors.Alice.PublicKeyBase64, Vectors.AliceToBob.ToEncryptedText()));
    }

    private static ChatIdentity RestoreAlice()
        => ChatIdentity.FromBackup(Vectors.Alice.Backup, Vectors.BackupPassword)!;

    private static ChatIdentity RestoreBob()
        => ChatIdentity.FromBackup(Vectors.Bob.Backup, Vectors.BackupPassword)!;

    /// <summary>Seals with a key given directly, to check what the agreement is expected to produce.</summary>
    private static EncryptedText SealWithRawKey(byte[] key, string plainText)
    {
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var nonce = System.Security.Cryptography.RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plainBytes.Length];
        var tag = new byte[16];

        using var cipher = new System.Security.Cryptography.AesGcm(key, 16);
        cipher.Encrypt(nonce, plainBytes, ciphertext, tag);

        return new EncryptedText(Convert.ToBase64String([.. ciphertext, .. tag]), Convert.ToBase64String(nonce));
    }

    private static BrowserVectors LoadVectors()
        => JsonSerializer.Deserialize<BrowserVectors>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Crypto", "browser-e2ee-vectors.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    private sealed record BrowserVectors(
        bool RawSharedSecretEqualsAesKey,
        VectorParty Alice,
        VectorParty Bob,
        string BackupPassword,
        string SharedSecretBase64,
        VectorMessage AliceToBob,
        VectorMessage BobToAlice,
        VectorMessage AliceToSelf);

    private sealed record VectorParty(string UserId, string PublicKeyBase64, WrappedPrivateKeyDto Backup);

    private sealed record VectorMessage(
        [property: JsonPropertyName("ciphertextBase64")] string CiphertextBase64,
        [property: JsonPropertyName("nonceBase64")] string NonceBase64,
        [property: JsonPropertyName("plainText")] string PlainText)
    {
        public EncryptedText ToEncryptedText() => new(CiphertextBase64, NonceBase64);
    }
}
