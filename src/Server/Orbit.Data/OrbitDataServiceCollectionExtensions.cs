using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orbit.Core.Notes;
using Orbit.Core.Tasks;
using Orbit.Core.Users;
using Orbit.Data.Repositories;

namespace Orbit.Data;

public static class OrbitDataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the SQLite-backed persistence layer and its repositories. SQLite is a prototype
    /// choice for zero-setup local development; swapping to another provider only touches this method.
    /// </summary>
    public static IServiceCollection AddOrbitData(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Orbit") ?? "Data Source=orbit.db";

        services.AddDbContext<OrbitDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }
}
