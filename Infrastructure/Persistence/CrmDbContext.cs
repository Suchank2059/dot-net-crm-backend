using Crm.Domain.Auth;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

public sealed class CrmDbContext(
    DbContextOptions<CrmDbContext> options) : IdentityUserContext<ApplicationUser>(options)
{
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RefreshSession>(entity =>
        {
            entity.HasKey(session => session.Id);
            entity.Property(session => session.UserId).HasMaxLength(255).IsRequired();
            entity.Property(session => session.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(session => session.TokenHash).IsUnique();
            entity.HasIndex(session => new { session.UserId, session.FamilyId });
            entity.HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
