using System.Text.Json;
using Orbit.Contracts.Users;
using Orbit.Mobile.Crypto;
using Xunit;

namespace Orbit.Mobile.Tests.Crypto;

/// <summary>
/// The other half of the interop. <see cref="BrowserInteropTests"/> proves this side can read what a
/// browser wrote; nothing in a .NET test can prove the reverse, because only a browser can say whether
/// WebCrypto accepts what this side produces.
///
/// So this writes the artefacts for that check next to the test binary, and
/// <c>verify-dotnet-output-in-a-browser.html</c> consumes them - see the README. Without it the reverse
/// direction is a one-off someone did by hand once and nobody can repeat.
/// </summary>
public sealed class DotNetOutputTests
{
    private const string PlainText = "Sent from .NET — zażółć gęślą jaźń 🔐";
    private const string SelfSealedText = "A note sealed by .NET for one reader.";
    private const string FreshBackupPassword = "a passphrase from dotnet";

    [Fact]
    public void What_this_side_writes_is_self_consistent_and_saved_for_a_browser_to_check()
    {
        var vectors = BrowserVectorsFile.Read();
        using var alice = ChatIdentity.FromBackup(vectors.Alice.Backup, vectors.BackupPassword)!;
        using var bob = ChatIdentity.FromBackup(vectors.Bob.Backup, vectors.BackupPassword)!;

        var message = alice.Encrypt(vectors.Bob.PublicKeyBase64, PlainText);
        var selfSealed = alice.EncryptForSelf(SelfSealedText);
        using var freshIdentity = ChatIdentity.Create();
        var freshBackup = freshIdentity.WrapWithPassword(FreshBackupPassword);

        // Consistent here, which is necessary but not sufficient - the browser check is what settles it.
        Assert.Equal(PlainText, bob.Decrypt(vectors.Alice.PublicKeyBase64, message));
        Assert.Equal(SelfSealedText, alice.DecryptForSelf(selfSealed));

        File.WriteAllText(
            Path.Combine(AppContext.BaseDirectory, "dotnet-produced.json"),
            JsonSerializer.Serialize(
                new
                {
                    alicePublicKeyBase64 = alice.PublicKeyBase64,
                    bobPublicKeyBase64 = vectors.Bob.PublicKeyBase64,
                    backupPassword = vectors.BackupPassword,
                    aliceBackup = vectors.Alice.Backup,
                    bobBackup = vectors.Bob.Backup,
                    messageFromDotNet = new { message.CiphertextBase64, message.NonceBase64, plainText = PlainText },
                    selfSealedByDotNet = new { selfSealed.CiphertextBase64, selfSealed.NonceBase64, plainText = SelfSealedText },
                    freshBackupFromDotNet = freshBackup,
                    freshBackupPassword = FreshBackupPassword,
                    freshPublicKeyBase64 = freshIdentity.PublicKeyBase64
                },
                new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public void The_public_key_this_side_derives_from_a_backup_is_the_one_the_browser_published()
    {
        var vectors = BrowserVectorsFile.Read();

        using var alice = ChatIdentity.FromBackup(vectors.Alice.Backup, vectors.BackupPassword)!;
        using var bob = ChatIdentity.FromBackup(vectors.Bob.Backup, vectors.BackupPassword)!;

        Assert.Equal(vectors.Alice.PublicKeyBase64, alice.PublicKeyBase64);
        Assert.Equal(vectors.Bob.PublicKeyBase64, bob.PublicKeyBase64);
    }
}

/// <summary>The committed browser vectors, read once and shared by both interop test classes.</summary>
internal static class BrowserVectorsFile
{
    public static Vectors Read()
        => JsonSerializer.Deserialize<Vectors>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Crypto", "browser-e2ee-vectors.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    internal sealed record Vectors(Party Alice, Party Bob, string BackupPassword, string SharedSecretBase64);

    internal sealed record Party(string UserId, string PublicKeyBase64, WrappedPrivateKeyDto Backup);
}
