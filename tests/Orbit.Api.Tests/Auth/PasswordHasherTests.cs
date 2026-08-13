using Orbit.Api.Auth;
using Xunit;

namespace Orbit.Api.Tests.Auth;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Verify_accepts_the_password_that_was_hashed()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("correct horse battery staple");

        Assert.True(hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_rejects_a_different_password()
    {
        var hasher = new PasswordHasher();
        var hash = hasher.Hash("correct horse battery staple");

        Assert.False(hasher.Verify("something else", hash));
    }

    [Fact]
    public void Hash_produces_a_different_value_each_time_for_the_same_password()
    {
        var hasher = new PasswordHasher();

        // PasswordHasher<T> salts every hash, so two hashes of the same password should never match
        // verbatim even though both verify successfully.
        Assert.NotEqual(hasher.Hash("correct horse battery staple"), hasher.Hash("correct horse battery staple"));
    }
}
