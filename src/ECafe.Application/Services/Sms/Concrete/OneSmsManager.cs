using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ECafe.Application.Services.Sms.Abstract;
using ECafe.Domain.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ECafe.Application.Services.Sms.Concrete;

public sealed class OneSmsManager : ISmsService
{
    private const int MaxSenderLength = 32;
    private const string DefaultApiKeyHeaderName = "X-API-Key";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConfiguration _configuration;
    private readonly ILogger<OneSmsManager> _logger;

    public OneSmsManager(IConfiguration configuration, ILogger<OneSmsManager> logger)
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
        var recipient = NormalizeAzerbaijanRecipient(toPhone);
        var apiKeyHeaderName = GetApiKeyHeaderName();

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri(baseUrl, "sms/notification"));

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        AddApiKeyHeader(request, apiKeyHeaderName, apiKey);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            request.Headers.Add("X-Idempotency-Key", idempotencyKey.Trim());

        request.Content = new StringContent(
            JsonSerializer.Serialize(new
            {
                recipients = new[] { recipient },
                text = message,
                senderName = sender
            }, JsonOptions),
            Encoding.UTF8,
            "application/json");

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var providerAccepted = IsAcceptedByProvider(responseBody, out var providerResponse);

        if (!response.IsSuccessStatusCode || !providerAccepted)
        {
            _logger.LogWarning(
                "1sms.az SMS send failed. StatusCode: {StatusCode}, Sender: {Sender}, Recipient: {Recipient}, ProviderSuccess: {ProviderSuccess}, Response: {Response}",
                (int)response.StatusCode,
                sender,
                MaskPhone(recipient),
                providerResponse?.Success,
                responseBody);

            throw new ServiceUnavailableException(ErrorCode.NotificationProviderUnavailable);
        }

        _logger.LogInformation(
            "1sms.az SMS sent. Recipient: {Recipient}, TaskId: {TaskId}, Cost: {Cost}, Balance: {Balance}",
            MaskPhone(recipient),
            providerResponse?.TaskId,
            providerResponse?.Cost,
            providerResponse?.Balance);
    }

    private Uri GetBaseUrl()
    {
        var configuredBaseUrl = _configuration["Sms:BaseUrl"];
        var baseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? "https://1sms.az/api/v1"
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

        if (sender.Length > MaxSenderLength)
        {
            _logger.LogWarning(
                "Invalid 1sms.az sender configuration. SenderLength: {SenderLength}",
                sender.Length);

            throw new ServiceUnavailableException(ErrorCode.NotificationProviderUnavailable);
        }

        return sender;
    }

    private string GetApiKeyHeaderName()
    {
        var configuredHeader = _configuration["Sms:ApiKeyHeader"];
        return string.IsNullOrWhiteSpace(configuredHeader)
            ? DefaultApiKeyHeaderName
            : configuredHeader.Trim();
    }

    private static void AddApiKeyHeader(HttpRequestMessage request, string headerName, string apiKey)
    {
        if (headerName.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            return;
        }

        request.Headers.Add(headerName, apiKey);
    }

    private static bool IsAcceptedByProvider(string responseBody, out OneSmsNotificationResponse? providerResponse)
    {
        providerResponse = null;

        if (string.IsNullOrWhiteSpace(responseBody))
            return false;

        try
        {
            providerResponse = JsonSerializer.Deserialize<OneSmsNotificationResponse>(responseBody, JsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        return providerResponse is { Success: true, SentCount: > 0 };
    }

    private static string NormalizeAzerbaijanRecipient(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        if (digits.StartsWith("00994"))
            throw new InvalidOperationException("SMS recipient phone number is invalid.");

        if (digits.StartsWith("9940"))
            throw new InvalidOperationException("SMS recipient phone number is invalid.");

        if (digits.StartsWith('0') && digits.Length == 10)
            digits = $"994{digits[1..]}";
        else if (digits.Length == 9)
            digits = $"994{digits}";

        if (!digits.StartsWith("994") || digits.Length != 12)
            throw new InvalidOperationException("SMS recipient phone number is invalid.");

        return $"+{digits}";
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length <= 4)
            return "****";

        return $"{new string('*', Math.Max(0, phone.Length - 4))}{phone[^4..]}";
    }

    private sealed class OneSmsNotificationResponse
    {
        public bool Success { get; set; }

        public int SentCount { get; set; }

        public string? TaskId { get; set; }

        public decimal? Cost { get; set; }

        public decimal? Balance { get; set; }
    }
}
