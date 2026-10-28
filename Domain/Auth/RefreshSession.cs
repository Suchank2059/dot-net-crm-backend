namespace Crm.Domain.Auth;

public sealed class RefreshSession
{
    public Guid Id { get; set; }

    public Guid FamilyId { get; set; }

    public required string UserId { get; set; }

    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset AbsoluteExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? ReplacedBySessionId { get; set; }
}
