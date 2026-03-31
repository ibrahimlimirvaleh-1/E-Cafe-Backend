using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;

namespace ECafe.Application.Services
{
    public class EmailManager : BaseManager, IEmailService
    {
        public EmailManager(IHttpContextAccessor httpContextAccessor, 
                            IMapper mapper, IConfiguration configuration) 
                            : base(httpContextAccessor, mapper, configuration)
        {
        }

        public async Task SendMailAsync(string toEmail, string restaurantName)
        {
            var smtpHost = _configuration["Email:SmtpHost"];
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"]!);
            var smtpUser = _configuration["Email:Username"];
            var smtpPass = _configuration["Email:Password"];
            var fromEmail = _configuration["Email:From"];

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };

            var mail = new MailMessage
            {
                From = new MailAddress(fromEmail!),
                Subject = "Restoran qeydiyyatı tamamlandı",
                Body = $"{restaurantName} uğurla qeydiyyatdan keçdi.",
                IsBodyHtml = false
            };

            mail.To.Add(toEmail);

            await client.SendMailAsync(mail);
        }
    }
}
