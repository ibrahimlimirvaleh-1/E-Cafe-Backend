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

        public async Task SendMailAsync(string toEmail, string name)
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
                Body = $"{name} uğurla qeydiyyatdan keçdi.",
                IsBodyHtml = false
            };

            mail.To.Add(toEmail);

            await client.SendMailAsync(mail);
        }

        public async Task SendMailAsync(string toEmail, string name, string surname, string password, string role)
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
                Subject = "İstifadəçi qeydiyyatı tamamlandı",
                Body = $"{name} {surname} {role} rolu ilə uğurla qeydiyyatdan keçdi.Şifrəniz : {password}",
                IsBodyHtml = false
            };

            mail.To.Add(toEmail);

            await client.SendMailAsync(mail);
        }

        public async Task SendMailAsync(string toEmail, string name, string surName, string role)
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
                Subject = "İstifadəçi qeydiyyatı tamamlandı",
                Body = $"{name} {surName} rolunuz dəyişdirildi.Yeni rolunuz : {role}",
                IsBodyHtml = false
            };

            mail.To.Add(toEmail);

            await client.SendMailAsync(mail);
        }
    }
}
