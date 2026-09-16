using Microsoft.Extensions.Options;
using Orbit.Core.Notes;

namespace Orbit.Api.Notes.Pictures;

/// <summary>
/// The bytes of a note's pictures as files in a directory - local development's store, where a storage
/// emulator would be one more container for the sake of a feature the database does not need. One file
/// per picture, named by its id and nothing else: what a file holds is the row's to say, and a sealed
/// picture's file says nothing at all about what it is.
/// </summary>
public sealed class DirectoryNotePictureStore : INotePictureStore
{
    private readonly string _directory;

    public DirectoryNotePictureStore(IOptions<NotePictureSettings> settings, IHostEnvironment environment)
    {
        _directory = string.IsNullOrWhiteSpace(settings.Value.Directory)
            ? Path.Combine(environment.ContentRootPath, "pictures")
            : settings.Value.Directory;
    }

    public async Task<long> WriteAsync(Guid pictureId, Stream content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_directory);
        await using var file = File.Create(PathOf(pictureId));
        await content.CopyToAsync(file, cancellationToken);
        return file.Length;
    }

    public Task<Stream?> OpenAsync(Guid pictureId, CancellationToken cancellationToken)
    {
        var path = PathOf(pictureId);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.OpenRead(path) : null);
    }

    public Task DeleteAsync(Guid pictureId, CancellationToken cancellationToken)
    {
        File.Delete(PathOf(pictureId));
        return Task.CompletedTask;
    }

    /// <summary>The id's own digits and nothing from outside - so nothing a request carries can name a path.</summary>
    private string PathOf(Guid pictureId) => Path.Combine(_directory, pictureId.ToString("N"));
}
