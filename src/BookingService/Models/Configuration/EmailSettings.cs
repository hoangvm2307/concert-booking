namespace BookingService.Models.Configuration
{
    public class EmailSettings
    {
        public required string SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public required string SmtpUser { get; set; }
        public required string SmtpPass { get; set; }
        public required string FromAddress { get; set; }
        public required string FromName { get; set; }
        public bool UseSsl { get; set; } = true; // Common for most SMTP servers
    }
}