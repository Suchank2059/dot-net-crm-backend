namespace Crm.Api.Modules.Authentication.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    // All rotated refresh tokens from one login session share this ID.
    public Guid FamilyId { get; set; }

    // SHA-256 hex digest of the original random token, never the raw token.
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    // Absolute upper bound for this login session.
    public DateTime FamilyExpiresAtUtc { get; set; }

    public DateTime? ConsumedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    // Points to the next token after rotation.
    public Guid? ReplacedByTokenId { get; set; }

    public ApplicationUser User { get; set; } = null!;
}