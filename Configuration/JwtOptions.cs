namespace Crm.Api.Configuration;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; }
    public int RefreshTokenDays { get; set; }
    public int SessionAbsoluteDays { get; set; }
}