using ECafe.Application.Services.Localization;
using Xunit;

namespace ECafe.Tests;

public sealed class WaiterWorkspaceLocalizationTests
{
    [Theory]
    [InlineData("az", "Hazır sifarişlər", "Təqdim et", "Hesab")]
    [InlineData("en", "Ready orders", "Serve", "Bill")]
    [InlineData("ru", "Готовые заказы", "Подать", "Счёт")]
    public void WorkspaceActionsAndRetryFeedbackHavePublishedFallbacks(string language,
        string ready, string serve, string bill)
    {
        var messages = new TranslationBootstrap().Snapshot.ForLanguage(language).Messages;
        Assert.Equal(ready, messages["ui.waiter.readyOrders"]);
        Assert.Equal(serve, messages["ui.waiter.serve"]);
        Assert.Equal(bill, messages["ui.waiter.bill"]);
        Assert.Contains("{{count}}", messages["ui.waiter.readyCount"]);
        Assert.Contains("{{count}}", messages["ui.waiter.unsentCount"]);
        Assert.Contains("{{id}}", messages["ui.waiter.serveUnknown"]);
        Assert.False(string.IsNullOrWhiteSpace(messages["ui.waiter.draftUnavailable"]));
    }
}
