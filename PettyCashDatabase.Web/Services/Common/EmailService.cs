using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using PettyCashDatabase.Web.Options;

namespace PettyCashDatabase.Web.Services;

public interface IEmailService
{
    Task SendPasswordResetAsync(string toEmail, string resetUrl, CancellationToken cancellationToken = default);
}

public sealed class EmailService : IEmailService
{
    private readonly SmtpOptions _smtp;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<SmtpOptions> smtp, ILogger<EmailService> logger)
    {
        _smtp = smtp.Value;
        _logger = logger;
    }

    public async Task SendPasswordResetAsync(string toEmail, string resetUrl, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_smtp.From));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Reset your PettyCash password";

        var body = $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;background:#f5f7f6;padding:24px;">
              <div style="max-width:520px;margin:0 auto;background:#ffffff;border-radius:8px;overflow:hidden;border:1px solid #e2e8e5;">
                <div style="background:#0B3D2E;color:#ffffff;padding:20px 24px;">
                  <h1 style="margin:0;font-size:20px;">PettyCash</h1>
                </div>
                <div style="padding:24px;color:#334155;">
                  <p>We received a request to reset your password.</p>
                  <p>
                    <a href="{resetUrl}"
                       style="display:inline-block;background:#F2B705;color:#0B3D2E;text-decoration:none;font-weight:600;padding:12px 18px;border-radius:6px;">
                      Reset password
                    </a>
                  </p>
                  <p style="font-size:13px;color:#64748b;">This link expires in 30 minutes. If you did not request it, you can ignore this email.</p>
                </div>
              </div>
            </div>
            """;

        message.Body = new TextPart("html") { Text = body };

        try
        {
            using var client = new SmtpClient();
            await client.ConnectAsync(_smtp.Host, _smtp.Port, _smtp.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_smtp.User))
                await client.AuthenticateAsync(_smtp.User, _smtp.Password, cancellationToken);

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email");
            throw new InvalidOperationException("Unable to send email at this time.");
        }
    }
}
