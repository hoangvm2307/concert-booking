using BookingService.Models.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BookingService.Services
{
    public class MailKitEmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<MailKitEmailService> _logger;

        public MailKitEmailService(IOptions<EmailSettings> emailSettingsOptions, ILogger<MailKitEmailService> logger)
        {
            _emailSettings = emailSettingsOptions.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toAddress, string subject, string htmlBody, string textBody = "")
        {
            if (string.IsNullOrEmpty(_emailSettings.SmtpServer) ||
                string.IsNullOrEmpty(_emailSettings.SmtpUser) ||
                string.IsNullOrEmpty(_emailSettings.SmtpPass) ||
                string.IsNullOrEmpty(_emailSettings.FromAddress))
            {
                _logger.LogWarning("Email settings are not fully configured. Skipping actual email send and logging to console instead.");
                LogEmailToConsole(toAddress, subject, htmlBody, textBody);
                return;
            }

            try
            {
                var email = new MimeMessage();
                email.From.Add(new MailboxAddress(_emailSettings.FromName, _emailSettings.FromAddress));
                email.To.Add(MailboxAddress.Parse(toAddress));
                email.Subject = subject;

                var bodyBuilder = new BodyBuilder();
                if (!string.IsNullOrEmpty(textBody))
                {
                    bodyBuilder.TextBody = textBody;
                }
                bodyBuilder.HtmlBody = htmlBody;
                email.Body = bodyBuilder.ToMessageBody();

                using var smtp = new SmtpClient();

                // Note: Mailtrap often uses port 25, 465, 587, or 2525.
                // For SSL/TLS on port 465, use SecureSocketOptions.SslOnConnect.
                // For STARTTLS on port 587 or 25, use SecureSocketOptions.StartTls.
                // If UseSsl is true and port is 465, SslOnConnect is typical. Otherwise, StartTls.
                SecureSocketOptions socketOptions = SecureSocketOptions.Auto;
                if (_emailSettings.UseSsl && _emailSettings.SmtpPort == 465)
                {
                    socketOptions = SecureSocketOptions.SslOnConnect;
                }
                else if (_emailSettings.UseSsl)
                { // For ports like 587
                    socketOptions = SecureSocketOptions.StartTls;
                }
                else
                { // For port 25 without explicit SSL/TLS, usually StartTlsWhenAvailable or None
                    socketOptions = SecureSocketOptions.StartTlsWhenAvailable;
                }


                _logger.LogInformation("Attempting to connect to SMTP server {SmtpServer} on port {SmtpPort} with UseSsl={UseSsl} (resolved as {SocketOptions})",
                    _emailSettings.SmtpServer, _emailSettings.SmtpPort, _emailSettings.UseSsl, socketOptions);

                await smtp.ConnectAsync(_emailSettings.SmtpServer, _emailSettings.SmtpPort, socketOptions);

                _logger.LogInformation("Authenticating with SMTP server using username {SmtpUser}", _emailSettings.SmtpUser);
                await smtp.AuthenticateAsync(_emailSettings.SmtpUser, _emailSettings.SmtpPass);

                _logger.LogInformation("Sending email to {ToAddress} with subject '{Subject}'", toAddress, subject);
                await smtp.SendAsync(email);
                await smtp.DisconnectAsync(true);
                _logger.LogInformation("Email successfully sent to {ToAddress}", toAddress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {ToAddress} with subject '{Subject}'. Error: {ErrorMessage}", toAddress, subject, ex.Message);
                // Fallback to logging if actual send fails
                LogEmailToConsole(toAddress, subject, htmlBody, textBody, isError: true, errorMessage: ex.Message);
            }
        }

        private void LogEmailToConsole(string toAddress, string subject, string htmlBody, string textBody, bool isError = false, string? errorMessage = null)
        {
            var logHeader = isError ? "--- FAILED EMAIL (LOGGED TO CONSOLE) ---" : "--- SIMULATED EMAIL (LOGGED TO CONSOLE) ---";
            if (isError && errorMessage != null)
            {
                logHeader += $"\nFailure Reason: {errorMessage}";
            }

            _logger.LogInformation(
                $"{logHeader}\n" +
                $"To: {toAddress}\n" +
                $"From: {_emailSettings.FromName} <{_emailSettings.FromAddress}>\n" +
                $"Subject: {subject}\n" +
                $"Text Body (if any): {textBody}\n" +
                $"HTML Body:\n{htmlBody}\n" +
                $"--- END LOGGED EMAIL ---"
            );
        }
    }
}