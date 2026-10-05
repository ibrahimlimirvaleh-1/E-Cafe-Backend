using System.Net;
using System.Text;
using System.Text.Json;
using ECafe.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ECafe.Tests;

public sealed class ExpoPushTransportTests
{
    [Fact]
    public async Task SendUsesGenericPayloadAndParsesTicketAndReceipt()
    {
        var calls = new List<(string Url, string Body, string? Authorization)>();
        var handler = new StubHandler(async request =>
        {
            var body = await request.Content!.ReadAsStringAsync();
            calls.Add((request.RequestUri!.AbsoluteUri, body,
                request.Headers.Authorization?.ToString()));
            var json = request.RequestUri.AbsolutePath.EndsWith("getReceipts")
                ? """{"data":{"ticket-1":{"status":"ok"}}}"""
                : """{"data":{"status":"ok","id":"ticket-1"}}""";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://exp.host/--/api/v2/push/")
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MobileApp:ExpoAccessToken"] = "server-only-secret"
        }).Build();
        var transport = new ExpoPushTransport(client, configuration);

        var ticket = await transport.SendAsync("ExpoPushToken[abcdefghijklmnopqrstuv]", Guid.NewGuid(), CancellationToken.None);
        var receipt = await transport.GetReceiptAsync(ticket.TicketId!, CancellationToken.None);

        Assert.True(ticket.Success);
        Assert.Equal("ticket-1", ticket.TicketId);
        Assert.True(receipt!.Success);
        Assert.EndsWith("/send", calls[0].Url);
        Assert.Equal("Yeni bildirişiniz var.",
            JsonDocument.Parse(calls[0].Body).RootElement.GetProperty("body").GetString());
        Assert.Equal("ecafe-alerts", JsonDocument.Parse(calls[0].Body).RootElement.GetProperty("channelId").GetString());
        Assert.Equal("default", JsonDocument.Parse(calls[0].Body).RootElement.GetProperty("sound").GetString());
        Assert.DoesNotContain("userId", calls[0].Body);
        Assert.Equal("Bearer server-only-secret", calls[0].Authorization);
        Assert.EndsWith("/getReceipts", calls[1].Url);
    }

    [Fact]
    public async Task RateLimitIsRetryable()
    {
        var client = new HttpClient(new StubHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests))))
        {
            BaseAddress = new Uri("https://exp.host/--/api/v2/push/")
        };
        var transport = new ExpoPushTransport(client, new ConfigurationBuilder().Build());

        await Assert.ThrowsAsync<HttpRequestException>(() => transport.SendAsync(
            "ExpoPushToken[abcdefghijklmnopqrstuv]", Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ReceiptHttpFailureDoesNotClaimPushDeliveryFailed()
    {
        var client = new HttpClient(new StubHandler(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.Unauthorized))))
        {
            BaseAddress = new Uri("https://exp.host/--/api/v2/push/")
        };
        var transport = new ExpoPushTransport(client, new ConfigurationBuilder().Build());

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            transport.GetReceiptAsync("ticket-1", CancellationToken.None));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => response(request);
    }
}
