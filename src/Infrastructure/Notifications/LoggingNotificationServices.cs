using GPMH.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace GPMH.Infrastructure.Notifications;

public sealed class LoggingEmailService(ILogger<LoggingEmailService> logger) : IEmailService
{
    public Task SendAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentNullException.ThrowIfNull(htmlBody);

        string masked = MaskEmail(recipientEmail);
        logger.LogInformation("Queued transactional email to {Recipient} with subject '{Subject}'.", masked, subject);
        return Task.CompletedTask;
    }

    private static string MaskEmail(string email)
    {
        int at = email.IndexOf('@', StringComparison.Ordinal);
        return at <= 2 ? $"***{email[Math.Max(0, at)..]}" : $"{email[..2]}***{email[at..]}";
    }
}

public sealed class LoggingSmsService(ILogger<LoggingSmsService> logger) : ISmsService
{
    public Task SendAsync(
        string mobileNumber,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mobileNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        string digits = mobileNumber.Trim();
        string masked = digits.Length > 4 ? $"******{digits[^4..]}" : "******";
        logger.LogInformation("Queued SMS notification to {Mobile} ({Length} chars).", masked, message.Length);
        return Task.CompletedTask;
    }
}

