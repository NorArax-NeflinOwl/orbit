using System.Net;
using Orbit.Api.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Notifications;

/// <summary>
/// Covers telling apart the one iOS delivery failure FCM actually reports. The other one - a key that is
/// present but wrong for the bundle - leaves FCM accepting the send and the message dying at Apple, and
/// nothing on this side can see that happen; it is a deployment checklist item, not a check.
/// </summary>
public sealed class FirebaseApnsFailureTests
{
    [Fact]
    public void FCM_reporting_no_usable_APNs_key_is_recognised()
    {
        var body = """{"error":{"status":"UNAUTHENTICATED","details":[{"errorCode":"THIRD_PARTY_AUTH_ERROR"}]}}""";

        Assert.True(FirebasePushNotificationSender.IsApnsKeyMissing(HttpStatusCode.Unauthorized, body));
    }

    [Fact]
    public void An_expired_access_token_is_not_mistaken_for_it()
    {
        // Also a 401, and also about credentials - but ours, not Apple's, and pointing whoever reads the
        // message at the Firebase console would send them looking in the wrong place.
        var body = """{"error":{"status":"UNAUTHENTICATED","message":"Request had invalid authentication credentials."}}""";

        Assert.False(FirebasePushNotificationSender.IsApnsKeyMissing(HttpStatusCode.Unauthorized, body));
    }

    [Fact]
    public void An_ordinary_refusal_is_left_alone()
        => Assert.False(FirebasePushNotificationSender.IsApnsKeyMissing(HttpStatusCode.BadRequest, "malformed message"));
}
