using Orbit.GoogleIntegration;
using Xunit;

namespace Orbit.Api.Tests.Users;

/// <summary>
/// The accepted-audience list is what stops a token minted for some *other* Google application from
/// signing its holder in here, so widening it from one client id to several is worth pinning down -
/// particularly that it never widens to "anything".
/// </summary>
public sealed class GoogleAudienceAllowlistTests
{
    private const string WebClientId = "000000000000-web.apps.googleusercontent.com";
    private const string IosClientId = "000000000000-ios.apps.googleusercontent.com";
    private const string AndroidClientId = "000000000000-android.apps.googleusercontent.com";

    [Fact]
    public void Every_configured_client_is_accepted()
    {
        var settings = new GoogleAuthSettings
        {
            ClientId = WebClientId,
            IosClientId = IosClientId,
            AndroidClientId = AndroidClientId
        };

        Assert.Equal([WebClientId, IosClientId, AndroidClientId], settings.AcceptedClientIds);
        Assert.True(settings.IsConfigured);
    }

    [Fact]
    public void A_platform_that_has_no_client_yet_is_left_out_rather_than_accepted_as_empty()
    {
        // An empty audience entry would be a hole rather than a no-op, so unconfigured platforms must
        // disappear from the list entirely.
        var settings = new GoogleAuthSettings { ClientId = WebClientId };

        Assert.Equal([WebClientId], settings.AcceptedClientIds);
        Assert.DoesNotContain(string.Empty, settings.AcceptedClientIds);
    }

    [Fact]
    public void Configuring_nothing_accepts_nothing_rather_than_everything()
    {
        // The failure direction that matters: no configuration must refuse every token, never wave them
        // through for lack of an audience to check against.
        var settings = new GoogleAuthSettings();

        Assert.Empty(settings.AcceptedClientIds);
        Assert.False(settings.IsConfigured);
    }

    [Fact]
    public void A_mobile_only_deployment_still_verifies_tokens_even_though_the_web_button_is_hidden()
    {
        // ClientId doubles as what the browser is told to use, so leaving it empty hides the web button -
        // that must not also switch verification off for the apps that are configured.
        var settings = new GoogleAuthSettings { IosClientId = IosClientId };

        Assert.True(settings.IsConfigured);
        Assert.Equal([IosClientId], settings.AcceptedClientIds);
        Assert.Empty(settings.ClientId);
    }

    [Fact]
    public void Surrounding_whitespace_in_configuration_does_not_become_part_of_the_audience()
    {
        // Values arrive from environment variables and hand-edited JSON; a stray space would silently
        // stop matching the audience Google puts in the token.
        var settings = new GoogleAuthSettings { ClientId = $"  {WebClientId}  " };

        Assert.Equal([WebClientId], settings.AcceptedClientIds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_client_id_counts_as_unconfigured(string clientId)
    {
        var settings = new GoogleAuthSettings { ClientId = clientId };

        Assert.False(settings.IsConfigured);
        Assert.Empty(settings.AcceptedClientIds);
    }
}
