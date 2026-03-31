namespace ECafe.Application.Services
{
    public interface IEmailService
    {
        public Task SendMailAsync(string toEmail, string restaurantName);
    }
}
