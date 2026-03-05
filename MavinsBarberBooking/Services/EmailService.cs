using System.Net;
using System.Net.Mail;

namespace MavinsBarberBooking.Services
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string email, string verificationCode);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendVerificationEmailAsync(string email, string verificationCode)
        {
            try
            {
                var isEmailDisabled = _config.GetValue<bool>("EmailSettings:IsEmailDisabled");

                if (isEmailDisabled)
                {
                    _logger.LogInformation($"[DEVELOPMENT MODE] Verification code for {email}: {verificationCode}");
                    await Task.CompletedTask;
                    return;
                }

                var smtpSettings = _config.GetSection("SmtpSettings");
                var smtpServer = smtpSettings["Server"];
                var smtpPort = int.Parse(smtpSettings["Port"] ?? "587");
                var smtpUsername = smtpSettings["Username"];
                var smtpPassword = smtpSettings["Password"];
                var senderEmail = smtpSettings["SenderEmail"];

                if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpUsername) || string.IsNullOrEmpty(smtpPassword))
                {
                    _logger.LogError("SMTP settings are not configured properly");
                    throw new InvalidOperationException("SMTP settings are missing in configuration");
                }

                using (var client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.EnableSsl = true;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(smtpUsername, smtpPassword);
                    client.Timeout = 10000;

                    var mailMessage = new MailMessage(senderEmail, email)
                    {
                        Subject = "Email Verification - Mavins Barber Booking",
                        Body = $@"
                            <h2>Email Verification</h2>
                            <p>Thank you for registering with Mavins Barber Booking!</p>
                            <p>Your verification code is: <strong>{verificationCode}</strong></p>
                            <p>This code will expire in 15 minutes.</p>
                        ",
                        IsBodyHtml = true
                    };

                    await client.SendMailAsync(mailMessage);
                    _logger.LogInformation($"Verification email sent to {email}");
                }
            }
            catch (SmtpException ex)
            {
                _logger.LogError($"SMTP Error: {ex.Message}");
                throw new InvalidOperationException("Failed to send verification email. Please try again later.", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Email service error: {ex.Message}");
                throw;
            }
        }
    }
}