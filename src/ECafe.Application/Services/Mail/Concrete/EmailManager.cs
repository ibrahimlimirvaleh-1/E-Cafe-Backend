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
            await SendAsync(
                toEmail,
                "Restoran qeydiyyatı tamamlandı",
                $"{name} uğurla qeydiyyatdan keçdi.");
        }

        public async Task SendMailAsync(string toEmail, string name, string surname, string password, string role)
        {
            await SendAsync(
                toEmail,
                "İstifadəçi qeydiyyatı tamamlandı",
                $"{name} {surname} {role} rolu ilə uğurla qeydiyyatdan keçdi.Şifrəniz : {password}");
        }

        public async Task SendMailAsync(string toEmail, string name, string surName, string role)
        {
            await SendAsync(
                toEmail,
                "İstifadəçi qeydiyyatı tamamlandı",
                $"{name} {surName} rolunuz dəyişdirildi.Yeni rolunuz : {role}");
        }

        public async Task SendContractNotificationAsync(string toEmail, string name, string subject, string body)
        {
            await SendAsync(
                toEmail,
                subject,
                $"Salam {name},\n\n{body}");
        }

        private async Task SendAsync(string toEmail, string subject, string body)
        {
            var smtpHost = GetRequiredEmailSetting("SmtpHost");
            var smtpPortValue = GetRequiredEmailSetting("SmtpPort");
            var smtpUser = GetRequiredEmailSetting("Username");
            var smtpPass = GetRequiredEmailSetting("Password");
            var fromEmail = _configuration["Email:From"];
            var fromName = _configuration["Email:FromName"] ?? "E-Cafe Admin";

            if (!int.TryParse(smtpPortValue, out var smtpPort))
                throw new InvalidOperationException("Email:SmtpPort must be a valid port number.");

            if (string.IsNullOrWhiteSpace(fromEmail))
                fromEmail = smtpUser;

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };

            var mail = new MailMessage
            {
                From = new MailAddress(fromEmail!, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };

            mail.To.Add(toEmail);

            await client.SendMailAsync(mail);
        }

        private string GetRequiredEmailSetting(string key)
        {
            var value = _configuration[$"Email:{key}"];
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Email:{key} configuration is required.");

            return value.Trim();
        }
    }
}
