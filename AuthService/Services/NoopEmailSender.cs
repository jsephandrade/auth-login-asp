namespace AuthService.Services;

public class NoopEmailSender : IEmailSender
{
    private readonly ILogger<NoopEmailSender> _logger;

    public NoopEmailSender(ILogger<NoopEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendVerifyEmailAsync(string email, string token, CancellationToken ct = default)
    {
        _logger.LogInformation("send verify email to {email} token={token}", email, token);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string token, CancellationToken ct = default)
    {
        _logger.LogInformation("send password reset to {email} token={token}", email, token);
        return Task.CompletedTask;
    }
}
