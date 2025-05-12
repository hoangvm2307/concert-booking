using System.Threading.Tasks;

namespace BookingService.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toAddress, string subject, string htmlBody, string textBody = "");
    }
}