using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FitForge.Services
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string toEmail, string toName, string token);
        Task SendPasswordResetAsync(string toEmail, string toName, string token);
        Task SendWelcomeAsync(string toEmail, string toName);
    }

    /// <summary>
    /// Stub implementation — logs email content to console when SMTP is not configured.
    /// Configure appsettings.json Email section to send real emails.
    /// </summary>
    public class EmailService(IConfiguration config, ILogger<EmailService> log) : IEmailService
    {
        private readonly string? _host   = config["Email:Host"];
        private readonly int     _port   = int.TryParse(config["Email:Port"], out var p) ? p : 587;
        private readonly string? _user   = config["Email:Username"];
        private readonly string? _pass   = config["Email:Password"];
        private readonly string  _from   = config["Email:From"] ?? "noreply@fitforge.app";
        private readonly string  _base   = config["Email:BaseUrl"] ?? "http://localhost:5000";

        private bool IsConfigured => !string.IsNullOrWhiteSpace(_host) && !string.IsNullOrWhiteSpace(_user);

        public async Task SendVerificationEmailAsync(string toEmail, string toName, string token)
        {
            string link = $"{_base}/Account/Verify?token={token}";
            string subject = "Verify your FitForge account";
            string body = $@"Hi {toName},<br><br>
Click the link below to verify your account:<br>
<a href=""{link}"">{link}</a><br><br>
This link expires in 24 hours.<br><br>
— FitForge";
            await SendAsync(toEmail, subject, body);
        }

        public async Task SendPasswordResetAsync(string toEmail, string toName, string token)
        {
            string link = $"{_base}/Account/ResetPassword?token={token}";
            string subject = "FitForge password reset";
            string body = $@"Hi {toName},<br><br>
Reset your password:<br>
<a href=""{link}"">{link}</a><br><br>
If you didn't request this, ignore this email.<br><br>
— FitForge";
            await SendAsync(toEmail, subject, body);
        }

        public async Task SendWelcomeAsync(string toEmail, string toName)
        {
            string subject = "Welcome to FitForge!";
            string body = $"Hi {toName}, welcome to FitForge! Start your first workout today.";
            await SendAsync(toEmail, subject, body);
        }

        private async Task SendAsync(string to, string subject, string htmlBody)
        {
            if (!IsConfigured)
            {
                log.LogInformation("[EMAIL STUB] To={To} Subject={Sub}\n{Body}", to, subject, htmlBody);
                return;
            }
            try
            {
                using var client = new System.Net.Mail.SmtpClient(_host, _port)
                {
                    EnableSsl   = true,
                    Credentials = new System.Net.NetworkCredential(_user, _pass)
                };
                var msg = new System.Net.Mail.MailMessage(_from, to, subject, htmlBody) { IsBodyHtml = true };
                await client.SendMailAsync(msg);
                log.LogInformation("Email sent to {To}: {Sub}", to, subject);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Email send failed to {To}", to);
            }
        }
    }
}
