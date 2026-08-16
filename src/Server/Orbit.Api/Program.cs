using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Orbit.Api;
using Orbit.Api.Auth;
using Orbit.Api.Calendar;
using Orbit.Api.HealthChecks;
using Orbit.Api.Notes;
using Orbit.Api.Tasks;
using Orbit.Core;
using Orbit.Core.Abstractions;
using Orbit.Data;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

// Standard OpenTelemetry env var name, so both this sink and the SDK exporter below pick it up the
// same way. Defaults to Aspire Dashboard's local port; docker-compose overrides it to the dashboard's
// service name, since "localhost" inside a container means the container itself, not the host.
var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:18889";
const string serviceName = "Orbit.Api";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Verbose() // lowest level: log everything until there's a reason to narrow it down
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/orbit-api-.log", rollingInterval: RollingInterval.Day)
    .WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = otlpEndpoint;
        options.Protocol = OtlpProtocol.Grpc;
        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = serviceName
        };
    })
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();
    builder.Logging.SetMinimumLevel(LogLevel.Trace);

    // Comma-separated list, configurable via the WebClientOrigins environment variable, since the
    // Blazor client's origin is different for `dotnet run` (fixed dev ports) vs. its nginx container
    // (see docker-compose.yml).
    var webClientOrigins = (builder.Configuration["WebClientOrigins"] ?? "http://localhost:5081,https://localhost:7081")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    const string webClientCorsPolicy = "WebClient";
    builder.Services.AddCors(options =>
    {
        options.AddPolicy(webClientCorsPolicy, policy => policy
            .WithOrigins(webClientOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
    });

    builder.Services.AddOrbitCore();
    builder.Services.AddOrbitData(builder.Configuration);
    builder.Services.AddOrbitHealthChecks(builder.Configuration);

    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
    builder.Services.AddSingleton<TokenService>();
    builder.Services.AddScoped<RefreshTokenService>();
    builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();

    // Fails fast on startup instead of on the first login attempt if the signing key was never
    // configured, or is too short to be a usable HMAC-SHA256 key - see JwtSettings for where it's
    // supposed to come from.
    var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
    if (string.IsNullOrWhiteSpace(jwtSettings.SigningKey))
    {
        throw new InvalidOperationException(
            "Jwt:SigningKey is not configured. Set the JWT_SIGNING_KEY environment variable (see " +
            ".env.example) when running via Docker Compose, or run " +
            "`dotnet user-secrets set \"Jwt:SigningKey\" \"<a long random string>\"` for local `dotnet run`.");
    }
    if (jwtSettings.SigningKey.Length < 32)
    {
        throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters long.");
    }

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            // Keeps the JWT's own claim names ("sub", "email", ...) instead of ASP.NET Core's default
            // remapping to legacy XML-namespace claim URIs, so endpoint code can read claims by the
            // same names TokenService issued them under.
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });
    builder.Services.AddAuthorization();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        // Brute-force protection for /api/auth/*: 5 requests per minute per client IP, with no
        // queueing, so a client that exceeds this gets an immediate 429 instead of waiting. Partitioned
        // by IP address (rather than one limiter shared by every caller) so one aggressive client can't
        // also lock out every other client hitting these endpoints.
        options.AddPolicy(RateLimiterPolicyNames.Auth, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    });

    // Traces every incoming HTTP request, every outgoing HttpClient call, and every command/query
    // dispatched through Orbit.Core's "Orbit.Core" ActivitySource (see LoggingDispatcher), so a
    // single user click shows up as one connected trace: HTTP request -> command -> its timing.
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(serviceName))
        .WithTracing(tracing => tracing
            .AddSource("Orbit.Core")
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint)));

    var app = builder.Build();

    // Applies any pending EF Core migrations on startup - creates the database on first run and brings
    // an existing one up to date on later runs. Unlike EnsureCreated (the previous approach here), this
    // requires migration files to exist under Orbit.Data/Migrations; see README.md for the
    // `dotnet ef migrations add` command to run after changing the EF Core model.
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<OrbitDbContext>();
        dbContext.Database.Migrate();
    }

    app.UseSerilogRequestLogging();
    app.UseCors(webClientCorsPolicy);
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapAuthEndpoints();
    app.MapNoteEndpoints();
    app.MapTaskEndpoints();
    app.MapCalendarEndpoints();
    app.MapHealthEndpoints();

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Orbit.Api terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
