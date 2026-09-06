using System.Text.Json;
using Orbit.Api.Instances;

namespace Orbit.Api.Telemetry;

/// <summary>
/// Drops this instance's remembered privacy choice for an account when another instance reports that it
/// changed - see <see cref="PrivacyChoiceCache.ForgetEverywhereAsync"/>.
/// </summary>
public sealed class PrivacyChoiceNoticeHandler(PrivacyChoiceCache choices) : IInstanceNoticeHandler
{
    public string Channel => PrivacyChoiceCache.Channel;

    public Task HandleAsync(string body, CancellationToken cancellationToken)
    {
        var userId = JsonSerializer.Deserialize<Guid>(body);
        if (userId != Guid.Empty)
        {
            choices.Forget(userId);
        }

        return Task.CompletedTask;
    }
}
