using System.Globalization;
using ECafe.Application.DTOs.ContentLocalization;
using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.DTOs.InventoryMovement;
using ECafe.Application.DTOs.Lookup;
using ECafe.Application.DTOs.MappingTests;
using ECafe.Application.DTOs.Order;
using ECafe.Application.DTOs.Recipe;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.DTOs.Restaurant.Public;
using ECafe.Application.DTOs.RestaurantGroup;
using ECafe.Application.DTOs.Table;
using ECafe.Application.DTOs.TableSession;
using ECafe.Application.DTOs.User;
using ECafe.Application.Mappings;
using ECafe.Application.Mappings.ContentLocalization;
using ECafe.Application.Repositories.ContentLocalization;
using ECafe.Application.Services.ContentLocalization.Concrete;
using ECafe.Domain.Enums;
using ECafe.Shared.DTOs;
using Moq;
using Xunit;

namespace ECafe.Tests
{
    public sealed class ContentResponseProfileTests
    {
        [Fact]
        public void KitchenStockPreviewLocalizesNamesWithoutChangingOriginalQuantitiesOrUnitCodes()
        {
            var ingredient = new KitchenStockReturnIngredientResponse(71, "Süd", 2, "Millilitr", "ml", 400.123456m);
            var bindings = ContentTranslationReadMapper.Collect(new KitchenStockReturnPreviewResponse(25, [ingredient]));
            Assert.Equal(2, bindings.Count);
            foreach (var binding in bindings) ContentTranslationReadMapper.Apply(binding, new("Translated"));
            Assert.Equal("Translated", ingredient.Name);
            Assert.Equal("Translated", ingredient.UnitName);
            Assert.Equal("ml", ingredient.UnitCode);
            Assert.Equal(400.123456m, ingredient.ConsumedQuantity);
        }

