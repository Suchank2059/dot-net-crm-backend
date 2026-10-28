using Crm.Application.Auth;
using Crm.Infrastructure.Email;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "The 'ConnectionStrings:DefaultConnection' configuration value is required.");
        var keysPath = configuration["DataProtection:KeysPath"]
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".aspnet",
                "Crm",
                "DataProtection-Keys");

        Directory.CreateDirectory(keysPath);
        services.AddDataProtection()
            .SetApplicationName("Crm")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        services.AddDbContext<CrmDbContext>(options =>
            options.UseMySQL(
                connectionString,
                mysql => mysql.MigrationsAssembly(typeof(CrmDbContext).Assembly.FullName)));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Tokens.EmailConfirmationTokenProvider =
                    TokenOptions.DefaultEmailProvider;
            })
            .AddEntityFrameworkStores<CrmDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromHours(1));

        services.AddOptions<SmtpSettings>()
            .Bind(configuration.GetSection(SmtpSettings.SectionName))
            .Validate(settings =>
                !string.IsNullOrWhiteSpace(settings.Host) &&
                settings.Port is > 0 and <= 65535 &&
                !string.IsNullOrWhiteSpace(settings.FromAddress) &&
                (string.IsNullOrWhiteSpace(settings.Username) ==
                 string.IsNullOrWhiteSpace(settings.Password)),
                "Valid SMTP host, port, and from address are required.")
            .ValidateOnStart();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IEmailSender, MailKitEmailSender>();
        return services;
    }
}
