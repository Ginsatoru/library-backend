using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace LibrarySystemBBU.Services
{
    public sealed class SmtpEmailSender : IEmailSender
    {
        private readonly SmtpOptions _opt;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<SmtpOptions> opt, ILogger<SmtpEmailSender> logger)
        {
            _opt = opt.Value;
            _logger = logger;
        }

        public async Task<EmailSendResult> SendAsync(string toEmail, string subject, string htmlBody)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_opt.Host) ||
                    _opt.Port <= 0 ||
                    string.IsNullOrWhiteSpace(_opt.FromEmail))
                {
                    var msg = "SMTP config missing: Smtp:Host / Smtp:Port / Smtp:FromEmail";
                    _logger.LogError(msg);
                    Console.WriteLine(msg);
                    return EmailSendResult.Fail(msg);
                }

                using var message = new MailMessage
                {
                    From = new MailAddress(_opt.FromEmail, _opt.FromName, Encoding.UTF8),
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true,
                    SubjectEncoding = Encoding.UTF8,
                    BodyEncoding = Encoding.UTF8
                };
                message.To.Add(new MailAddress(toEmail));

                using var client = new SmtpClient(_opt.Host, _opt.Port)
                {
                    EnableSsl = _opt.EnableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Timeout = _opt.TimeoutMs
                };

                if (!string.IsNullOrWhiteSpace(_opt.Username) && !string.IsNullOrWhiteSpace(_opt.Password))
                {
                    // IMPORTANT: App Password should be stored WITHOUT spaces
                    var cleanPass = _opt.Password.Replace(" ", "");
                    client.Credentials = new NetworkCredential(_opt.Username, cleanPass);
                }

                await client.SendMailAsync(message);

                _logger.LogInformation("SMTP email sent to {ToEmail} subject={Subject}", toEmail, subject);
                Console.WriteLine($"SMTP OK: sent to {toEmail}");
                return EmailSendResult.Ok();
            }
            catch (SmtpException ex)
            {
                var err = $"SMTP ERROR: StatusCode={ex.StatusCode}, Message={ex.Message}, Inner={ex.InnerException?.Message}";
                _logger.LogError(ex, err);
                Console.WriteLine(err);
                return EmailSendResult.Fail(err);
            }
            catch (Exception ex)
            {
                var err = $"EMAIL ERROR: {ex.Message}, Inner={ex.InnerException?.Message}";
                _logger.LogError(ex, err);
                Console.WriteLine(err);
                return EmailSendResult.Fail(err);
            }
        }
    }
}
