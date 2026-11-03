using Microsoft.AspNetCore.Identity;

namespace Crm.Api.Modules.Authentication.Entities;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

