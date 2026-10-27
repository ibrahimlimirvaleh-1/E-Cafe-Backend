using System.Security.Claims;
using System.Text.Json;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using ECafe.Application.Common.Exceptions;
using ECafe.Application.DTOs.InventoryMovement;
using ECafe.Application.Mappings;
using ECafe.Application.Repositories.Restaurant;
using ECafe.Application.Repository;
using ECafe.Application.Services.InventoryMovement.Abstract;
using ECafe.Application.Services.InventoryMovement.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories.InventoryItem;
using ECafe.Infrastructure.Repositories.InventoryMovement;
using ECafe.Infrastructure.Repositories.InventoryMovementType;
using ECafe.Infrastructure.Repositories.Unit;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class InventoryMovementHistoryTests
{
    [Fact]
    public void ProjectionKeepsOriginalSnapshotUnitOrderAndReturnLinkWithoutInventingABalance()
    {
        var source = Movement();
        source.MovementTypeId = 6;
        source.MovementType = new InventoryMovementType { Id = 6, Code = "StockReturn", Name = "Stoka qaytarma" };
        source.QuantityChange = 90.000001m;
        source.ReversedMovementId = 40;
        var result = new[] { source }.AsQueryable().ProjectTo<InventoryMovementHistoryResponse>(Mapper().ConfigurationProvider).Single();
        Assert.Equal("ml", result.UnitCode);
        Assert.Equal("StockReturn", result.MovementTypeCode);
        Assert.Equal("25", result.RelatedOrderId);
        Assert.Equal(26, result.RelatedOrderItemId);
        Assert.Equal("Latte", result.OrderItemName);
        Assert.Equal(2, result.OrderItemQuantity);
        Assert.Equal(3, result.TableNo);
        Assert.Equal("Asia/Baku", result.TimeZone);
        Assert.Equal(40, result.ReversedMovementId);
        Assert.Equal(90.000001m, result.QuantityChange);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("quantityAfterMovement", json);
        Assert.Contains("2026-10-27T14:05:00Z", json);
        Assert.Equal(8200, source.InventoryItem.QuantityOnHand);
    }

    [Theory]
    [InlineData("manual")] [InlineData("legacy")] [InlineData("foreignRestaurant")] [InlineData("wrongOrder")]
    public void MissingOrInconsistentOrderContextDoesNotInventProductOrTable(string state)
    {
        var source = Movement();
        if (state == "manual") { source.RelatedOrderId = null; source.RelatedOrderItemId = null; source.RelatedOrderItem = null; }
        if (state == "legacy") { source.RelatedOrderItemId = null; source.RelatedOrderItem = null; }
        if (state == "foreignRestaurant") source.RelatedOrderItem!.Order.RestaurantId = 2;
        if (state == "wrongOrder") source.RelatedOrderId = 99;
        var result = Mapper().Map<InventoryMovementHistoryResponse>(source);
        Assert.Null(result.OrderItemName);
        Assert.Null(result.OrderItemQuantity);
        Assert.Null(result.TableNo);
    }

    [Fact]
    public void ForeignTableCannotLeakIntoHistory()
    {
        var source = Movement();
        source.RelatedOrderItem!.Order.Table.RestaurantId = 2;
        Assert.Null(Mapper().Map<InventoryMovementHistoryResponse>(source).TableNo);
    }

    [Fact]
    public async Task HistoryHasStablePaginationAndDoesNotChangeStockOrExposeOtherIngredients()
    {
        await using var db = new ECafeDbContext(new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var first = Movement();
        db.InventoryMovements.Add(first);
        db.InventoryMovements.Add(new InventoryMovement { Id = 42, RestaurantId = 1, InventoryItemId = 71,
            UnitId = 72, MovementTypeId = 4, QuantityChange = -30, CreatedAt = first.CreatedAt });
        db.InventoryMovements.Add(new InventoryMovement { Id = 43, RestaurantId = 2, InventoryItemId = 71,
            UnitId = 72, MovementTypeId = 4, QuantityChange = -99, CreatedAt = first.CreatedAt });
        db.InventoryMovements.Add(new InventoryMovement { Id = 44, RestaurantId = 1, InventoryItemId = 99,
            UnitId = 72, MovementTypeId = 4, QuantityChange = -99, CreatedAt = first.CreatedAt });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = Service(db);
        var page1 = await service.HistoryAsync(71, 1, new PaginationFilter(1, 1));
        var page2 = await service.HistoryAsync(71, 1, new PaginationFilter(2, 1));
        Assert.Equal(2, page1.TotalCount);
        Assert.Equal(42, Assert.Single(page1.Items).Id);
        Assert.Equal(41, Assert.Single(page2.Items).Id);
        Assert.Equal("Latte", page2.Items[0].OrderItemName);
        Assert.Equal("ml", page1.Items[0].UnitCode);
        Assert.Equal(8200, (await db.InventoryItems.AsNoTracking().SingleAsync()).QuantityOnHand);
        Assert.Empty(db.ChangeTracker.Entries());
        await Assert.ThrowsAsync<ForbiddenException>(() => service.HistoryAsync(71, 2, new PaginationFilter(1, 10)));
        Assert.Equal(ErrorCode.InventoryItemNotFound,
            (await Assert.ThrowsAsync<NotFoundException>(() => service.HistoryAsync(99, 1, new PaginationFilter(1, 10)))).Code);
    }

    /// <summary>Stok tarixçəsini real oxuma repository-ləri və yalnız bir restorana aid istifadəçi ilə qurur; yazı xidmətləri çağırılmır.</summary>
    private static InventoryMovementManager Service(ECafeDbContext db)
    {
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("userId", "1"), new Claim("restaurantId", "1"), new Claim(ClaimTypes.Role, "3")], "test")) };
        var restaurants = new Mock<IRestaurantRepository>();
        restaurants.Setup(repository => repository.GetByIdAsync(1)).ReturnsAsync(new Restaurant { Id = 1 });
        return new InventoryMovementManager(new HttpContextAccessor { HttpContext = http }, Mapper(), new ConfigurationBuilder().Build(),
            new InventoryMovementRepository(db), restaurants.Object, new InventoryItemRepository(db),
            new InventoryMovementTypeRepository(db), new UnitRepository(db), Mock.Of<IInventoryLowStockNotifier>(),
            Mock.Of<IApplicationDbTransactionFactory>());
    }

    /// <summary>İstehsaldakı tarixçə proyeksiyasını test edir; cavab sahələri testdə ayrıca kopyalanmır.</summary>
    private static IMapper Mapper() => new MapperConfiguration(config => config.AddProfile<InventoryMovementProfile>(),
        Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateMapper();

    /// <summary>Cari menyu adı dəyişsə də ilkin məhsul adının və UTC vaxtının qalmasını yoxlamaq üçün sərfiyyat nümunəsi yaradır.</summary>
    private static InventoryMovement Movement()
    {
        var restaurant = new Restaurant { Id = 1, Name = "History", Location = "Baku", Phone = "1", TimeZone = "Asia/Baku" };
        return new InventoryMovement
        {
            Id = 41, RestaurantId = 1, InventoryItemId = 71, UnitId = 72, MovementTypeId = 4,
            Unit = new Unit { Id = 72, Code = "ml", Name = "Millilitr" },
            MovementType = new InventoryMovementType { Id = 4, Code = "OrderConsumption", Name = "Sifariş sərfiyyatı", Description = "Recipe consumption" },
            QuantityChange = -90, CreatedAt = new DateTime(2026, 10, 27, 14, 5, 0, DateTimeKind.Utc),
            InventoryItem = new InventoryItem { Id = 71, RestaurantId = 1, Restaurant = restaurant, Name = "Süd", UnitId = 72, QuantityOnHand = 8200 },
            RelatedOrderId = 25, RelatedOrderItemId = 26,
            RelatedOrderItem = new OrderItem { Id = 26, OrderId = 25, ItemId = 27, NameSnapshot = "Latte", Quantity = 2,
                Item = new Item { Id = 27, RestaurantId = 1, Name = "Renamed item" },
                Order = new Order { Id = 25, RestaurantId = 1, Restaurant = restaurant, TableId = 73,
                    Table = new Table { Id = 73, RestaurantId = 1, TableNo = 3 } } }
        };
    }
}
