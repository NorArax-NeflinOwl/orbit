using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Orbit.Contracts.Folders;
using Orbit.Core.Abstractions;
using Orbit.Core.Folders;
using Orbit.Core.Folders.CreateFolder;
using Orbit.Core.Folders.DeleteFolder;
using Orbit.Core.Folders.GetFolders;
using Orbit.Core.Folders.RenameFolder;

namespace Orbit.Api.Folders;

/// <summary>
/// The tabs the pages made of cards are drawn under. Only folders somebody made themselves are here:
/// the three built-in ones have no row and are the client's own drawing - see
/// Orbit.Core.Folders.BuiltInFolder for why they are decided rather than stored.
///
/// Nothing here is gated on a permission. A folder is a place to put your own things, so an account that
/// can keep a note can keep it somewhere.
/// </summary>
public static class FolderEndpoints
{
    public static void MapFolderEndpoints(this WebApplication app)
    {
        // Every folder belongs to exactly one account, and none is ever shared - see Folder.
        var folders = app.MapGroup("/api/folders").RequireAuthorization();

        folders.MapGet("/", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new GetFoldersQuery(GetUserId(user)), cancellationToken);
            return Results.Ok(result.Select(ToDto));
        });

        folders.MapPost("/", async (
            CreateFolderRequest request, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var folder = await dispatcher.SendAsync(new CreateFolderCommand(GetUserId(user), request.Name), cancellationToken);
            // The whole folder rather than its id: the page that asked for it has a tab to draw, and it
            // would only have to ask again for the name it just sent.
            return Results.Created($"/api/folders/{folder.Id}", ToDto(folder));
        });

        folders.MapPut("/{id:guid}", async (
            Guid id, RenameFolderRequest request, ClaimsPrincipal user, IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var renamed = await dispatcher.SendAsync(new RenameFolderCommand(GetUserId(user), id, request.Name), cancellationToken);
            return renamed ? Results.NoContent() : Results.NotFound();
        });

        // Removes the tab and keeps everything under it - see IFolderRepository.DeleteAsync.
        folders.MapDelete("/{id:guid}", async (
            Guid id, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var deleted = await dispatcher.SendAsync(new DeleteFolderCommand(GetUserId(user), id), cancellationToken);
            return deleted ? Results.NoContent() : Results.NotFound();
        });
    }

    private static FolderDto ToDto(Folder folder)
        => new(folder.Id, folder.Name, folder.CreatedAtUtc, folder.UpdatedAtUtc);

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated request is missing a 'sub' claim.");
        return Guid.Parse(subject);
    }
}
