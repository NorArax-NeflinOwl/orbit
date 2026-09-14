using Microsoft.EntityFrameworkCore;
using Orbit.Core.Notes;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class NotePictureRepository : INotePictureRepository
{
    private readonly OrbitDbContext _dbContext;

    public NotePictureRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(NotePicture picture, CancellationToken cancellationToken)
    {
        _dbContext.NotePictures.Add(ToEntity(picture));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<NotePicture?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.NotePictures.AsNoTracking().FirstOrDefaultAsync(picture => picture.Id == id, cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<NotePicture>> GetForNoteAsync(Guid noteId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.NotePictures.AsNoTracking()
            .Where(picture => picture.NoteId == noteId)
            .OrderBy(picture => picture.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return entities.Select(ToDomain).ToList();
    }

    /// <summary>Summed in the database: the count is asked on every upload, and a note may hold many pictures.</summary>
    public Task<long> TotalBytesForNoteAsync(Guid noteId, CancellationToken cancellationToken)
        => _dbContext.NotePictures
            .Where(picture => picture.NoteId == noteId)
            .SumAsync(picture => picture.SizeBytes, cancellationToken);

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _dbContext.NotePictures.Where(picture => picture.Id == id).ExecuteDeleteAsync(cancellationToken);
    }

    private static NotePictureEntity ToEntity(NotePicture picture)
        => new()
        {
            Id = picture.Id,
            NoteId = picture.NoteId,
            OwnerUserId = picture.OwnerUserId,
            SizeBytes = picture.SizeBytes,
            ContentType = picture.ContentType,
            IsSealed = picture.IsSealed,
            CreatedAtUtc = picture.CreatedAtUtc
        };

    private static NotePicture ToDomain(NotePictureEntity entity)
        => NotePicture.Restore(
            entity.Id, entity.NoteId, entity.OwnerUserId, entity.SizeBytes, entity.ContentType, entity.IsSealed, entity.CreatedAtUtc);
}
