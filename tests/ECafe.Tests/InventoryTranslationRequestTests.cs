using ECafe.Api.Controllers;
using ECafe.Application.DTOs.InventoryItem;
using ECafe.Application.Features.Commands.InventoryItem.Create;
using ECafe.Application.Features.Commands.InventoryItem.Update;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class InventoryTranslationRequestTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateForwardsTranslationsAndStockFieldsInsideRouteRestaurant(bool hasTranslations)
    {
        var translations = hasTranslations ? new List<InventoryItemTranslationRequest>
            { new() { LanguageCode = "en", Name = "Honey" } } : null;
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator.Setup(bus => bus.Send(It.Is<CreateInventoryItemCommand>(command =>
            command.RestaurantId == 7 && command.Name == "Bal" && command.UnitId == 3 &&
            command.QuantityOnHand == 12.5m && command.LowStockThreshold == 2 &&
            ReferenceEquals(command.Translations, translations)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryItemDto { Id = 11 });
        using var provider = new ServiceCollection().AddSingleton(mediator.Object).BuildServiceProvider();
        var controller = CreateController(provider);
        var result = Assert.IsType<OkObjectResult>(await controller.Create(7, new CreateInventoryItemRequest
        {
            Name = "Bal", UnitId = 3, QuantityOnHand = 12.5m, LowStockThreshold = 2, Translations = translations
        }));
        Assert.Equal(11, Assert.IsType<InventoryItemDto>(result.Value).Id);
        mediator.VerifyAll();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task UpdatePreservesAbsentEmptyAndPopulatedTranslationSemantics(int mode)
    {
        List<InventoryItemTranslationRequest>? translations = mode switch
        {
            0 => null,
            1 => [],
            _ => [new() { LanguageCode = "ru", Name = "Med" }]
        };
        var mediator = new Mock<IMediator>(MockBehavior.Strict);
        mediator.Setup(bus => bus.Send(It.Is<UpdateInventoryItemCommand>(command =>
            command.RestaurantId == 7 && command.InventoryItemId == 11 && command.Name == "Bal" &&
            command.UnitId == 3 && command.LowStockThreshold == 2 && command.IsActive &&
            ReferenceEquals(command.Translations, translations)), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryItemDto { Id = 11 });
        using var provider = new ServiceCollection().AddSingleton(mediator.Object).BuildServiceProvider();
        var controller = CreateController(provider);
        await controller.Update(7, 11, new UpdateInventoryItemRequest
        {
            Name = "Bal", UnitId = 3, LowStockThreshold = 2, IsActive = true, Translations = translations
        });
        mediator.VerifyAll();
    }

    /// <summary>Controller-in həqiqi mediator həllini test provayderinə bağlayır; sorğu sahələrinin itirilməsini strict mock yoxlayır.</summary>
    private static InventoryController CreateController(IServiceProvider services) => new()
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } }
    };
}
