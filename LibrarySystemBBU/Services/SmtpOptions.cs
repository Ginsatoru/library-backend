namespace LibrarySystemBBU.Services
{
    public sealed class SmtpOptions
    {
        public string Host { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string FromEmail { get; set; } = "";
        public string FromName { get; set; } = "Library System";
        public bool EnableSsl { get; set; } = true;
        public int TimeoutMs { get; set; } = 15000;
    }
}
