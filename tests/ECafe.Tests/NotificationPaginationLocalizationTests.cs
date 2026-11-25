using ECafe.Application.Services.Localization;
using ECafe.Domain.Policies;
using Xunit;

namespace ECafe.Tests;

public sealed class NotificationPaginationLocalizationTests
{
    private static readonly TranslationBootstrap Bootstrap = new();

    [Theory]
    [InlineData("ui.notifications.pagination")]
    [InlineData("ui.notifications.loadFailed")]
    [InlineData("ui.notifications.refresh")]
    [InlineData("ui.notifications.retry")]
    [InlineData("ui.notifications.unread_one")]
    [InlineData("ui.notifications.unread_few")]
    [InlineData("ui.notifications.unread_many")]
    [InlineData("ui.notifications.unread_other")]
    public void PaginationUiKeysAreRegisteredForAdminEditingAndAllLanguagesPreserveParameters(string key)
    {
        Assert.True(Bootstrap.Snapshot.Entries.TryGetValue(key, out var entry));
        var expected = TranslationPolicy.Placeholders(entry.Az);
        foreach (var language in TranslationPolicy.Languages)
        {
            var value = Bootstrap.Snapshot.ForLanguage(language).Messages[key];
            Assert.False(string.IsNullOrWhiteSpace(value));
            Assert.True(expected.SetEquals(TranslationPolicy.Placeholders(value)));
            if (key.Contains("unread", StringComparison.Ordinal)) Assert.Contains("count", expected);
        }
    }
}
