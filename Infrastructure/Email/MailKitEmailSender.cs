using Crm.Application.Auth;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Crm.Infrastructure.Email;

public sealed class MailKitEmailSender(IOptions<SmtpSettings> settings) : IEmailSender
{
    private readonly SmtpSettings _settings = settings.Value;

    public async Task SendAsync(
        string email,
        string subject,
        string plainTextBody,
        CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = plainTextBody };

        using var client = new SmtpClient();
        var secureSocketOptions = _settings.UseStartTls
            ? SecureSocketOptions.StartTls
            : SecureSocketOptions.None;

        await client.ConnectAsync(
            _settings.Host,
            _settings.Port,
            secureSocketOptions,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(_settings.Username))
        {
            var password = _settings.Password
                ?? throw new InvalidOperationException("SMTP password is required with a username.");
            await client.AuthenticateAsync(
                _settings.Username,
                password,
                cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
