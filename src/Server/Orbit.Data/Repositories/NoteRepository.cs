using System.Text.Json;
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

    /// <summary>Both columns are written together or not at all, so either alone means no sealed content.</summary>
    private static EncryptedPayload? ToEncryptedPayload(string? ciphertext, string? nonce)
        // Blank counts as absent, not just null: a row half-written before EncryptedPayload started
        // checking its own parts would otherwise fail inside that check while being read, which is the
        // one place a stored row must never throw.
        => !string.IsNullOrWhiteSpace(ciphertext) && !string.IsNullOrWhiteSpace(nonce)
            ? new EncryptedPayload(ciphertext, nonce)
            : null;

    private static Note ToDomain(NoteEntity entity)
        => Note.FromPersistence(
            entity.Id, entity.UserId, entity.Title,
            JsonSerializer.Deserialize<List<NoteContentLine>>(entity.ContentJson) ?? [],
            entity.IsPrivate,
            ToEncryptedPayload(entity.EncryptedCiphertext, entity.EncryptedNonce),
            entity.CreatedAtUtc, entity.UpdatedAtUtc,
            entity.LockedByUserId, entity.LockedByUserName, entity.LockExpiresAtUtc, entity.IsPinned);

    private static NoteEntity ToEntity(Note note)
        => new()
        {
            Id = note.Id,
            UserId = note.UserId,
            Title = note.Title,
            ContentJson = JsonSerializer.Serialize(note.Content),
            IsPrivate = note.IsPrivate,
            IsPinned = note.IsPinned,
            EncryptedCiphertext = note.EncryptedContent?.Ciphertext,
            EncryptedNonce = note.EncryptedContent?.Nonce,
            LockedByUserId = note.LockedByUserId,
            LockedByUserName = note.LockedByUserName,
            LockExpiresAtUtc = note.LockExpiresAtUtc,
            CreatedAtUtc = note.CreatedAtUtc,
            UpdatedAtUtc = note.UpdatedAtUtc
        };
}
