namespace ECafe.Application.Services
{
    public interface IEmailService
    {
        public Task SendMailAsync(string toEmail, string name);

        public Task SendMailAsync(string toEmail, string name, string surName, string password, string role);
        public Task SendMailAsync(string toEmail, string name, string surName, string role);

        public Task SendContractNotificationAsync(string toEmail, string name, string subject, string body);
    }
}
