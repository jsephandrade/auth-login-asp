namespace AuthService.Services;

public class NoopEmailSender : IEmailSender
{
    private readonly ILogger<NoopEmailSender> _logger;
    private readonly IVerificationCodeDebugStore _verificationCodeStore;
    private readonly IPasswordResetCodeDebugStore _passwordResetCodeStore;

    public NoopEmailSender(
        ILogger<NoopEmailSender> logger,
        IVerificationCodeDebugStore verificationCodeStore,
        IPasswordResetCodeDebugStore passwordResetCodeStore)
    {
        _logger = logger;
        _verificationCodeStore = verificationCodeStore;
        _passwordResetCodeStore = passwordResetCodeStore;
    }

    public Task SendVerifyEmailAsync(string email, string code, CancellationToken ct = default)
    {
        _verificationCodeStore.SetCode(email, code);
        _logger.LogInformation("send verify email to {email} code={code}", email, code);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string code, CancellationToken ct = default)
    {
        _passwordResetCodeStore.SetCode(email, code);
        _logger.LogInformation("send password reset to {email} code={code}", email, code);
        return Task.CompletedTask;
    }
}
