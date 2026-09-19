using Orbit.Core.Abstractions;

namespace Orbit.Core.Location.ResolveMapLink;

/// <summary>
/// Where a link to somebody else's map points - the answer a client cannot work out for itself when the
/// link is a shortened one. See MapLinks, which reads every other kind without asking anybody.
/// </summary>
public sealed record ResolveMapLinkQuery(string Url) : IRequest<MapLink?>;
