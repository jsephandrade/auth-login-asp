using AuthService.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;

namespace AuthService.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendVerifyEmailAsync(string email, string token, CancellationToken ct = default)
    {
        var link = BuildLink(_options.VerifyEmailUrlTemplate, email, token);
        var body = $"Verify your email for Print Shop: {link}";
        await SendAsync(email, "Verify your email", body, ct);
    }

    public async Task SendPasswordResetAsync(string email, string token, CancellationToken ct = default)
    {
        var link = BuildLink(_options.ResetPasswordUrlTemplate, email, token);
        var body = $"Reset your Print Shop password: {link}";
        await SendAsync(email, "Reset your password", body, ct);
    }

    private async Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("plain") { Text = body };

        using var client = new SmtpClient();
        var secure = ResolveSocketMode();
        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, secure, ct);

        if (!string.IsNullOrWhiteSpace(_options.SmtpUser))
        {
            await client.AuthenticateAsync(_options.SmtpUser, _options.SmtpPass, ct);
        }

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
        _logger.LogInformation("Email sent to {email}", toEmail);
    }

    private SecureSocketOptions ResolveSocketMode()
    {
        if (_options.SmtpPort == 465)
        {
            return SecureSocketOptions.SslOnConnect;
        }

        if (_options.UseSsl)
        {
            return SecureSocketOptions.StartTls;
        }

        return SecureSocketOptions.StartTlsWhenAvailable;
    }

    private static string BuildLink(string template, string email, string token)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return token;
        }

        return template
            .Replace("{token}", Uri.EscapeDataString(token), StringComparison.OrdinalIgnoreCase)
            .Replace("{email}", Uri.EscapeDataString(email), StringComparison.OrdinalIgnoreCase);
    }
}
