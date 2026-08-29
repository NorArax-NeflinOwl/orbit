using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Orbit.Mobile.Data;

/// <summary>
/// Exists so `dotnet ef migrations add` can build the model without an app around it. The connection
/// string is never opened - the tooling only needs to know the provider, since that is what decides the
/// SQL a migration is written in.
/// </summary>
internal sealed class DesignTimeLocalDbContextFactory : IDesignTimeDbContextFactory<OrbitLocalDbContext>
{
    public OrbitLocalDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<OrbitLocalDbContext>()
            .UseSqlite("Data Source=design-time-only.db3")
            .Options);
}
