using Orbit.Core.Abstractions;

namespace Orbit.Core.Transfer.ImportArchive;

[ClientAction(ClientActionCategory.Edit)]
public sealed record ImportArchiveCommand(Guid UserId, OrbitArchive Archive) : IRequest<ImportArchiveResult>;

/// <summary>How much of the file landed. Counts rather than ids, since nothing here is addressable afterwards by anything the file said.</summary>
/// <param name="Places">
/// Defaulted, and last, so a client built before places were imported still reads this answer. The
/// places actually made rather than the places in the file - see ImportArchiveCommandHandler for the one
/// it leaves out.
/// </param>
public sealed record ImportArchiveResult(int Notes, int TaskLists, int CalendarEvents, int Inventories, int Places = 0);
