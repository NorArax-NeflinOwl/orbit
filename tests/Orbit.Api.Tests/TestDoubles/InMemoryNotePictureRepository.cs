using Orbit.Core.Notes;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>In-memory <see cref="INotePictureRepository"/> for the handler tests - the rows, as the database keeps them.</summary>
internal sealed class InMemoryNotePictureRepository : INotePictureRepository
{
    private readonly List<NotePicture> _pictures = [];

    public Task AddAsync(NotePicture picture, CancellationToken cancellationToken)
    {
        _pictures.Add(picture);
        return Task.CompletedTask;
    }

    public Task<NotePicture?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_pictures.FirstOrDefault(picture => picture.Id == id));

    public Task<IReadOnlyList<NotePicture>> GetForNoteAsync(Guid noteId, CancellationToken cancellationToken)
    {
        IReadOnlyList<NotePicture> forNote = _pictures.Where(picture => picture.NoteId == noteId).ToList();
        return Task.FromResult(forNote);
    }

    public Task<long> TotalBytesForNoteAsync(Guid noteId, CancellationToken cancellationToken)
        => Task.FromResult(_pictures.Where(picture => picture.NoteId == noteId).Sum(picture => picture.SizeBytes));

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        _pictures.RemoveAll(picture => picture.Id == id);
        return Task.CompletedTask;
    }
}

/// <summary>In-memory <see cref="INotePictureStore"/>: the bytes, by id, so a test can see what was written and what was swept.</summary>
internal sealed class InMemoryNotePictureStore : INotePictureStore
{
    public Dictionary<Guid, byte[]> Bytes { get; } = [];

    public async Task<long> WriteAsync(Guid pictureId, Stream content, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        Bytes[pictureId] = buffer.ToArray();
        return buffer.Length;
    }

    public Task<Stream?> OpenAsync(Guid pictureId, CancellationToken cancellationToken)
        => Task.FromResult<Stream?>(Bytes.TryGetValue(pictureId, out var bytes) ? new MemoryStream(bytes) : null);

    public Task DeleteAsync(Guid pictureId, CancellationToken cancellationToken)
    {
        Bytes.Remove(pictureId);
        return Task.CompletedTask;
    }
}
