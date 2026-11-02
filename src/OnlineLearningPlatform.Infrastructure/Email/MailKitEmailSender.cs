using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using OnlineLearningPlatform.Application.Authentication;

namespace OnlineLearningPlatform.Infrastructure.Email;

public sealed class MailKitEmailSender(
    IOptions<SmtpOptions> smtpOptions,
    IOptions<EmailOptions> emailOptions) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message.To);

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(emailOptions.Value.FromName, emailOptions.Value.FromAddress));
        email.To.Add(MailboxAddress.Parse(message.To));
        email.Subject = message.Subject;
        email.Body = new BodyBuilder
        {
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody,
        }.ToMessageBody();

        var settings = smtpOptions.Value;
        using var client = new MailKit.Net.Smtp.SmtpClient();
        var socketOptions = settings.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(settings.Host, settings.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(settings.UserName))
        {
            var password = settings.Password ?? throw new InvalidOperationException(
                "Email SMTP password is required when a username is configured.");
            await client.AuthenticateAsync(settings.UserName, password, cancellationToken);
        }

        await client.SendAsync(email, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
