using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

public sealed class CrmDbContext(
    DbContextOptions<CrmDbContext> options) : DbContext(options);
