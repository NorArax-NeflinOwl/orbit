using Orbit.Api.LiveUpdates;
using Orbit.Core.LiveUpdates;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Orbit.Api;
using Orbit.Api.Assistant;
using Orbit.Api.Auth;
using Orbit.GoogleIntegration;
using Orbit.Api.Calendar;
using Orbit.Api.Chat;
using Orbit.Api.Config;
using Orbit.Api.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Orbit.Api.HealthChecks;
using Orbit.Api.Instances;
using Orbit.Api.Permissions;
using Orbit.Api.Telemetry;
using Orbit.Api.Sharing;
using Orbit.Api.Inventories;
using Orbit.Api.Notes;
using Orbit.Api.Notifications;
using Orbit.Api.PushNotifications;
using Orbit.Api.RateLimiting;
using Orbit.Api.Suggestions;
using Orbit.Api.Tasks;
using Orbit.Api.Transfer;
using Orbit.Api.Users;
using Orbit.Core;
using Orbit.Core.Abstractions;
using Orbit.Core.Assistant;
using Orbit.Core.Notifications;
using Orbit.Core.Permissions;
using Orbit.Core.Users;
using Orbit.Data;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.ApplicationInsights.TelemetryConverters;
using Serilog.Sinks.OpenTelemetry;
using Azure.Monitor.OpenTelemetry.Exporter;

// Standard OpenTelemetry env var name, so both this sink and the SDK exporter below pick it up the
// same way. Defaults to Aspire Dashboard's local port; docker-compose overrides it to the dashboard's
// service name, since "localhost" inside a container means the container itself, not the host.
var otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:18889";
var applicationInsightsConnectionString = Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
const string serviceName = "Orbit.Api";

// Read directly rather than waiting for WebApplicationBuilder (built below), since this logger needs
// to exist before that - matches the env var ASPNETCORE_ENVIRONMENT itself sets. Development keeps the
// detail that helps locally (every EF Core query, with the SQL and how long it took); production keeps
// only what someone would read after the fact, so the handful of lines that matter - a failed login, a
// dropped e-mail - aren't buried under a constant flood from background services and health probes.
var isDevelopment = string.Equals(
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase);

var loggerConfiguration = new LoggerConfiguration()
    .MinimumLevel.Is(isDevelopment ? LogEventLevel.Debug : LogEventLevel.Information)
    // EF Core narrates each query in around ten lines - creating a command, opening a connection,
    // executing, disposing the reader, closing again - and prints the SQL twice, once before and once
    // after. Only the "Executed DbCommand" line carries anything (the SQL plus its duration), and only
    // locally: in production it is one flood per request answering a question nobody asked.
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .MinimumLevel.Override(
        "Microsoft.EntityFrameworkCore.Database.Command",
        isDevelopment ? LogEventLevel.Information : LogEventLevel.Warning)
    // UseSerilogRequestLogging (below) already writes one line per request with its method, path,
    // status and duration. The framework's own "Request starting", "Executing endpoint", "Executed
    // endpoint", "Setting HTTP status code" and "Request finished" repeat that five more times.
    .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Http", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    // Bounded on purpose: an unbounded daily file reached 493 MB in a single day of local use, which
    // is both unreadable and a directory nobody notices filling their disk. Rolling on size as well as
    // by day keeps any one file openable, and 14 files is more history than anyone reads locally.
    .WriteTo.File(
        "logs/orbit-api-.log", rollingInterval: RollingInterval.Day,
        fileSizeLimitBytes: 50 * 1024 * 1024, rollOnFileSizeLimit: true, retainedFileCountLimit: 14);

// Where the log lines go beyond this machine, and the same either/or the traces make below: a
// deployment with Application Insights sends them there, everything else sends them to whatever is
// listening on the OTLP endpoint - locally, the Aspire dashboard.
//
// Application Insights does not read OTLP, so on Azure the OTLP sink was writing into a port nothing
// answered on: the traces arrived and the words written alongside them did not, leaving the container's
// console - which goes away with the container - as the only place a log line could be read.
if (!string.IsNullOrEmpty(applicationInsightsConnectionString))
{
    // Traces, not events: a log line is a sentence about something that happened, which is what App
    // Insights calls a trace. TelemetryConverter.Events would file each one as a custom event, which is
    // for things being counted rather than things being read.
    loggerConfiguration.WriteTo.ApplicationInsights(applicationInsightsConnectionString, TelemetryConverter.Traces);
}
else
{
    loggerConfiguration.WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = otlpEndpoint;
        options.Protocol = OtlpProtocol.Grpc;
        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = serviceName
        };
    });
}

