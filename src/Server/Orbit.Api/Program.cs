using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Orbit.Api;
using Orbit.Api.Auth;
using Orbit.Api.Calendar;
using Orbit.Api.Chat;
using Orbit.Api.Config;
using Orbit.Api.HealthChecks;
using Orbit.Api.Inventory;
using Orbit.Api.Notes;
using Orbit.Api.Notifications;
using Orbit.Api.PushNotifications;
using Orbit.Api.Tasks;
using Orbit.Api.Users;
using Orbit.Core;
using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;
using Orbit.Data;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.OpenTelemetry;
using Azure.Monitor.OpenTelemetry.Exporter;

// Standard OpenTelemetry env var name, so both this sink and the SDK exporter below pick it up the
// same way. Defaults to Aspire Dashboard's local port; docker-compose overrides it to the dashboard's
// service name, since "localhost" inside a container means the container itself, not the host.
var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:18889";
var applicationInsightsConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
const string serviceName = "Orbit.Api";

// Read directly rather than waiting for WebApplicationBuilder (built below), since this logger needs
// to exist before that - matches the env var ASPNETCORE_ENVIRONMENT itself sets. Verbose is invaluable
// for local development (every EF Core query, every connection open/close) but far too noisy for a
// production log stream - it drowns out the handful of lines that actually matter (e.g. a failed
// login) under a constant flood from background services and health probes.
var isDevelopment = string.Equals(
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Is(isDevelopment ? LogEventLevel.Verbose : LogEventLevel.Information)
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
    builder.Logging.SetMinimumLevel(builder.Environment.IsDevelopment() ? LogLevel.Trace : LogLevel.Information);

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

    // Calendar event reminder emails (see CalendarEventReminderBackgroundService). SmtpEmailSender
    // itself just logs a warning and skips sending when Smtp:Host/Smtp:FromAddress aren't configured,
    // rather than failing startup - a fresh local checkout should still run without email set up.
    builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
    builder.Services.AddHostedService<CalendarEventReminderBackgroundService>();

    // Push notifications for approaching events (above), new chat messages (see
    // SendMessageCommandHandler) and overdue tasks (below). VapidPushNotificationSender itself just
    // logs a warning and skips sending when Vapid:PublicKeyBase64Url/PrivateKeyBase64Url/Subject aren't
    // configured, rather than failing startup - a fresh local checkout should still run without a VAPID
    // key pair set up.
    builder.Services.Configure<VapidSettings>(builder.Configuration.GetSection("Vapid"));
    builder.Services.AddSingleton<WebPush.WebPushClient>();
    builder.Services.AddSingleton<IPushNotificationSender, VapidPushNotificationSender>();
    builder.Services.AddHostedService<OverdueTaskNotificationBackgroundService>();
    builder.Services.AddHostedService<DailyTaskReminderBackgroundService>();
    builder.Services.AddHostedService<InventoryExpiryReminderBackgroundService>();

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
        // Brute-force protection for /api/auth/register and /api/auth/login (see AuthEndpoints for why
        // /refresh and /logout don't use this policy): 5 requests per minute per client IP, with no
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
        .WithTracing(tracing =>
        {
            tracing
                .AddSource("Orbit.Core")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation();

            if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
            {
                tracing.AddAzureMonitorTraceExporter(options =>
                    options.ConnectionString = applicationInsightsConnectionString);
            }
            else
            {
                tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
            }
        });

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
    app.MapUserEndpoints();
    app.MapChatEndpoints();
    app.MapNoteEndpoints();
    app.MapTaskEndpoints();
    app.MapCalendarEndpoints();
    app.MapInventoryEndpoints();
    app.MapPushNotificationEndpoints();
    app.MapNotificationEndpoints();
    app.MapConfigEndpoints();
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
