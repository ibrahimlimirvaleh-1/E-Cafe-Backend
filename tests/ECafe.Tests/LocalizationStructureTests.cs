using ECafe.Domain.Constants;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace ECafe.Tests;

public sealed class LocalizationStructureTests
{
    [Theory]
    [InlineData(LanguageCode.Az, 1, "az", "Azərbaycan")]
    [InlineData(LanguageCode.En, 2, "en", "English")]
    [InlineData(LanguageCode.Ru, 3, "ru", "Русский")]
    public void EnumAndSeederRetainExistingIdsCodesAndNativeNames(LanguageCode language, int id, string code, string name)
    {
        using var db = Database();
        var seed = Assert.Single(db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Language))!.GetSeedData(),
            row => (int)row["Id"]! == id);
        Assert.Equal(id, (int)language);
        Assert.Equal(code, TranslationPolicy.Code(language));
        Assert.Equal(language, TranslationPolicy.ParseLanguage(code));
        Assert.Equal(id, TranslationPolicy.LanguageId(code));
        Assert.Equal(code, seed["Code"]);
        Assert.Equal(name, seed["Name"]);
        Assert.Equal(true, seed["IsActive"]);
        Assert.Equal(language == LanguageCode.Az, seed["IsDefault"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("Az")]
    [InlineData("en-US")]
    [InlineData("de")]
    public void WriteParserDoesNotAcceptEnumNumbersAliasesOrUnsupportedLanguages(string? value)
    {
        var error = Assert.Throws<BusinessRuleException>(() => TranslationPolicy.ParseLanguage(value));
        Assert.Equal(ErrorCode.LocalizationInvalidLanguage, error.Code);
    }

    [Theory]
    [InlineData("en-US", "en", 2)]
    [InlineData(" RU_ru ", "ru", 3)]
    [InlineData("az-Latn-AZ", "az", 1)]
    [InlineData("de-DE", "az", 1)]
    [InlineData(null, "az", 1)]
    public void ReadPreferenceKeepsRegionalNormalizationAndDefaultFallback(string? value, string code, int id)
    {
        Assert.Equal(code, TranslationPolicy.NormalizeLanguage(value));
        Assert.Equal(id, TranslationPolicy.LanguageId(value));
    }

    [Fact]
    public void EverySupportedEnumHasOneSeedAndAConcreteDictionaryField()
    {
        using var db = Database();
        var seeds = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Language))!.GetSeedData().ToList();
        Assert.Equal(Enum.GetValues<LanguageCode>().Length, seeds.Count);
        Assert.Equal(new[] { "az", "en", "ru" }, TranslationPolicy.Languages);
        var texts = new TranslationText("AZ", "EN", "RU");
        Assert.Equal(new[] { "AZ", "EN", "RU" }, Enum.GetValues<LanguageCode>().Select(language => TranslationPolicy.TextFor(texts, language)));
        Assert.False(TranslationPolicy.IsAdditionalLanguageId((int)LanguageCode.Az));
        Assert.True(TranslationPolicy.IsAdditionalLanguageId((int)LanguageCode.En));
        Assert.True(TranslationPolicy.IsAdditionalLanguageId((int)LanguageCode.Ru));
        Assert.False(TranslationPolicy.IsAdditionalLanguageId(99));
        Assert.Throws<ArgumentOutOfRangeException>(() => TranslationPolicy.Code((LanguageCode)99));
    }

    [Fact]
    public void CatalogSeederOnlyDefinesOriginalEmptySingleton()
    {
        using var db = Database();
        var seed = Assert.Single(db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(TranslationCatalog))!.GetSeedData());
        Assert.Equal(1, LocalizationDefaults.CatalogId);
        Assert.Equal(LocalizationDefaults.CatalogId, seed["Id"]);
        Assert.Equal(0L, seed["Revision"]);
        Assert.Null(seed["PublishedReleaseId"]);
        Assert.Equal(LocalizationDefaults.CatalogId, new TranslationCatalog().Id);
    }

    /// <summary>Fiziki bazaya toxunmadan seeder və EF model metadatasını ayrıca yaddaş kontekstində yoxlayır.</summary>
    private static ECafeDbContext Database() => new(new DbContextOptionsBuilder<ECafeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
