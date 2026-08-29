using Orbit.Mobile.Crypto;
using Xunit;

namespace Orbit.Mobile.Tests.Crypto;

/// <summary>
/// What the key gate asks for. Both wrong answers are dead ends the user cannot get out of: asking a
/// Google account with no password to "enter your password" asks for something that does not exist, and
/// asking an account that has one to "set a password" asks for something the server will refuse.
/// </summary>
public sealed class ChatKeyGateTests
{
    [Fact]
    public void A_device_that_already_holds_the_key_is_asked_for_nothing()
        => Assert.Equal(
            ChatKeyGateSituation.AlreadyUnlocked,
            ChatKeyGate.Decide(deviceHoldsTheKey: true, accountHasPassword: true));

    [Fact]
    public void An_account_with_no_password_is_asked_to_create_one()
        => Assert.Equal(
            ChatKeyGateSituation.SetFirstPassword,
            ChatKeyGate.Decide(deviceHoldsTheKey: false, accountHasPassword: false));

    [Fact]
    public void An_account_with_a_password_is_asked_for_it()
        => Assert.Equal(
            ChatKeyGateSituation.EnterPassword,
            ChatKeyGate.Decide(deviceHoldsTheKey: false, accountHasPassword: true));

    [Fact]
    public void A_device_holding_the_key_is_asked_for_nothing_even_without_a_password_on_the_account()
        // The key is already here; how it would have been restored no longer matters.
        => Assert.Equal(
            ChatKeyGateSituation.AlreadyUnlocked,
            ChatKeyGate.Decide(deviceHoldsTheKey: true, accountHasPassword: false));

    [Fact]
    public void Resetting_needs_an_address_the_code_can_actually_reach()
    {
        Assert.True(ChatKeyGate.CanResetPassword(isEmailVerified: true));
        Assert.False(ChatKeyGate.CanResetPassword(isEmailVerified: false));
    }
}
