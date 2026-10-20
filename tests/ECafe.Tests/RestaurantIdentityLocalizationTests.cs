using System.Globalization;
using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.DTOs.Restaurant.Public;
using ECafe.Application.DTOs.RestaurantGroup;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.DTOs.User;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Services.ContentLocalization.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class RestaurantIdentityLocalizationTests
{
    [Theory]
    [InlineData("az")]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task BrandBranchAddressAndGroupStayCanonicalAcrossEverySurface(string language)
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);
            var query = new Mock<IContentTranslationQuery>(MockBehavior.Strict);
            query.Setup(repository => repository.GetRowsAsync(ContentEntityType.Table, It.IsAny<IReadOnlyCollection<int>>(),
                language, It.IsAny<CancellationToken>())).ReturnsAsync([new ContentTranslationRow(8, language, 1, new("Translated table"))]);
            var list = new PublicRestaurantListItemDto { Id = 7, Name = "Dolce Vita", BranchName = "Sahil", Location = "Baki", RestaurantGroupId = 2, RestaurantGroupName = "Brand" };
            var detail = new RestaurantDetailDto { Name = "Dolce Vita", BranchName = "Sahil", Location = "Baki", RestaurantGroupId = 2, RestaurantGroupName = "Brand" };
            var group = new RestaurantGroupResponse { Id = 2, Name = "Brand", LegalName = "Brand MMC" };
            var reservation = new ReservationResponse { RestaurantId = 7, RestaurantName = "Dolce Vita", TableId = 8, TableName = "Masa" };
            var profile = new ProfileResponseDto { RestaurantId = 7, RestaurantName = "Dolce Vita" };
            await new ContentTranslationReader(query.Object).LocalizeAsync(new object[] { list, detail, group, reservation, profile }, default, new(7));
            Assert.Equal("Dolce Vita", list.Name);
            Assert.Equal("Sahil", list.BranchName);
            Assert.Equal("Baki", list.Location);
            Assert.Equal("Brand", list.RestaurantGroupName);
            Assert.Equal("Dolce Vita", detail.Name);
            Assert.Equal("Sahil", detail.BranchName);
            Assert.Equal("Baki", detail.Location);
            Assert.Equal("Brand", detail.RestaurantGroupName);
            Assert.Equal("Brand", group.Name);
            Assert.Equal("Dolce Vita", reservation.RestaurantName);
            Assert.Equal("Dolce Vita", profile.RestaurantName);
            Assert.Equal(language == "az" ? "Masa" : "Translated table", reservation.TableName);
            if (language == "az") query.VerifyNoOtherCalls();
            else query.Verify(repository => repository.GetRowsAsync(ContentEntityType.Table, It.IsAny<IReadOnlyCollection<int>>(), language, It.IsAny<CancellationToken>()), Times.Once);
        }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    [Fact]
    public void SearchUsesCanonicalIdentityAndStillFindsTranslatedMenu()
    {
        var group = new RestaurantGroup { Name = "Brand" };
        var restaurant = new Restaurant { Name = "Cafe", BranchName = "Sahil", Location = "Baki", Phone = "000", RestaurantGroup = group,
            Categories = [new() { Name = "Ickiler", Slug = "ickiler", IsActive = true, Items = [new() { Name = "Cay", IsActive = true, IsAvailable = true,
                Translations = [new() { LanguageId = 2, Name = "EnglishTea" }] }] }] };
        var query = new[] { restaurant }.AsQueryable();
        Assert.Empty(RestaurantSearchQuery.Search(query, "unknown", 2, true));
        Assert.Single(RestaurantSearchQuery.Search(query, "cafe", 2, false));
        Assert.Single(RestaurantSearchQuery.Search(query, "englishtea", 2, true));
    }
}
