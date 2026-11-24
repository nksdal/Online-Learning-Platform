// Gmail SMTP is a free stopgap (about 500 emails/day, may land in spam). To move to Resend later, only this file needs to change.
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using OnlineLearningPlatform.Application.Authentication;

namespace OnlineLearningPlatform.Infrastructure.Email;

/// <summary>The only adapter that communicates with the email provider.</summary>
public sealed class GmailSmtpEmailSender(
    IOptions<SmtpOptions> smtpOptions,
    IOptions<EmailOptions> emailOptions,
    ILogger<GmailSmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message.To);

        var email = new MimeMessage();
        email.From.Add(MailboxAddress.Parse(emailOptions.Value.From));
        email.To.Add(MailboxAddress.Parse(message.To));
        email.Subject = message.Subject;
        email.Body = new BodyBuilder
        {
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody,
        }.ToMessageBody();

        var settings = smtpOptions.Value;
        if (string.IsNullOrWhiteSpace(settings.UserName) || string.IsNullOrWhiteSpace(settings.Password))
        {
            throw new InvalidOperationException("Gmail SMTP requires EMAIL_USER and EMAIL_PASS to be configured.");
        }

        using var client = new MailKit.Net.Smtp.SmtpClient();
        try
        {
            var socketOptions = settings.Secure ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None;
            await client.ConnectAsync(settings.Host, settings.Port, socketOptions, cancellationToken);
            await client.AuthenticateAsync(settings.UserName, settings.Password, cancellationToken);
            await client.SendAsync(email, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Gmail SMTP failed to send an email.");
            throw;
        }
    }
}
