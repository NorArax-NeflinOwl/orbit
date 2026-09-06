namespace Orbit.Api.Instances;

/// <summary>
/// Something one API instance needs to tell the others - see <see cref="PostgresInstanceNoticeListener"/>
/// for how it travels and <see cref="PostgresInstanceNoticeSender"/> for how it is sent.
///
/// Notices are best-effort and nothing here is durable. A handler may be told late, or not at all if the
/// listener was reconnecting, so a notice may only ever be an optimisation over something that is
/// already correct on its own - "you can stop waiting" or "what you cached is stale", never a fact that
/// exists nowhere else. Both of today's uses are exactly that.
/// </summary>
public interface IInstanceNoticeHandler
{
    /// <summary>The PostgreSQL channel this handler is interested in, and nothing else.</summary>
    string Channel { get; }

    /// <param name="body">The JSON the sender passed, with the envelope already stripped.</param>
    Task HandleAsync(string body, CancellationToken cancellationToken);
}
