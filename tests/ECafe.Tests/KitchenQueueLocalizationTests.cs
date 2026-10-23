using ECafe.Application.Services.Localization;
using Xunit;

namespace ECafe.Tests;

public sealed class KitchenQueueLocalizationTests
{
    [Theory]
    [InlineData("az", "Qəbuldan keçən vaxt", "Hazırlanma müddəti", "Hazır gözləyir")]
    [InlineData("en", "Time since acceptance", "Preparation time", "Waiting to be served")]
    [InlineData("ru", "Время после принятия", "Время приготовления", "Ожидает подачи")]
    public void StageDurationLabelsDescribeEachKitchenStatus(string language, string accepted, string preparing, string ready)
    {
        var messages = new TranslationBootstrap().Snapshot.ForLanguage(language).Messages;
        Assert.Equal(accepted, messages["ui.kitchenQueue.acceptedElapsed"]);
        Assert.Equal(preparing, messages["ui.kitchenQueue.preparationElapsed"]);
        Assert.Equal(ready, messages["ui.kitchenQueue.readyWaiting"]);
    }

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
