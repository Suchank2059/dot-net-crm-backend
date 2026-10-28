using System.Threading.RateLimiting;
using Asp.Versioning;
using Crm.Application.Auth;
using Crm.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NSwag;
using NSwag.Generation.Processors.Security;

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = builder.Configuration
    .GetSection(JwtSettings.SectionName)
    .Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings are required.");

byte[] signingKey;
try
{
    signingKey = Convert.FromBase64String(jwtSettings.SigningKeyBase64);
}
catch (FormatException exception)
{
    throw new InvalidOperationException(
        "Authentication:Jwt:SigningKeyBase64 must be a Base64-encoded random key.",
        exception);
}

if (signingKey.Length < 32)
{
    throw new InvalidOperationException(
        "Authentication:Jwt:SigningKeyBase64 must decode to at least 32 bytes.");
}

builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .Validate(settings =>
        !string.IsNullOrWhiteSpace(settings.Issuer) &&
        !string.IsNullOrWhiteSpace(settings.Audience) &&
        settings.AccessTokenMinutes is > 0 and <= 15 &&
        settings.RefreshTokenIdleDays > 0 &&
        settings.RefreshTokenAbsoluteDays >= settings.RefreshTokenIdleDays,
        "JWT issuer, audience, and valid token lifetimes are required.")
    .ValidateOnStart();

builder.Services.AddControllers();
builder.Services.AddOpenApiDocument(options =>
{
    options.DocumentName = "v1";
    options.ApiGroupNames = ["v1"];
    options.Title = "CRM API";
    options.Version = "v1";
    options.AddSecurity("Bearer", new OpenApiSecurityScheme
    {
        Type = OpenApiSecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Short-lived access token returned by the sign-in endpoint."
    });
    options.OperationProcessors.Add(
        new AspNetCoreOperationSecurityScopeProcessor("Bearer"));
});
builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
        options.AssumeDefaultVersionWhenUnspecified = false;
        options.ReportApiVersions = true;
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = builder.Environment.IsDevelopment()
        ? "crm-antiforgery"
        : "__Host-crm-antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.Path = "/";
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? [];
if (allowedOrigins.Length == 0)
{
    throw new InvalidOperationException(
        "Configure at least one exact Cors:AllowedOrigins value before starting the API.");
}

builder.Services.AddCors(options =>
    options.AddPolicy("frontend", policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKey),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRouting();
app.UseCors("frontend");
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseOpenApi();
    app.UseSwaggerUi();
}

app.UseAuthentication();
app.UseAuthorization();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.MapControllers();

app.Run();

public partial class Program;
