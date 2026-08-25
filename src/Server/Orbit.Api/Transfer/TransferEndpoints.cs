using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Orbit.Core.Abstractions;
using Orbit.Core.Transfer;
using Orbit.Core.Transfer.ExportArchive;
using Orbit.Core.Transfer.ImportArchive;

namespace Orbit.Api.Transfer;

/// <summary>
/// Taking everything out of an account as one file, and putting a file back into one. The archive is
/// its own shape rather than the API's DTOs (see OrbitArchive), so a file saved months ago still opens
/// after the endpoints around it have changed.
/// </summary>
public static class TransferEndpoints
{
    public static void MapTransferEndpoints(this WebApplication app)
    {
        var transfer = app.MapGroup("/api/transfer").RequireAuthorization();

        transfer.MapGet("/export", async (ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var archive = await dispatcher.SendAsync(new ExportArchiveQuery(GetUserId(user)), cancellationToken);
            return Results.Ok(archive);
        });

        transfer.MapPost("/import", async (
            OrbitArchive archive, ClaimsPrincipal user, IDispatcher dispatcher, CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(new ImportArchiveCommand(GetUserId(user), archive), cancellationToken);
            return Results.Ok(result);
        });
    }

    private static Guid GetUserId(ClaimsPrincipal user)
    {
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new InvalidOperationException("Authenticated request is missing a 'sub' claim.");
        return Guid.Parse(subject);
    }
}