Log.Logger = loggerConfiguration.CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();
    builder.Logging.SetMinimumLevel(builder.Environment.IsDevelopment() ? LogLevel.Debug : LogLevel.Information);

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

    builder.Services.AddOrbitInstanceNotices(builder.Configuration);
    builder.Services.AddOrbitLiveUpdates();
    builder.Services.AddOrbitCore();
    builder.Services.AddOrbitData(builder.Configuration);
    builder.Services.AddOrbitHealthChecks(builder.Configuration);

    // One place decides what a refused request looks like - see InvalidRequestExceptionHandler.
    builder.Services.AddExceptionHandler<InvalidRequestExceptionHandler>();

    // A body that leaves out a required field, or sends null for one, is a bad request - and used to be
    // a 500. Every request record here is a positional record of non-nullable values, so the binder is
    // the right place to say so: without these, a missing field arrived as null and the handler
    // dereferenced it, which told the caller only that something had gone wrong on the server.
    //
    // Both flags are needed and neither covers the other: RespectRequiredConstructorParameters catches
    // a field that isn't there, RespectNullableAnnotations catches one that is there and null.
    builder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.RespectRequiredConstructorParameters = true;
        options.SerializerOptions.RespectNullableAnnotations = true;
    });

    // Calendar event reminder emails (see CalendarEventReminderBackgroundService). SmtpEmailSender
    // itself just logs a warning and skips sending when Smtp:Host/Smtp:FromAddress aren't configured,
    // rather than failing startup - a fresh local checkout should still run without email set up.
    builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
    builder.Services.AddHostedService<CalendarEventReminderBackgroundService>();

    // The address a shared-item email's link points at - see IWebClientLinks. Optional: unset (the
    // common case on a fresh local checkout) just means an email that shared item sends carries no link.
    builder.Services.AddSingleton<IWebClientLinks, WebClientLinks>();

    // Push notifications for approaching events (above), new chat messages (see
    // SendMessageCommandHandler) and overdue tasks (below). VapidPushNotificationSender itself just
    // logs a warning and skips sending when Vapid:PublicKeyBase64Url/PrivateKeyBase64Url/Subject aren't
    // configured, rather than failing startup - a fresh local checkout should still run without a VAPID
    // key pair set up.
    builder.Services.Configure<VapidSettings>(builder.Configuration.GetSection("Vapid"));
    builder.Services.AddSingleton<WebPush.WebPushClient>();
    builder.Services.AddSingleton<IPushNotificationSender, VapidPushNotificationSender>();
    // Firebase reaches the Orbit.Maui apps; PushNotificationDispatcher picks between the two by
    // transport. Unconfigured, it logs and skips exactly as the VAPID sender does.
    builder.Services.Configure<FirebaseSettings>(builder.Configuration.GetSection(FirebaseSettings.SectionName));
    builder.Services.AddHttpClient<FirebaseAccessTokenProvider>();
    builder.Services.AddHttpClient<FirebasePushNotificationSender>();
    builder.Services.AddSingleton<IPushNotificationSender>(services =>
        services.GetRequiredService<FirebasePushNotificationSender>());
    builder.Services.AddHostedService<OverdueTaskNotificationBackgroundService>();
    builder.Services.AddHostedService<DailyTaskReminderBackgroundService>();
    builder.Services.AddHostedService<InventoryExpiryReminderBackgroundService>();
    builder.Services.AddHostedService<NotificationRetentionBackgroundService>();
    builder.Services.AddHostedService<DiagnosticLogRetentionBackgroundService>();

    // The assistant's language model - Ollama locally, Azure AI Foundry in production, the same client
    // either way (see info/ai-assistant-plan.md). Unconfigured, AssistantChatClient says so in the log
    // the first time anything asks for it and the assistant endpoint answers 503, rather than anything
    // failing - a fresh checkout runs with no model, exactly as it runs with no SMTP server.
    builder.Services.Configure<AssistantSettings>(builder.Configuration.GetSection(AssistantSettings.SectionName));
    builder.Services.AddSingleton<IAssistantChatClient, AssistantChatClient>();

    builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
    builder.Services.AddSingleton<TokenService>();
    builder.Services.AddScoped<RefreshTokenService>();
    builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
    builder.Services.AddSingleton<IVerificationCodeGenerator, VerificationCodeGenerator>();
    builder.Services.Configure<GoogleAuthSettings>(builder.Configuration.GetSection(GoogleAuthSettings.SectionName));
    builder.Services.AddSingleton<IGoogleIdentityVerifier, GoogleIdentityVerifier>();
    builder.Services.Configure<MobileVersionSettings>(builder.Configuration.GetSection(MobileVersionSettings.SectionName));
    builder.Services.Configure<DiagnosticLogSettings>(builder.Configuration.GetSection(DiagnosticLogSettings.SectionName));

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

            // A browser cannot put an Authorization header on a WebSocket handshake - the WebSocket API
            // has no way to set one - so SignalR passes the token in the query string instead, and this
            // is what reads it. Scoped to the hub's own path deliberately: a token in a URL is a token
            // in access logs and in anything that records one, so nowhere else in this API accepts a
            // credential that way. See nginx-app-locations.conf, which stops logging the query string
            // for this one path for the same reason.
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var token = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(token)
                        && context.HttpContext.Request.Path.StartsWithSegments(LiveUpdateMessages.Path))
                    {
                        context.Token = token;
                    }

                    return Task.CompletedTask;
                }
            };
        });
    builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
    builder.Services.AddAuthorization(options => options.AddPermissionPolicies());

    // Remembers, briefly, which accounts asked to be left out of the trace - see TraceOptOut.
    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<PrivacyChoiceCache>();
    builder.Services.AddSingleton<IInstanceNoticeHandler, PrivacyChoiceNoticeHandler>();
    builder.Services.AddSingleton<IRateLimitWindows, PostgresRateLimitWindows>();
    builder.Services.AddHostedService<RateLimitWindowRetentionBackgroundService>();
    builder.Services.AddRateLimiter(options => options.AddOrbitPolicies());

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

    // Every permission gets a code the first time this deployment starts without one. Starting again
    // never changes one, so a code rotated on purpose stays rotated. They are rows, so reading them back
    // is a plain query rather than a search through a build log, and changing one is an UPDATE:
    //
    //     SELECT "Permission", "Code" FROM "PermissionCodes";
    using (var scope = app.Services.CreateScope())
    {
        var codes = await scope.ServiceProvider.GetRequiredService<PermissionCodeStore>()
            .EnsureEveryPermissionHasOneAsync(CancellationToken.None);
        Log.Information(
            "{CodeCount} permission unlock codes are in place - read them with: SELECT \"Permission\", \"Code\" FROM \"PermissionCodes\";",
            codes.Count);
    }

    // First, because everything after it that looks at who is calling - the request log, the rate
    // limiter's partitions - would otherwise see the proxy instead. On Azure this container is reached
    // only through orbit-web's nginx (its own ingress is internal), and nginx appends the address it
    // derived from the forwarded chain; see nginx.azure.conf, which explains how that address is
    // arrived at and why it is trustworthy only from a known proxy.
    //
    // KnownNetworks is the Container Apps internal range, measured from the ingress address in
    // orbit-web's access log rather than assumed. Restricting it is the whole point: a request whose
    // immediate peer is not on that list is left with its own address, so X-Forwarded-For from an
    // arbitrary caller cannot decide which rate-limit bucket that caller lands in. If the header is
    // absent or the peer unrecognised, this does nothing and behaviour is exactly what it was before.
    var forwardedHeaders = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
        // One hop: nginx appends exactly one address, its own view of the caller, at the right-hand end.
        ForwardLimit = 1
    };
    forwardedHeaders.KnownIPNetworks.Clear();
    forwardedHeaders.KnownProxies.Clear();
    forwardedHeaders.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("100.100.0.0"), 16));
    app.UseForwardedHeaders(forwardedHeaders);

    app.UseSerilogRequestLogging(options =>
    {
        // The container probes /health/live every ten seconds for as long as it runs. A probe that
        // succeeded is not news, so it drops below both environments' minimum level and disappears; one
        // that failed says something, and stays.
        options.GetLevel = (httpContext, _, exception) =>
        {
            if (exception is not null || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError)
            {
                return LogEventLevel.Error;
            }

            return httpContext.Request.Path.StartsWithSegments("/health")
                ? LogEventLevel.Verbose
                : LogEventLevel.Information;
        };
    });

    // Inside the request logging above on purpose: a refused request is turned into its 400 before
    // Serilog writes the access log line, so the log records the 400 the caller actually got rather
    // than a 500 with a stack trace for what is ordinary, expected input.
    app.UseExceptionHandler(new ExceptionHandlerOptions
    {
        // Anything InvalidRequestExceptionHandler doesn't claim falls through to here and keeps the
        // empty 500 it has always produced, rather than needing a ProblemDetails body it never had.
        ExceptionHandler = _ => Task.CompletedTask
    });
    app.UseCors(webClientCorsPolicy);
    app.UseAuthentication();
    // After authentication, not before: the Auth policy partitions a signed-in caller by their user id,
    // and nobody has one until UseAuthentication has read the token. Ordered the other way the policy
    // still runs and still looks correct - it just silently falls back to the IP for every request,
    // which is the bug this ordering exists to prevent. The cost is that a bearer token is verified
    // before a request can be rejected; that is a local HMAC check, and the endpoints that carry no
    // token at all (login, register) short-circuit it anyway.
    app.UseRateLimiter();
    app.UseAuthorization();

    // After authorization, so the caller is known: an account that asked to be left out of the trace
    // has its request un-recorded here, before any endpoint runs - see TraceOptOut.
    app.UseTraceOptOut();

    app.MapAuthEndpoints();
    app.MapUserEndpoints();
    app.MapChatEndpoints();
    app.MapNoteEndpoints();
    app.MapTaskEndpoints();
    app.MapCalendarEndpoints();
    app.MapInventoryEndpoints();
    app.MapSuggestionEndpoints();
    app.MapPushNotificationEndpoints();
    app.MapNotificationEndpoints();
    app.MapConfigEndpoints();
    app.MapDiagnosticLogEndpoints();
    app.MapPublicShareEndpoints();
    app.MapTransferEndpoints();
    app.MapAssistantEndpoints();
    app.MapHealthEndpoints();
    app.MapHub<LiveUpdatesHub>(LiveUpdateMessages.Path);

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
