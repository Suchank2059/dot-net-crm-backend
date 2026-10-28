namespace Crm.Infrastructure.Email;

public sealed class SmtpSettings
{
    public const string SectionName = "Email:Smtp";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 1025;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "CRM";

    public bool UseStartTls { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }
}
