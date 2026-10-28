using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Crm.Infrastructure.Persistence;

public sealed class CrmDesignTimeDbContextFactory : IDesignTimeDbContextFactory<CrmDbContext>
{
    public CrmDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__DefaultConnection before creating or applying migrations.");
        }

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseMySQL(
                connectionString,
                mysql => mysql.MigrationsAssembly(typeof(CrmDbContext).Assembly.FullName))
            .Options;

        return new CrmDbContext(options);
    }
}
