using Crm.Infrastructure.Persistence;
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

        services.AddDbContext<CrmDbContext>(options =>
            options.UseMySQL(
                connectionString,
                mysql => mysql.MigrationsAssembly(typeof(CrmDbContext).Assembly.FullName)));

        return services;
    }
}
