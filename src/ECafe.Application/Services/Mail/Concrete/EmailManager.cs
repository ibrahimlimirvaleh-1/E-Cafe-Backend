using AutoMapper;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MimeKit;

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

        public async Task SendAsync(
            string toEmail,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
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

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("plain")
            {
                Text = body
            };

            using var client = new SmtpClient();
            client.Timeout = GetEmailTimeoutMilliseconds();
            client.LocalDomain = GetEmailLocalDomain();

            await client.ConnectAsync(
                smtpHost,
                smtpPort,
                GetSecureSocketOptions(smtpPort));

            if (!client.IsSecure)
                throw new InvalidOperationException("SMTP connection must use TLS.");

            client.AuthenticationMechanisms.Remove("XOAUTH2");
            await client.AuthenticateAsync(smtpUser, smtpPass, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }

        private string GetRequiredEmailSetting(string key)
        {
            var value = _configuration[$"Email:{key}"];
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Email:{key} configuration is required.");

            return value.Trim();
        }

        private SecureSocketOptions GetSecureSocketOptions(int smtpPort)
        {
            var configuredValue = _configuration["Email:SecureSocketOption"];
            if (!string.IsNullOrWhiteSpace(configuredValue)
                && Enum.TryParse<SecureSocketOptions>(configuredValue, ignoreCase: true, out var configuredOption))
            {
                return configuredOption;
            }

            return smtpPort switch
            {
                465 => SecureSocketOptions.SslOnConnect,
                587 => SecureSocketOptions.StartTls,
                _ => SecureSocketOptions.Auto
            };
        }

        private int GetEmailTimeoutMilliseconds()
        {
            var value = _configuration["Email:TimeoutSeconds"];
            return int.TryParse(value, out var seconds) && seconds > 0
                ? seconds * 1000
                : 30000;
        }

        private string GetEmailLocalDomain()
        {
            var value = _configuration["Email:LocalDomain"];
            return string.IsNullOrWhiteSpace(value)
                ? "localhost"
                : value.Trim();
        }
    }
}
