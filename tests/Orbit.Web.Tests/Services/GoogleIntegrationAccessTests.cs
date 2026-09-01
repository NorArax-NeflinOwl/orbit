using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Contracts.Users;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Who is offered the links that hand something off to Google. Two answers have to agree: the account
/// has to qualify, and whoever is at this browser has to want them.
/// </summary>
public sealed class GoogleIntegrationAccessTests
{
    [Fact]
    public async Task A_verified_account_is_offered_them()
    {
        var access = Access(AnAccount(isEmailVerified: true), TurnedOn());

        Assert.True(await access.IsAvailableAsync());
    }

    [Fact]
    public async Task An_account_that_has_neither_verified_nor_connected_is_not()
    {
        var access = Access(AnAccount(isEmailVerified: false), TurnedOn());

        Assert.False(await access.IsAvailableAsync());
    }

    /// <summary>
    /// The switch has the last word. Qualifying only says the links may be offered; it does not say
    /// anybody wants them - and turning them off must not need a Google account disconnected first.
    /// </summary>
    [Fact]
    public async Task A_browser_that_turned_them_off_is_not_offered_them_however_the_account_qualifies()
    {
        var access = Access(AnAccount(isEmailVerified: true), await TurnedOffAsync());

        Assert.False(await access.IsAvailableAsync());
    }

    /// <summary>Never asked anything, which is a browser where the extras are on - see DevicePreferences.</summary>
    private static DevicePreferences TurnedOn() => new(new StubJSRuntime());

    private static async Task<DevicePreferences> TurnedOffAsync()
    {
        var preferences = new DevicePreferences(new StubJSRuntime());
        await preferences.SetAllowGoogleExtrasAsync(false);
        return preferences;
    }

    private static GoogleIntegrationAccess Access(AccountDto account, DevicePreferences preferences)
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(account)
        });
        return new GoogleIntegrationAccess(
            new UsersApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") }),
            preferences,
            NullLogger<GoogleIntegrationAccess>.Instance);
    }

    private static AccountDto AnAccount(bool isEmailVerified)
        => new(
            Guid.NewGuid(), "owner@example.com", "owner", "Owner",
            isEmailVerified, HasPassword: true, IsGoogleLinked: false);
}
