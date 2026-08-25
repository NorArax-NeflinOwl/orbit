using Orbit.Core.Abstractions;

namespace Orbit.Core.Transfer.ExportArchive;

/// <summary>Everything this user owns, as one archive - see <see cref="OrbitArchive"/>.</summary>
public sealed record ExportArchiveQuery(Guid UserId) : IRequest<OrbitArchive>;
