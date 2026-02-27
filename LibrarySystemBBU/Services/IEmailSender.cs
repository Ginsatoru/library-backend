using System.Threading.Tasks;

namespace LibrarySystemBBU.Services
{
    public interface IEmailSender
    {
        Task<EmailSendResult> SendAsync(string toEmail, string subject, string htmlBody);
    }

    public sealed class EmailSendResult
    {
        public bool Success { get; }
        public string? Error { get; }

        private EmailSendResult(bool success, string? error)
        {
            Success = success;
            Error = error;
        }

        public static EmailSendResult Ok() => new EmailSendResult(true, null);
        public static EmailSendResult Fail(string error) => new EmailSendResult(false, error);
    }
}
