using System.IO.Compression;
using Flota.Api.Auth;
using Flota.Api.Endpoints;
using Flota.Api.Hubs;
using Flota.Api.Infrastructure.Data;
using Flota.Api.Infrastructure.Geocoding;
using Flota.Api.Infrastructure.Targeo;
using Flota.Api.Infrastructure.UniAvl;
using Flota.Api.Services;
using Flota.Api.Downloads;
using Flota.Api.WebApp;
using Flota.Core.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture));

// Konfiguracja
builder.Services.Configure<UniAvlOptions>(builder.Configuration.GetSection(UniAvlOptions.Section));
builder.Services.Configure<TargeoOptions>(builder.Configuration.GetSection(TargeoOptions.Section));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.Section));
builder.Services.Configure<ReportOptions>(builder.Configuration.GetSection(ReportOptions.Section));
builder.Services.Configure<LiveOptions>(builder.Configuration.GetSection(LiveOptions.Section));
builder.Services.AddSingleton(TimeProvider.System);

// JSON: kontrakty z generatora źródłowego
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, FlotaJsonContext.Default));

// Baza: SQLite (środowisko deweloperskie) lub PostgreSQL (produkcja)
var connection = builder.Configuration.GetConnectionString("Flota") ?? "Data Source=App_Data/flota.db";
builder.Services.AddDbContext<FlotaDbContext>(o =>
{
    if (string.Equals(builder.Configuration["Database:Provider"], "Postgres", StringComparison.OrdinalIgnoreCase))
        o.UseNpgsql(connection);
    else
        o.UseSqlite(connection);
});

var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Flota.Api");
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));

builder.Services.AddMemoryCache(o => o.SizeLimit = 200_000);

// UniAVL: typowany HttpClient z ponawianiem, limitem czasu i wyłącznikiem obwodu (P6, P9)
builder.Services.AddHttpClient<UniAvlClient>((sp, http) =>
    {
        var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<UniAvlOptions>>().Value;
        http.BaseAddress = o.BaseUrl ?? throw new InvalidOperationException("Brak konfiguracji UniAvl:BaseUrl.");
        http.Timeout = Timeout.InfiniteTimeSpan;
    })
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        AutomaticDecompression = System.Net.DecompressionMethods.All,
        MaxConnectionsPerServer = 32,
    })
    .AddStandardResilienceHandler(o =>
    {
        var timeout = builder.Configuration.GetValue<TimeSpan?>("UniAvl:Timeout") ?? TimeSpan.FromSeconds(60);
        o.AttemptTimeout.Timeout = timeout;
        o.TotalRequestTimeout.Timeout = timeout * 3;
        o.CircuitBreaker.SamplingDuration = timeout * 2;
        o.Retry.MaxRetryAttempts = 2;
        o.Retry.DisableForUnsafeHttpMethods();
    });

builder.Services.AddSingleton<TargeoClient>();
builder.Services.AddSingleton<IReverseGeocoder>(sp => sp.GetRequiredService<TargeoClient>());
builder.Services.AddSingleton<GeocodingService>();
builder.Services.AddSingleton<SessionStore>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<RefreshCookie>();
builder.Services.AddSingleton<LiveSessionRegistry>();
builder.Services.AddScoped<UserContext>();
builder.Services.AddScoped<FleetService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHostedService<PositionPollingService>();

// Uwierzytelnianie JWT (również dla huba — token w parametrze access_token)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<TokenService>((o, tokens) =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = tokens.ValidationParameters();
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments(FleetHub.Path))
                    ctx.Token = token;
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

// CORS wyłącznie dla aplikacji przeglądarkowej spod innego adresu (środowisko deweloperskie);
// w produkcji aplikacja jest udostępniana przez Flota.Api pod tym samym adresem (T9.4)
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
if (corsOrigins.Length > 0)
    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

builder.Services.AddSignalR().AddJsonProtocol(o => o.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, FlotaJsonContext.Default));

builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
    o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/json"]);
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<FlotaDbContext>();
builder.Services.AddProblemDetails();

var otel = builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation())
    .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());
if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
    otel.UseOtlpExporter();

var app = builder.Build();

// Odwrotne proxy: schemat i adres klienta z nagłówków X-Forwarded-* wyłącznie od zaufanych adresów (T9.2)
app.UseForwardedHeaders(ProxyHeaders.Options(app.Configuration));

// Komunikaty dla użytkownika bez szczegółów technicznych (B4); szczegóły w dzienniku
app.UseExceptionHandler(e => e.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    (ctx.Response.StatusCode, var message) = ex switch
    {
        UniAvlUnauthorizedException => (StatusCodes.Status401Unauthorized, "Sesja wygasła. Zaloguj się ponownie."),
        UniAvlException or HttpRequestException or TaskCanceledException => (StatusCodes.Status502BadGateway, "Usługa UniAVL jest chwilowo niedostępna."),
        _ => (StatusCodes.Status500InternalServerError, "Wystąpił błąd serwera."),
    };
    await ctx.Response.WriteAsJsonAsync(new ErrorResponse(message), FlotaJsonContext.Default.ErrorResponse);
}));

app.UseSerilogRequestLogging();
app.UseResponseCompression();
if (corsOrigins.Length > 0)
    app.UseCors();
// Aplikacja przeglądarkowa (faza 9): pliki statyczne z katalogu WebApp:RootPath
app.TryUseFlotaWebApp();
// Pliki do pobrania (pakiet Android) z katalogu Downloads:Path pod /pobierz/ (T9.5)
app.UseFlotaDownloads();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health");
app.MapFlotaApi();
app.MapFlotaDownloads();
app.MapHub<FleetHub>(FleetHub.Path);

// Schemat bazy (migracje po rozstrzygnięciu K6 — dostawca bazy produkcyjnej)
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FlotaDbContext>();
    if (db.Database.IsSqlite())
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connection).DataSource)) ?? ".");
    await db.Database.EnsureCreatedAsync();
}

await app.RunAsync();

/// <summary>Punkt wejścia (dostęp dla testów integracyjnych).</summary>
public partial class Program;
