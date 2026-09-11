using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Sharing;
using Orbit.Core.Notifications;
using Orbit.Web.Services;
using Orbit.Web.Tests.TestDoubles;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Where an offer taken up in a conversation leads: the chat has only the share's id, and the accept
/// endpoints answer only whether it worked, so the thing is found through the offer.
/// </summary>
public sealed class SharesApiClientTests
{
    private static readonly Guid ShareId = Guid.NewGuid();
    private static readonly Guid ItemId = Guid.NewGuid();

    [Fact]
    public async Task An_accepted_note_leads_to_the_note_itself()
    {
        var client = ClientAnswering(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new ShareOfferDto(ItemId, "Shopping", IsAccepted: true))
        });

        Assert.Equal($"/notes/{ItemId}", await client.WhereItLandsAsync(SharedItemKind.Note, ShareId));
    }

    [Fact]
    public async Task An_accepted_place_leads_to_its_pin_on_the_map()
    {
        var client = ClientAnswering(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new ShareOfferDto(ItemId, "The good bakery", IsAccepted: true))
        });

        Assert.Equal($"/map?place={ItemId}", await client.WhereItLandsAsync(SharedItemKind.Place, ShareId));
    }

    /// <summary>
    /// An offer that can no longer be read - gone, or a server that did not answer - still leaves the
    /// section it went into, since the acceptance itself has already happened.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task An_offer_that_cannot_be_read_leads_to_the_section(HttpStatusCode status)
    {
        var client = ClientAnswering(_ => new HttpResponseMessage(status));

        Assert.Equal("/tasks", await client.WhereItLandsAsync(SharedItemKind.TaskList, ShareId));
    }

    private static SharesApiClient ClientAnswering(Func<HttpRequestMessage, HttpResponseMessage> answer)
        => new(new HttpClient(new StubHttpMessageHandler(answer)) { BaseAddress = new Uri("https://example.test/") });
}
