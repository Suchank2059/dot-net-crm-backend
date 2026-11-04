using System.Text;
using Crm.Api.Configuration;
using Crm.Api.Infrastructure.Persistance;
using Crm.Api.Modules.Authentication.Entities;
using Crm.Api.Modules.Authentication.Interfaces;
using Crm.Api.Modules.Authentication.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Api.Modules.Authentication;

public static class AuthenticationModule
{
    public static IServiceCollection AddAuthenticationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IAuthenticationService, AuthenticationService>();

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services
            .AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .Validate(options =>
                !string.IsNullOrWhiteSpace(options.SecretKey) &&
                !string.IsNullOrWhiteSpace(options.Issuer) &&
                !string.IsNullOrWhiteSpace(options.Audience) &&
                options.AccessTokenMinutes > 0 &&
                options.RefreshTokenDays > 0 &&
                options.SessionAbsoluteDays > 0,
                "JWT configuration contains invalid values.")
            .ValidateOnStart();

        var jwtOptions = configuration
            .GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is missing.");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,

                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtOptions.SecretKey)
                ),

                ClockSkew = TimeSpan.Zero
            };
        });

        return services;
    }
}