using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ECafe.Application.Common.Validation;
using ECafe.Application.Services.Sms.Abstract;
using ECafe.Domain.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ECafe.Application.Services.Sms.Concrete;

public sealed class BrevoSmsManager : ISmsService
{
    private const int MaxSenderLength = 11;
    private const string SenderPattern = "^[A-Za-z0-9]+$";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConfiguration _configuration;
    private readonly ILogger<BrevoSmsManager> _logger;

    public BrevoSmsManager(IConfiguration configuration, ILogger<BrevoSmsManager> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAsync(
        string toPhone,
        string message,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var apiKey = GetRequiredSetting("ApiKey");
        var sender = GetValidatedSender();
        var baseUrl = GetBaseUrl();
        var recipient = NormalizeRecipient(toPhone);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(baseUrl, "transactionalSMS/sms"));

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("api-key", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                sender,
                recipient,
                content = message
            }, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Brevo SMS send failed. StatusCode: {StatusCode}, Sender: {Sender}, Recipient: {Recipient}, Response: {Response}",
                (int)response.StatusCode,
                sender,
                MaskPhone(recipient),
                responseBody);

            throw new ServiceUnavailableException(ErrorCode.NotificationProviderUnavailable);
        }

        _logger.LogInformation("Brevo SMS sent. Recipient: {Recipient}", MaskPhone(recipient));
    }

    public Task<SmsBalanceResponse> GetBalanceAsync(CancellationToken cancellationToken = default)
        => throw new ServiceUnavailableException(ErrorCode.NotificationProviderUnavailable);

    public Task<SmsDeliveryStatusResponse> GetStatusAsync(
        string messageId,
        CancellationToken cancellationToken = default)
        => throw new ServiceUnavailableException(ErrorCode.NotificationProviderUnavailable);

    private Uri GetBaseUrl()
    {
        var configuredBaseUrl = _configuration["Sms:BaseUrl"];
        var baseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? "https://api.brevo.com/v3"
            : configuredBaseUrl.Trim();

        if (!baseUrl.EndsWith('/'))
            baseUrl += "/";

        return new Uri(baseUrl, UriKind.Absolute);
    }

    private string GetRequiredSetting(string key)
    {
        var value = _configuration[$"Sms:{key}"];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Sms:{key} configuration is required.");

        return value.Trim();
    }

    private string GetValidatedSender()
    {
        var sender = GetRequiredSetting("Sender");

        if (sender.Length > MaxSenderLength || !System.Text.RegularExpressions.Regex.IsMatch(sender, SenderPattern))
        {
            _logger.LogWarning(
                "Invalid SMS sender configuration. SenderLength: {SenderLength}, AllowedPattern: {AllowedPattern}",
                sender.Length,
                SenderPattern);

            throw new ServiceUnavailableException(ErrorCode.NotificationProviderUnavailable);
        }

        return sender;
    }

    private static string NormalizeRecipient(string phone)
    {
        try
        {
            return PhoneNumberValidationExtensions.NormalizeAzerbaijanPhoneNumber(phone).TrimStart('+');
        }
        catch (ArgumentException)
        {
            throw new InvalidOperationException("SMS recipient phone number is invalid.");
        }
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length <= 4)
            return "****";

        return $"{new string('*', Math.Max(0, phone.Length - 4))}{phone[^4..]}";
    }
}
