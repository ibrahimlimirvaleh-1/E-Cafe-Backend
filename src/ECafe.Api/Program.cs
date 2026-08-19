using ECafe.Api.BackgroundServices;
using ECafe.Api.Middlewares;
using ECafe.Api.Security;
using ECafe.Api.Swagger;
using ECafe.Application;
using ECafe.Application.Services.Jwt.Concrete;
using ECafe.Infrastructure;
using ECafe.Infrastructure.Authorization;
using ECafe.Infrastructure.Redis;
using ECafe.Shared.Services.Jwt.Abstract;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Net;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsEnvironment("Local"))
{
    builder.Configuration
        .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
        .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
        .AddUserSecrets<Program>(optional: true);
}

builder.WebHost.UseSentry(options =>
{
    options.Dsn = builder.Configuration["Sentry:Dsn"];
    options.Environment = builder.Environment.EnvironmentName;
    options.Debug = builder.Configuration.GetValue<bool>("Sentry:Debug");
    options.EnableLogs = builder.Configuration.GetValue<bool>("Sentry:EnableLogs");
    options.TracesSampleRate = builder.Configuration.GetValue<double?>("Sentry:TracesSampleRate") ?? 0.0;
});

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "ECafe API",
        Version = "v1",
        Description = """
        ECafe restoran kataloqu, stol/ofisiant seçimi, menyu və depozitli rezervasiya axınları üçün backend API-dir.

        Swagger istifadə qaydası:
        1. Login endpoint-i ilə token al.
        2. Authorize düyməsinə `Bearer {token}` formatında token əlavə et.
        3. Search/filter sahəsi ilə modul və endpoint tap.
        """,
        Contact = new OpenApiContact
        {
            Name = "ECafe Backend",
            Email = "support@ecafe.local"
        }
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header istifadə et. Nümunə: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    options.OperationFilter<EcafeSwaggerOperationFilter>();
    options.DocumentFilter<EcafeSwaggerDocumentFilter>();
    options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
});

builder.Services.AddHttpContextAccessor();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Local"))
    {
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
        return;
    }

    var knownProxies = builder.Configuration
        .GetSection("ForwardedHeaders:KnownProxies")
        .Get<string[]>() ?? [];

    foreach (var proxy in knownProxies)
    {
        if (IPAddress.TryParse(proxy, out var address))
            options.KnownProxies.Add(address);
    }
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("EcafeCors", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();

            return;
        }

        if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Local"))
        {
            policy.WithOrigins(
                    "http://localhost:5173",
                    "http://127.0.0.1:5173",
                    "http://localhost:8081",
                    "http://127.0.0.1:8081")
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var jwtKey = GetRequiredConfigurationValue(builder.Configuration, "Jwt:Key");
var jwtIssuer = GetRequiredConfigurationValue(builder.Configuration, "Jwt:Issuer");
var jwtAudience = GetRequiredConfigurationValue(builder.Configuration, "Jwt:Audience");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.IncludeErrorDetails = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey))
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";

                var response = new
                {
                    statusCode = StatusCodes.Status401Unauthorized,
                    code = "Unauthorized",
                    message = "Sessiya etibarsızdır və ya vaxtı bitib. Zəhmət olmasa yenidən daxil olun.",
                    traceId = context.HttpContext.TraceIdentifier,
                    timestamp = DateTime.UtcNow
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddEcafeRateLimiting(builder.Configuration);

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:Connection"];
});

builder.Services.AddScoped<IJwtService, JwtManager>();
builder.Services.AddScoped<IPermissionCacheService, PermissionCacheService>();

builder.Services.AddMemoryCache();

builder.Services.AddHostedService<UnattachedFileCleanupService>();
builder.Services.AddHostedService<AuditOutboxWorker>();
builder.Services.AddHostedService<EmailOutboxWorker>();
builder.Services.AddHostedService<ContractExpiryWorker>();

builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ActiveRestaurantContractAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseForwardedHeaders();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Local"))
{
    app.UseSwagger();
    app.UseStaticFiles();
    app.UseSwaggerUI(options =>
    {
        options.DocumentTitle = "ECafe API Explorer";
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ECafe API v1");
        options.RoutePrefix = "swagger";
        options.EnableFilter();
        options.DisplayRequestDuration();
        options.EnablePersistAuthorization();
        options.DocExpansion(DocExpansion.None);
        options.DefaultModelsExpandDepth(1);
        options.DefaultModelRendering(ModelRendering.Example);
        options.InjectStylesheet("/swagger-ui/ecafe-swagger.css?v=20260714-3");
        options.InjectJavascript("/swagger-ui/ecafe-swagger.js?v=20260714-3");
    });
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseCors("EcafeCors");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.Run();

static string GetRequiredConfigurationValue(IConfiguration configuration, string key)
{
    var value = configuration[key];

    if (string.IsNullOrWhiteSpace(value))
        throw new InvalidOperationException($"Required configuration value '{key}' is missing.");

    return value;
}
