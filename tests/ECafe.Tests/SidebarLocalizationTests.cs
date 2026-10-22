using ECafe.Application.Services.Localization;
using Xunit;

namespace ECafe.Tests;

public sealed class SidebarLocalizationTests
{
    private static readonly TranslationBootstrap Bootstrap = new();

    [Theory]
    [InlineData("az", "ui.nav.openSidebar", "Menyunu aç")]
    [InlineData("az", "ui.nav.closeSidebar", "Menyunu bağla")]
    [InlineData("az", "ui.nav.collapseSidebar", "Yan paneli yığ")]
    [InlineData("az", "ui.nav.expandSidebar", "Yan paneli genişləndir")]
    [InlineData("en", "ui.nav.openSidebar", "Open menu")]
    [InlineData("en", "ui.nav.closeSidebar", "Close menu")]
    [InlineData("en", "ui.nav.collapseSidebar", "Collapse sidebar")]
    [InlineData("en", "ui.nav.expandSidebar", "Expand sidebar")]
    [InlineData("ru", "ui.nav.openSidebar", "Открыть меню")]
    [InlineData("ru", "ui.nav.closeSidebar", "Закрыть меню")]
    [InlineData("ru", "ui.nav.collapseSidebar", "Свернуть панель")]
    [InlineData("ru", "ui.nav.expandSidebar", "Развернуть панель")]
    public void SidebarControlsAreAvailableInEachLanguage(string language, string key, string expected)
        => Assert.Equal(expected, Bootstrap.Snapshot.ForLanguage(language).Messages[key]);
}
