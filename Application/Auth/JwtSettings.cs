namespace Crm.Application.Auth;

public sealed class JwtSettings
{
    public const string SectionName = "Authentication:Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKeyBase64 { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 5;

    public int RefreshTokenIdleDays { get; set; } = 7;

    public int RefreshTokenAbsoluteDays { get; set; } = 30;
}
