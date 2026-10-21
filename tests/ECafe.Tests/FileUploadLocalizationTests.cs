using ECafe.Application.Services.Localization;
using ECafe.Domain.Policies;
using Xunit;

namespace ECafe.Tests;

public sealed class FileUploadLocalizationTests
{
    private static readonly TranslationBootstrap Bootstrap = new();

    [Fact]
    public void BundledUploadMessagesExistInEverySupportedLanguage()
    {
        string[] keys = ["ui.fileUpload.choose", "ui.fileUpload.uploading", "ui.fileUpload.tooLarge",
            "ui.fileUpload.uploaded", "ui.fileUpload.failed"];
        foreach (var key in keys)
        {
            var entry = Bootstrap.Snapshot.Entries[key];
            TranslationPolicy.ValidateText(entry);
            Assert.False(string.IsNullOrWhiteSpace(entry.Az));
            Assert.False(string.IsNullOrWhiteSpace(entry.En));
            Assert.False(string.IsNullOrWhiteSpace(entry.Ru));
            foreach (var language in TranslationPolicy.Languages)
                Assert.False(string.IsNullOrWhiteSpace(Bootstrap.Snapshot.ForLanguage(language).Messages[key]));
        }
    }

    [Theory]
    [InlineData("az", "Fayl seç")]
    [InlineData("en", "Choose file")]
    [InlineData("ru", "Выбрать файл")]
    public void ChooseFileUsesRequestedLanguage(string language, string expected)
        => Assert.Equal(expected, Bootstrap.Snapshot.ForLanguage(language).Messages["ui.fileUpload.choose"]);

    [Fact]
    public void UploadParametersRemainCompatibleInAllLanguages()
    {
        foreach (var language in TranslationPolicy.Languages)
        {
            var messages = Bootstrap.Snapshot.ForLanguage(language).Messages;
            Assert.Equal("maxSizeMb", Assert.Single(TranslationPolicy.Placeholders(messages["ui.fileUpload.tooLarge"])));
            Assert.Equal("id", Assert.Single(TranslationPolicy.Placeholders(messages["ui.fileUpload.uploaded"])));
        }
    }
}
