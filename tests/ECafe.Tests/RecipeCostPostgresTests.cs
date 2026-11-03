using System.Security.Claims;
using AutoMapper;
using ECafe.Application.DTOs.InventoryMovement;
using ECafe.Application.Mappings;
using ECafe.Application.Services.InventoryMovement.Abstract;
using ECafe.Application.Services.InventoryMovement.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.InventoryItem;
using ECafe.Infrastructure.Repositories.InventoryMovement;
using ECafe.Infrastructure.Repositories.InventoryMovementType;
using ECafe.Infrastructure.Repositories.Unit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed partial class OrderStockPostgresTests
{
    /// <summary>Alış vahidlərini, real stok kilidini və Americano mayasının sonrakı alışdan sonra dəyişməməsini yoxlayır.</summary>
    [StockPostgresFact]
    public async Task PricedReceiptsAndKitchenConsumptionKeepOriginalAmericanoCost()
    {
        await using var fixture = await Database.CreateAsync(0);
        await using (var db = fixture.Context())
        {
            (await db.InventoryItems.SingleAsync()).Name = "Coffee";
            (await db.Recipes.SingleAsync()).Quantity = 20;
            db.InventoryItems.Add(new InventoryItem
            {
                Id = 1002, RestaurantId = 1001, Name = "Water", UnitId = (int)UnitCode.Liter, IsActive = true
            });
            db.Recipes.Add(new Recipe
            {
                Id = 1002, RestaurantId = 1001, InventoryItemId = 1002, ItemId = 1001,
                UnitId = (int)UnitCode.Milliliter, Quantity = 50, IsActive = true
            });
            var line = await db.OrderItems.SingleAsync(row => row.Id == 1001);
            line.Quantity = 1;
            line.UnitPrice = line.UnitPriceSnapshot = line.LineTotal = 7m;
            await db.SaveChangesAsync();
        }
        await using (var db = fixture.Context())
        {
            var receipts = ReceiptManager(db);
            await receipts.CreateAsync(1001, 1001, new() { Quantity = 1, UnitId = (int)UnitCode.Kilogram, MovementTypeId = 1, PurchaseUnitPrice = 5m });
            await receipts.CreateAsync(1002, 1001, new() { Quantity = 1000, UnitId = (int)UnitCode.Milliliter, MovementTypeId = 1, PurchaseUnitPrice = 0.0005m });
            var error = await Assert.ThrowsAsync<BusinessRuleException>(() => receipts.CreateAsync(1001, 1001,
                new() { Quantity = 1, UnitId = (int)UnitCode.Kilogram, MovementTypeId = 2, PurchaseUnitPrice = 1m }));
            Assert.Equal(ErrorCode.InventoryPurchasePriceInvalid, error.Code);
        }
        await using (var db = fixture.Context())
        {
            await Service(db).StartPreparingAsync(1001, 1001);
            await Service(db).StartPreparingAsync(1001, 1001);
        }
        await using (var db = fixture.Context())
        {
            await ReceiptManager(db).CreateAsync(1001, 1001,
                new() { Quantity = 1, UnitId = (int)UnitCode.Kilogram, MovementTypeId = 1, PurchaseUnitPrice = 10m });
        }
        await using var read = fixture.Context();
        var costs = await read.InventoryMovements.Where(row => row.RelatedOrderId == 1001)
            .OrderBy(row => row.InventoryItemId).Select(row => row.CostAmount).ToListAsync();
        Assert.Equal(new decimal?[] { .1m, .025m }, costs);
        Assert.Equal(.125m, (await read.OrderItems.SingleAsync(row => row.Id == 1001)).IngredientCostSnapshot);
        Assert.Equal(14.9m, (await read.InventoryItems.SingleAsync(row => row.Id == 1001)).StockCostValue);
        Assert.Equal(.475m, (await read.InventoryItems.SingleAsync(row => row.Id == 1002)).StockCostValue);

        var history = await ReceiptManager(read, RoleCode.Manager).HistoryAsync(1002, 1001, new(1, 10));
        var purchase = Assert.Single(history.Items, row => row.MovementTypeId == (int)InventoryMovementTypeCode.Purchase);
        Assert.Equal("l", purchase.UnitCode);
        Assert.Equal(1m, purchase.QuantityChange);
        Assert.Equal(.5m, purchase.PurchaseCostAmount);
        Assert.Equal(.5m, purchase.PurchaseUnitPrice);
        Assert.Null(Assert.Single(history.Items, row => row.QuantityChange < 0).PurchaseCostAmount);

        var kitchenHistory = await ReceiptManager(read).HistoryAsync(1002, 1001, new(1, 10));
        var kitchenPurchase = Assert.Single(kitchenHistory.Items, row => row.Id == purchase.Id);
        Assert.Equal(purchase.QuantityChange, kitchenPurchase.QuantityChange);
        Assert.Null(kitchenPurchase.PurchaseCostAmount);
        Assert.Null(kitchenPurchase.PurchaseUnitPrice);
    }

    /// <summary>Real repository və transaction-larla alış girişini test edir; yalnız restoran lookup və bildiriş saxtalaşdırılır.</summary>
    private static InventoryMovementManager ReceiptManager(ECafeDbContext db, RoleCode role = RoleCode.Kitchen)
    {
        var restaurants = new Mock<ECafe.Application.Repositories.Restaurant.IRestaurantRepository>();
        restaurants.Setup(repository => repository.GetByIdAsync(1001)).ReturnsAsync(new Restaurant { Id = 1001 });
        restaurants.Setup(repository => repository.GetRestaurantInfoAsync(1001)).ReturnsAsync(new Restaurant { Id = 1001 });
        var mapper = new MapperConfiguration(config => config.AddProfile<InventoryMovementProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var accessor = Accessor();
        var identity = (ClaimsIdentity)accessor.HttpContext!.User.Identity!;
        identity.RemoveClaim(identity.FindFirst(ClaimTypes.Role)!);
        identity.AddClaim(new Claim(ClaimTypes.Role, ((int)role).ToString()));
        return new(accessor, mapper, new ConfigurationBuilder().Build(), new InventoryMovementRepository(db),
            restaurants.Object, new InventoryItemRepository(db), new InventoryMovementTypeRepository(db), new UnitRepository(db),
            Mock.Of<IInventoryLowStockNotifier>(), new Transactions(db));
    }
}
