using ECafe.Application.Services.Localization;
using ECafe.Domain.Policies;
using Xunit;

namespace ECafe.Tests;

public sealed class DashboardPresentationCatalogTests
{
    /// <summary>Miqdar, məbləğ və diaqram idarələrinin ayrı açarlarla üç dildə təqdim edildiyini yoxlayır.</summary>
    [Theory]
    [InlineData("ui.dashboardReports.column.itemUnits")]
    [InlineData("ui.dashboardReports.column.originalSubtotal")]
    [InlineData("ui.dashboardReports.column.confirmedOrderAmount")]
    [InlineData("ui.dashboardReports.cancelledOriginal")]
    [InlineData("ui.dashboardReports.originalSubtotalNote")]
    [InlineData("ui.dashboard.column.confirmedOrderAmount")]
    [InlineData("ui.dashboard.title.manager")]
    [InlineData("ui.dashboard.chart.mode.bar")]
    [InlineData("ui.dashboard.chart.mode.line")]
    [InlineData("ui.dashboard.chart.dataTable")]
    [InlineData("ui.dashboard.financial.composition")]
    [InlineData("ui.dashboardReports.orderStages")]
    [InlineData("ui.dashboardReports.otherStages")]
    public void ClarifiedOrderLabelsAreAvailableInEveryLanguage(string key)
    {
        var snapshot = new TranslationBootstrap().Snapshot;
        Assert.True(snapshot.Entries.ContainsKey(key));
        foreach (var language in TranslationPolicy.Languages)
        {
            var dictionary = snapshot.ForLanguage(language).Messages;
            Assert.True(dictionary.TryGetValue(key, out var text));
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.NotEqual(key, text);
        }
    }

    /// <summary>Dashboard və hesabat mətnlərinin dil parametrlərinin uyğunluğunu və təhlükəsiz mətn qaydalarını yoxlayır.</summary>
    [Fact]
    public void DashboardCatalogPreservesTranslationPlaceholders()
    {
        var entries = new TranslationBootstrap().Snapshot.Entries.Where(entry =>
            entry.Key.StartsWith("ui.dashboard.", StringComparison.Ordinal) ||
            entry.Key.StartsWith("ui.dashboardReports.", StringComparison.Ordinal) ||
            entry.Key.StartsWith("ui.operationalReports.", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(entries);
        foreach (var entry in entries)
            TranslationPolicy.ValidateText(entry.Value);
    }

    /// <summary>Bütün dil faylları yüklənəndən sonra mərkəzi kataloqun yayımlanma ölçü limitinə uyğun qaldığını yoxlayır.</summary>
    [Fact]
    public void CompleteCatalogFitsPublicationLimit()
    {
        var entries = new TranslationBootstrap().Snapshot.Entries;
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(entries, TranslationJson.Options);
        Assert.InRange(bytes.Length, 1, TranslationPolicy.MaxCatalogBytes);
    }

    /// <summary>Dashboard dil fayllarının kataloqun oxuduğu əsas assembly-yə daxil olmasını təmin edir.</summary>
    [Theory]
    [InlineData("az")]
    [InlineData("en")]
    [InlineData("ru")]
    public void DashboardDictionariesAreEmbeddedInBootstrapAssembly(string language)
    {
        var resources = typeof(TranslationBootstrap).Assembly.GetManifestResourceNames();
        Assert.Contains($"ECafe.Application.Services.Localization.Resources.dashboard.{language}.json", resources);
    }
}
