using ECafe.Application.Services.Localization;
using Xunit;

namespace ECafe.Tests;

public sealed class KitchenQueueLocalizationTests
{
    [Theory]
    [InlineData("az", "Hamısı", "dəq")]
    [InlineData("en", "All", "min")]
    [InlineData("ru", "Все", "мин")]
    public void QueueFiltersTimersAndStaleFeedbackHaveCentralFallbacks(string language, string all, string minute)
    {
        var messages = new TranslationBootstrap().Snapshot.ForLanguage(language).Messages;
        Assert.Equal(all, messages["ui.kitchenQueue.allStages"]);
        Assert.Equal(minute, messages["ui.kitchenQueue.minuteShort"]);
        foreach (var key in new[] { "stageFilter", "waiting", "inStage", "lessThanMinute", "staleQueue",
                     "inQueue", "lastUpdated", "accept", "start", "ready", "retry" })
            Assert.False(string.IsNullOrWhiteSpace(messages[$"ui.kitchenQueue.{key}"]));
    }
}
