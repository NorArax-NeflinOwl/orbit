using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Core.Users;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Choosing whether you are there to answer. What matters here is that the choice is only believed once
/// the server has taken it - somebody who clicked "do not disturb" and was refused must not be left
/// showing as available with nothing said about it.
/// </summary>
public sealed class PresenceServiceTests
{
    [Fact]
    public async Task A_choice_the_server_takes_is_the_one_that_holds()
    {
        var presence = Create(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var raised = 0;
        presence.Changed += () => raised++;

        var worked = await presence.SetAvailabilityAsync(PresenceAvailability.DoNotDisturb);

        Assert.True(worked);
        Assert.Equal(PresenceAvailability.DoNotDisturb, presence.Availability);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task A_refused_choice_says_so_and_changes_nothing()
    {
        // The picker keeps showing what is actually in force, which is why the caller has to be told:
        // otherwise a refused click is indistinguishable from one that never registered.
        var presence = Create(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));

        var worked = await presence.SetAvailabilityAsync(PresenceAvailability.DoNotDisturb);

        Assert.False(worked);
        Assert.Equal(PresenceAvailability.Available, presence.Availability);
    }

    [Fact]
    public async Task A_connection_that_is_down_is_an_answer_rather_than_a_throw()
    {
        // Thrown out of a click handler, this reaches the browser console and nobody else.
        var presence = Create(_ => throw new HttpRequestException("No route to host."));

        var worked = await presence.SetAvailabilityAsync(PresenceAvailability.DoNotDisturb);

        Assert.False(worked);
        Assert.Equal(PresenceAvailability.Available, presence.Availability);
    }

    [Fact]
    public async Task Nothing_is_announced_for_a_choice_that_did_not_take()
    {
        var presence = Create(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var raised = 0;
        presence.Changed += () => raised++;

        await presence.SetAvailabilityAsync(PresenceAvailability.DoNotDisturb);

        Assert.Equal(0, raised);
    }

    private static PresenceService Create(Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(answer))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        // The heartbeat is never started here: these tests are about the choice, and StartAsync would
        // put a timer and a JS import between the assertion and what it is about.
        return new PresenceService(new UsersApiClient(httpClient), new StubJSRuntime(), NullLogger<PresenceService>.Instance);
    }
}