        [Fact]
        public void AdditionalDtoProfileExtendsCollectionWithoutTraversalChanges()
        {
            var response = new AdditionalContentDto { Id = 12, Label = "Esas", PersonalName = "Ali" };
            Assert.Empty(ContentTranslationReadMapper.Collect(response));
            var profiles = ContentResponseProfiles.Default.Append(AdditionalProfile());
            var binding = Assert.Single(ContentTranslationReadMapper.Collect(response, profiles));
            Assert.Equal(ContentEntityType.Item, binding.Type);
            Assert.Equal(12, binding.Id);
            ContentTranslationReadMapper.Apply(binding, new("Translated"));
            Assert.Equal("Translated", response.Label);
            Assert.Equal("Ali", response.PersonalName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SparseTranslationPreservesCanonicalFallback(string? name)
        {
            var response = new PublicMenuItemDto { Id = 7, Name = "Cay", Description = "Esas metn" };
            foreach (var binding in ContentTranslationReadMapper.Collect(response))
                ContentTranslationReadMapper.Apply(binding, new(name, "Hot tea"));
            Assert.Equal("Cay", response.Name);
            Assert.Equal("Hot tea", response.Description);
        }

        [Fact]
        public void ContextNeverBindsRestaurantButResolvesExplicitUnitLookup()
        {
            var restaurant = new RestaurantDetailDto { Name = "Esas", Location = "Baki" };
            Assert.Empty(ContentTranslationReadMapper.Collect(restaurant));
            var bindings = ContentTranslationReadMapper.Collect(restaurant, restaurantId: 23);
            Assert.Empty(bindings);
            var lookup = new LookupItemResponse { Id = 5, Name = "kq" };
            Assert.Empty(ContentTranslationReadMapper.Collect(lookup));
            var unit = Assert.Single(ContentTranslationReadMapper.Collect(lookup, unitLookup: true));
            Assert.Equal(ContentEntityType.Unit, unit.Type);
            ContentTranslationReadMapper.Apply(unit, new("kg"));
            Assert.Equal("kg", lookup.Name);
        }

        [Fact]
        public void TypedBindingsSupportInitPropertiesWithoutChangingOrderSnapshotsOrFreeText()
        {
            var reservation = new ReservationResponse { RestaurantId = 2, RestaurantName = "Cafe", TableId = 9, TableName = "Masa" };
            var line = new CustomerOrderLineResponse(1, 7, "Immutable tea", 2, 3, 6);
            var kitchenLine = new KitchenOrderLineResponse(1, 7, "Immutable tea", 2, "Do not translate");
            var profile = new ProfileResponseDto { Id = 3, Name = "Ali", Surname = "Valiyev", RestaurantId = 2, RestaurantName = "Cafe" };
            var group = new RestaurantGroupResponse { Id = 8, Name = "Brand", LegalName = "Legal MMC" };
            var editor = new ContentTranslationsResponse("items", 7, 2, [new("az", "Raw", null, null, 1)]);
            var response = new object[] { reservation, line, kitchenLine, profile, group, editor };
            var bindings = ContentTranslationReadMapper.Collect(response);
            Assert.Single(bindings);
            foreach (var binding in bindings) ContentTranslationReadMapper.Apply(binding, new("Translated"));
            Assert.Equal("Cafe", reservation.RestaurantName);
            Assert.Equal("Translated", reservation.TableName);
            Assert.Equal("Immutable tea", line.Name);
            Assert.Equal("Immutable tea", kitchenLine.Name);
            Assert.Equal("Do not translate", kitchenLine.Note);
            Assert.Equal("Ali", profile.Name);
            Assert.Equal("Valiyev", profile.Surname);
            Assert.Equal("Cafe", profile.RestaurantName);
            Assert.Equal("Brand", group.Name);
            Assert.Equal("Legal MMC", group.LegalName);
            Assert.Equal("Raw", editor.Translations[0].Name);
        }

        [Fact]
        public void TraversalRetainsCycleDepthNamespaceAndExactTypeGuards()
        {
            var root = new AdditionalContentDto { Id = 1 };
            root.Child = root;
            Assert.Single(ContentTranslationReadMapper.Collect(root, [AdditionalProfile()]));
            var tail = root;
            for (var i = 2; i <= 30; i++)
            {
                tail.Child = new AdditionalContentDto { Id = i };
                tail = tail.Child;
            }
            Assert.Equal(13, ContentTranslationReadMapper.Collect(root, [AdditionalProfile()]).Count);
            Assert.Empty(ContentTranslationReadMapper.Collect(new OutsideDto { Child = root }, [AdditionalProfile()]));
            Assert.Empty(ContentTranslationReadMapper.Collect(new DerivedContentDto { Id = 5 }, [AdditionalProfile()]));
            Assert.Empty(ContentTranslationReadMapper.Collect(new ECafe.Application.DTOsUntrusted.Envelope { Child = root }, [AdditionalProfile()]));
        }

        [Fact]
        public void ExplicitProfileCannotOptRawEditorDtosIntoTraversal()
        {
            var value = new ContentTranslationValue("Raw");
            var response = new ContentTranslationsResponse("items", 7, 1, [new("az", "Raw", null, null, 1)]);
            var intercepted = false;
            var profile = new InterceptEditorProfile(() => intercepted = true);
            Assert.Empty(ContentTranslationReadMapper.Collect(response, [profile]));
            Assert.False(intercepted);
            Assert.Equal("Raw", value.Name);
        }

        [Fact]
        public void MissingAndNonpositiveIdentifiersNeverProduceBindings()
        {
            Assert.Empty(ContentTranslationReadMapper.Collect(new PublicMenuItemDto { Id = 0 }));
            Assert.Empty(ContentTranslationReadMapper.Collect(new PublicMenuItemDto { Id = -1 }));
            Assert.Empty(ContentTranslationReadMapper.Collect(new ProfileResponseDto { RestaurantId = null }));
        }

        [Theory]
        [MemberData(nameof(ExistingResponses))]
        public void ExplicitProfilesPreserveEveryPreviouslySupportedResponse(object response, int count)
        {
            var bindings = ContentTranslationReadMapper.Collect(response, restaurantId: 11, unitLookup: true);
            Assert.Equal(count, bindings.Count);
            Assert.All(bindings, binding => Assert.True(binding.Id > 0));
            foreach (var binding in bindings)
                ContentTranslationReadMapper.Apply(binding, new("Translated", "Description", "Unavailable"));
        }

        [Fact]
        public async Task ExistingReaderKeepsOneBatchPerEntityAndDeduplicatesRepeatedIds()
        {
            var previous = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en");
                var repository = new Mock<IContentTranslationRepository>();
                repository.Setup(r => r.GetRowsAsync(It.IsAny<ContentEntityType>(), It.IsAny<IReadOnlyCollection<int>>(),
                    "en", It.IsAny<CancellationToken>())).ReturnsAsync((ContentEntityType type, IReadOnlyCollection<int> ids,
                    string? _, CancellationToken _) => (IReadOnlyList<ContentTranslationRow>)ids.Select(id =>
                        new ContentTranslationRow(id, "en", 1, new(type.ToString()))).ToList());
                var first = new InventoryItemDto { Id = 7, UnitId = 4, Name = "Sugar", UnitName = "Original unit" };
                var second = new InventoryItemDto { Id = 7, UnitId = 4, Name = "Sugar", UnitName = "Original unit" };
                var page = new PaginatedList<InventoryItemDto>([first, second], 20, 2, 2);
                await new ContentTranslationReader(repository.Object).LocalizeAsync(page, default);
                Assert.Equal("InventoryItem", first.Name);
                Assert.Equal("Unit", first.UnitName);
                Assert.Equal("InventoryItem", second.Name);
                Assert.Equal(20, page.TotalCount);
                Assert.Equal(2, page.PageIndex);
                foreach (var type in new[] { ContentEntityType.InventoryItem, ContentEntityType.Unit })
                    repository.Verify(r => r.GetRowsAsync(type, It.Is<IReadOnlyCollection<int>>(ids => ids.Count == 1),
                        "en", It.IsAny<CancellationToken>()), Times.Once);
                repository.VerifyNoOtherCalls();
            }
            finally { CultureInfo.CurrentUICulture = previous; }
        }

        /// <summary>Əvvəlki ağ siyahının bütün DTO növlərini və icazəli bağ saylarını müqayisə üçün verir.</summary>
        public static IEnumerable<object[]> ExistingResponses()
        {
            yield return [new GetAllRestaurantsResponse { Id = 1, RestaurantGroupId = 2 }, 0];
            yield return [new PublicRestaurantListItemDto { Id = 1, RestaurantGroupId = 2 }, 0];
            yield return [new PublicRestaurantDetailDto { Id = 1, RestaurantGroupId = 2 }, 0];
            yield return [new RestaurantDetailDto { RestaurantGroupId = 2 }, 0];
            yield return [new RestaurantGroupResponse { Id = 1 }, 0];
            yield return [new ECafe.Application.DTOs.Category.GetAllCategoryResponse { Id = 1 }, 1];
            yield return [new CategoryDto { Id = 1 }, 1];
            yield return [new PublicMenuCategoryDto { Id = 1 }, 1];
            yield return [new ECafe.Application.DTOs.Item.ItemDto { Id = 1, CategoryId = 2 }, 3];
            yield return [new ECafe.Application.DTOs.Restaurant.ItemDto { Id = 1 }, 3];
            yield return [new PublicMenuItemDto { Id = 1 }, 2];
            yield return [new TableDto { Id = 1 }, 1];
            yield return [new TableResponse { Id = 1 }, 1];
            yield return [new PublicTableDto { Id = 1 }, 1];
            yield return [new InventoryItemDto { Id = 1, UnitId = 2 }, 2];
            yield return [new RecipeDto { ItemId = 1, InventoryItemId = 2, UnitId = 3 }, 3];
            yield return [new InventoryMovementResponse { UnitId = 1 }, 1];
            yield return [new InventoryMovementHistoryResponse { UnitId = 1 }, 1];
            yield return [new LookupItemResponse { Id = 1 }, 1];
            yield return [new ReservationResponse { RestaurantId = 1, TableId = 2 }, 1];
            yield return [new ReservationServiceItemResponse { RestaurantId = 1, TableId = 2 }, 1];
            yield return [new WaiterTableSessionResponse { RestaurantId = 1, TableId = 2 }, 1];
            yield return [new CustomerTableSessionResponse(1, 2, "Cafe", 3, "Table", null, 1, DateTimeOffset.UtcNow, null, "Asia/Baku"), 1];
            yield return [new KitchenOrderResponse(1, 2, 3, "Table", null, null, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
                null, [], "Asia/Baku", null, null, null, null, null, null, true, false, false), 1];
            yield return [new ProfileResponseDto { RestaurantId = 1 }, 0];
            yield return [new UserProfileAssignmentDto { RestaurantId = 1 }, 0];
        }

        /// <summary>Yeni DTO-nun yalnız etiketini qeydiyyata alır; fərdi ad və digər sahələri daxil etmir.</summary>
        private static IContentResponseProfile AdditionalProfile() => new ContentResponseProfile<AdditionalContentDto>(
            new ContentResponseField<AdditionalContentDto>(ContentEntityType.Item, (r, _) => r.Id, r => r.Label, v => v.Name));

        private sealed class OutsideDto
        {
            public AdditionalContentDto? Child { get; init; }
        }

        private sealed class InterceptEditorProfile(Action intercepted) : IContentResponseProfile
        {
            public Type ResponseType => typeof(ContentTranslationsResponse);

            /// <summary>Xam redaktor cavabının heç vaxt profilə ötürülmədiyini sınaqda müşahidə edir.</summary>
            public void Collect(object response, ContentResponseContext context, ICollection<ContentReadBinding> bindings) => intercepted();
        }
    }
}

namespace ECafe.Application.DTOs.MappingTests
{
    public class AdditionalContentDto
    {
        public int Id { get; init; }
        public string? Label { get; set; }
        public string? PersonalName { get; init; }
        public AdditionalContentDto? Child { get; set; }
    }

    public sealed class DerivedContentDto : AdditionalContentDto;
}

namespace ECafe.Application.DTOsUntrusted
{
    public sealed class Envelope
    {
        public AdditionalContentDto? Child { get; init; }
    }
}
