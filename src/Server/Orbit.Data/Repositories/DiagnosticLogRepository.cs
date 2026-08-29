using Microsoft.EntityFrameworkCore;
using Orbit.Core.Diagnostics;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class DiagnosticLogRepository : IDiagnosticLogRepository
{
    private readonly OrbitDbContext _dbContext;

    public DiagnosticLogRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        Guid userId, MobileDeviceInfo device, IReadOnlyList<DiagnosticLogEntry> entries, DateTimeOffset receivedAtUtc,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return;
        }

        _dbContext.DiagnosticLogEntries.AddRange(entries.Select(entry => new DiagnosticLogEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ReceivedAtUtc = receivedAtUtc,
            TimestampUtc = entry.TimestampUtc,
            Level = entry.Level,
            Message = entry.Message,
            Detail = entry.Detail,
            AppVersion = device.AppVersion,
            Platform = device.Platform,
            OperatingSystemVersion = device.OperatingSystemVersion,
            DeviceModel = device.DeviceModel
        }));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<int> DeleteReceivedBeforeAsync(DateTimeOffset olderThanUtc, CancellationToken cancellationToken)
        => _dbContext.DiagnosticLogEntries
            .Where(entry => entry.ReceivedAtUtc < olderThanUtc)
            .ExecuteDeleteAsync(cancellationToken);
}
