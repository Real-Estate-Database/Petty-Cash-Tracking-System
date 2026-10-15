using Microsoft.Extensions.Logging;

namespace PettyCashDatabase.Web.Services;

/// <summary>
/// Development-grade email service that outputs transactional email contents and clickable URLs
/// directly to the console/terminal and application logger without requiring remote SMTP infrastructure.
/// </summary>
public sealed class ConsoleEmailService : IEmailService
{
    private readonly ILogger<ConsoleEmailService> _logger;

    public ConsoleEmailService(ILogger<ConsoleEmailService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Captures and prints password reset emails directly to the standard output and debug logs.
    /// Provides an instant clickable link for developers in VS Code, Visual Studio, or terminal sessions.
    /// </summary>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="resetUrl">Fully qualified password reset confirmation URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task SendPasswordResetAsync(string toEmail, string resetUrl, CancellationToken cancellationToken = default)
    {
        // 1. Construct formatted visual console banner for development inspection
        var banner = $"""

        ================================================================================
        [LOCAL DEV EMAIL DISPATCHED] (Zero-Config SMTP Bypass)
        --------------------------------------------------------------------------------
        Timestamp : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
        Recipient : {toEmail}
        Subject   : Reset your PettyCash password
        Action URL: {resetUrl}
        --------------------------------------------------------------------------------
        (Ctrl+Click or copy the URL above into your browser to test the reset flow)
        ================================================================================
        """;

        // 2. Emit visual banner to standard output (interactive terminal window)
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(banner);
        Console.ResetColor();

        // 3. Log structured event for diagnostic inspection in test runners and log collectors
        _logger.LogInformation(
            "Development email simulated for recipient {Recipient}. Reset URL: {ResetUrl}",
            toEmail,
            resetUrl);

        // 4. Return completed task immediately (zero network delay)
        return Task.CompletedTask;
    }
}
