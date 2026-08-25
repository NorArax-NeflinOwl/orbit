using Orbit.Core.Abstractions;

namespace Orbit.Core.Transfer.ImportArchive;

[ClientAction(ClientActionCategory.Edit)]
public sealed record ImportArchiveCommand(Guid UserId, OrbitArchive Archive) : IRequest<ImportArchiveResult>;

/// <summary>How much of the file landed. Counts rather than ids, since nothing here is addressable afterwards by anything the file said.</summary>
public sealed record ImportArchiveResult(int Notes, int TaskLists, int CalendarEvents, int Warehouses);
