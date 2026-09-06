using System.Text;
using System.Text.Json;
using Orbit.Api.LiveUpdates;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.LiveUpdates;
using Xunit;

namespace Orbit.Api.Tests.LiveUpdates;

/// <summary>
/// What has to survive the trip between two API instances.
///
/// The whole point of the backplane is a failure nobody would report. With one replica an announcement
/// always reached its client, so nothing here was ever exercised; with two, an announcement made on the
/// instance that did the work has to reach connections on the other one, and when it does not, the app
/// merely feels slow again. There is no error, no failed request and no log line on the client - which
/// is why the wire format is pinned down here rather than trusted to a deployment nobody is watching.
/// </summary>
public sealed class LiveUpdateBackplaneTests
{
    /// <summary>
    /// PostgreSQL refuses a NOTIFY payload over 8000 bytes. A group chat is the case that reaches it -
    /// a conversation with hundreds of members is one announcement to hundreds of accounts - and the
    /// failure would land on exactly the feature the backplane exists for.
    /// </summary>
    [Fact]
    public void A_large_audience_is_split_into_payloads_postgres_will_accept()
    {
        var audience = Enumerable.Range(0, 250).Select(_ => Guid.NewGuid()).ToArray();

        var announcements = LiveUpdateAnnouncement
            .ForAudience(Guid.NewGuid(), LiveUpdateMessages.ChatChanged, audience, [])
            .ToArray();

        Assert.Equal(3, announcements.Length);
        Assert.Equal(audience, announcements.SelectMany(announcement => announcement.UserIds));

        foreach (var announcement in announcements)
        {
            var payloadBytes = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(announcement));
            Assert.True(payloadBytes < 8000, $"A payload of {payloadBytes} bytes would be refused.");
        }
    }

    /// <summary>
    /// Each part of a split announcement is a whole announcement to the accounts it names - so anything
    /// it carries has to ride on all of them. Dropping the arguments after the first part would leave
    /// most of a large audience hearing PresenceChanged without being told whose.
    /// </summary>
    [Fact]
    public void Every_part_of_a_split_announcement_carries_what_the_message_needs()
    {
        var subject = Guid.NewGuid();
        var audience = Enumerable.Range(0, 250).Select(_ => Guid.NewGuid()).ToArray();

        var announcements = LiveUpdateAnnouncement
            .ForAudience(Guid.NewGuid(), LiveUpdateMessages.PresenceChanged, audience, [subject])
            .ToArray();

        Assert.All(announcements, announcement =>
            Assert.Equal(subject, Assert.Single(announcement.Arguments).GetGuid()));
    }

    /// <summary>
    /// The announcement has to come out of the wire as what went in, arguments included. They cross as
    /// JSON and are handed straight to SignalR on the other side, which serialises them to the client -
    /// so what the receiving instance sends is the JSON the sending instance would have sent.
    /// </summary>
    [Fact]
    public void An_announcement_survives_the_round_trip()
    {
        var subject = Guid.NewGuid();
        var audience = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var origin = Guid.NewGuid();

        var sent = LiveUpdateAnnouncement
            .ForAudience(origin, LiveUpdateMessages.PresenceChanged, audience, [subject])
            .Single();

        var received = JsonSerializer.Deserialize<LiveUpdateAnnouncement>(JsonSerializer.Serialize(sent));

        Assert.NotNull(received);
        Assert.Equal(origin, received.Origin);
        Assert.Equal(LiveUpdateMessages.PresenceChanged, received.Message);
        Assert.Equal(audience, received.UserIds);
        Assert.Equal(
            JsonSerializer.Serialize(subject),
            Assert.Single(received.Arguments).GetRawText());
    }

    /// <summary>
    /// NOTIFY reaches every listener including the sender, and the sender delivered to its own
    /// connections before it sent. The origin is what stops the instance that did the work from
    /// announcing to its clients twice, so it has to survive the trip - see LiveUpdateInstance.
    /// </summary>
    [Fact]
    public void An_announcement_says_which_instance_made_it()
    {
        var instance = new LiveUpdateInstance();

        var announcement = LiveUpdateAnnouncement
            .ForAudience(instance.Id, LiveUpdateMessages.ChatChanged, [Guid.NewGuid()], [])
            .Single();

        var received = JsonSerializer.Deserialize<LiveUpdateAnnouncement>(
            JsonSerializer.Serialize(announcement));

        Assert.Equal(instance.Id, received!.Origin);
        Assert.NotEqual(new LiveUpdateInstance().Id, received.Origin);
    }

    /// <summary>
    /// The names are the agreement with the client (see LiveUpdateMessages), and the audience is the
    /// half that is invisible when wrong. Both are decided in one place now that there are two
    /// transports, and this is that place.
    /// </summary>
    [Fact]
    public async Task Each_announcement_goes_out_under_the_name_its_client_listens_for()
    {
        var fanOut = new RecordingLiveUpdateFanOut();
        var announcer = new LiveUpdateAnnouncer(fanOut);
        var reader = Guid.NewGuid();
        var writer = Guid.NewGuid();

        await announcer.ChatChangedAsync(reader, CancellationToken.None);
        await announcer.NotificationsChangedAsync(reader, CancellationToken.None);
        await announcer.PresenceChangedAsync(writer, [reader], CancellationToken.None);

        Assert.Collection(
            fanOut.Announcements,
            chat =>
            {
                Assert.Equal(LiveUpdateMessages.ChatChanged, chat.Message);
                Assert.Equal([reader], chat.Audience);
            },
            notifications =>
            {
                Assert.Equal(LiveUpdateMessages.NotificationsChanged, notifications.Message);
                Assert.Equal([reader], notifications.Audience);
            },
            presence =>
            {
                Assert.Equal(LiveUpdateMessages.PresenceChanged, presence.Message);

                // Announced to the people who can see it, carrying whose presence it was - not to the
                // person it is about, who already knows.
                Assert.Equal([reader], presence.Audience);
                Assert.Equal(writer, Assert.Single(presence.Arguments));
            });
    }
}
