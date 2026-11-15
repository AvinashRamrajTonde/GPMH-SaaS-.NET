namespace GPMH.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendAsync(string recipientEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}

public interface ISmsService
{
    Task SendAsync(string mobileNumber, string message, CancellationToken cancellationToken = default);
}

