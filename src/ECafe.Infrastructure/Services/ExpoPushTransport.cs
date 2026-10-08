using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ECafe.Infrastructure.Services;

public sealed class ExpoPushTransport(HttpClient client, IConfiguration configuration) : IExpoPushTransport
{
    public async Task<ExpoPushResult> SendAsync(string expoToken, Guid deliveryId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "send")
        {
            Content = JsonContent.Create(new
            {
                to = expoToken,
                title = "ECafe",
                body = "Yeni bildirişiniz var.",
                channelId = "ecafe-alerts",
                sound = "default",
                data = new { deliveryId }
            })
        };
        AddAuthorization(request);
        using var response = await client.SendAsync(request, cancellationToken);
        EnsureRetryableResponse(response);
        if (!response.IsSuccessStatusCode)
            return new(false, null, "ProviderRejected");

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("data", out var data))
            throw new HttpRequestException("Expo push ticket is missing data.");

        var ticket = data.ValueKind == JsonValueKind.Array ? data.EnumerateArray().FirstOrDefault() : data;
        if (ticket.ValueKind != JsonValueKind.Object)
            throw new HttpRequestException("Expo push ticket is invalid.");

        if (ticket.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String && status.GetString() == "ok" &&
            ticket.TryGetProperty("id", out var id) &&
            id.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString()))
            return new(true, id.GetString(), null);

        return new(false, null, GetErrorCode(ticket));
    }

    public async Task<ExpoReceiptResult?> GetReceiptAsync(string ticketId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "getReceipts")
        {
            Content = JsonContent.Create(new { ids = new[] { ticketId } })
        };
        AddAuthorization(request);
        using var response = await client.SendAsync(request, cancellationToken);
        EnsureRetryableResponse(response);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException("Expo receipt request was not accepted.");

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Object ||
            !data.TryGetProperty(ticketId, out var receipt))
            return null;

        if (receipt.ValueKind != JsonValueKind.Object ||
            !receipt.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String)
            return null;

        return status.GetString() switch
        {
            "ok" => new(true, null),
            "error" => new(false, GetErrorCode(receipt)),
            _ => null
        };
    }

    // Expo sorğusuna yalnız konfiqurasiya edilmiş giriş məlumatını əlavə edir.
    private void AddAuthorization(HttpRequestMessage request)
    {
        var accessToken = configuration["MobileApp:ExpoAccessToken"];
        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    // HTTP cavabının müvəqqəti xəta olub yenidən sınana biləcəyini ayırır.
    private static void EnsureRetryableResponse(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500)
            throw new HttpRequestException("Expo push service is temporarily unavailable.");
    }

    // Expo cavabındakı maşın tərəfindən oxunan xəta kodunu çıxarır.
    private static string GetErrorCode(JsonElement element)
    {
        if (element.TryGetProperty("details", out var details) &&
            details.ValueKind == JsonValueKind.Object &&
            details.TryGetProperty("error", out var error) &&
            error.ValueKind == JsonValueKind.String)
        {
            var code = error.GetString();
            if (!string.IsNullOrWhiteSpace(code) && code.Length <= 100)
                return code;
        }

        return "ProviderRejected";
    }
}
