using ECafe.Api.Middlewares;
using ECafe.Api.Swagger;
using ECafe.Application;
using ECafe.Application.Services.Jwt.Concrete;
using ECafe.Infrastructure;
using ECafe.Infrastructure.Authorization;
using ECafe.Infrastructure.Redis;
using ECafe.Shared.Services.Jwt.Abstract;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseSentry(options =>
{
    options.Dsn = builder.Configuration["Sentry:Dsn"];
    options.Environment = builder.Environment.EnvironmentName;
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

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:Connection"];
});

builder.Services.AddScoped<IJwtService, JwtManager>();
builder.Services.AddScoped<IPermissionCacheService, PermissionCacheService>();

builder.Services.AddMemoryCache();


builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
