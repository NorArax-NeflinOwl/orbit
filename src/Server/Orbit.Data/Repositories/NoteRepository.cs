using Microsoft.EntityFrameworkCore;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class NoteRepository : INoteRepository
{
    private readonly OrbitDbContext _dbContext;

    public NoteRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Note>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        // SQLite can't translate ORDER BY on a DateTimeOffset column, so the sort has to happen in
        // memory after fetching (see the EF Core NotSupportedException this avoids).
        var entities = await _dbContext.Notes
            .AsNoTracking()
            .Where(entity => entity.UserId == userId)
            .ToListAsync(cancellationToken);

        return entities
            .OrderByDescending(entity => entity.UpdatedAtUtc)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<Note?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Notes
            .AsNoTracking()
            .FirstOrDefaultAsync(note => note.Id == id && note.UserId == userId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(Note note, CancellationToken cancellationToken)
    {
        _dbContext.Notes.Add(ToEntity(note));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Note note, CancellationToken cancellationToken)
    {
        _dbContext.Notes.Update(ToEntity(note));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Notes
            .FirstOrDefaultAsync(note => note.Id == id && note.UserId == userId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        _dbContext.Notes.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Note ToDomain(NoteEntity entity)
        => Note.FromPersistence(
            entity.Id, entity.UserId, entity.Title, entity.Content, entity.CreatedAtUtc, entity.UpdatedAtUtc,
            entity.IsShared, entity.SharedByUserName, Enum.Parse<ShareAccessLevel>(entity.AccessLevel));

    private static NoteEntity ToEntity(Note note)
        => new()
        {
            Id = note.Id,
            UserId = note.UserId,
            Title = note.Title,
            Content = note.Content,
            IsShared = note.IsShared,
            SharedByUserName = note.SharedByUserName,
            AccessLevel = note.AccessLevel.ToString(),
            CreatedAtUtc = note.CreatedAtUtc,
            UpdatedAtUtc = note.UpdatedAtUtc
        };
}
