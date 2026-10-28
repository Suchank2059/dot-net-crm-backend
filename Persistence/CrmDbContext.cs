using Microsoft.EntityFrameworkCore;

namespace backend.Persistence;

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options) : DbContext(options);
