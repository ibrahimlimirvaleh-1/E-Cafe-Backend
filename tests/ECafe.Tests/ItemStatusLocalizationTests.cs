using AutoMapper;
using ECafe.Api.Filters;
using ECafe.Application.DTOs.Item;
using ECafe.Application.DTOs.Lookup;
using ECafe.Application.DTOs.Reservation;
using ECafe.Application.DTOs.User;
using ECafe.Application.Mappings;
using ECafe.Application.Services.Localization;
using ECafe.Application.Services.Localization.Concrete;
using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Policies;
using ECafe.Infrastructure.Context;
using ECafe.Shared.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ECafe.Tests;

public sealed class ItemStatusLocalizationTests
{
    private static readonly TranslationBootstrap Bootstrap = new();

    [Theory]
    [InlineData("az", "Mövcuddur", "Məhduddur", "Bitib")]
    [InlineData("en", "Available", "Limited", "Out of stock")]
    [InlineData("ru", "В наличии", "Ограничено", "Нет в наличии")]
    public async Task RealSeededItemStatusesLocalizeInsideMenuResponseWithoutChangingContent(
        string language, string available, string limited, string outOfStock)
    {
        await using var db = Database();
        await db.Database.EnsureCreatedAsync();
        var states = await db.Statuses.Where(row => row.StatusTypeId == (int)ECafe.Domain.Enums.StatusType.ItemStatus)
            .OrderBy(row => row.Id).ToListAsync();
        Assert.Equal(new[] { 5001, 5002, 5003 }, states.Select(row => row.Id));
        Assert.Equal(Enum.GetValues<ItemStatus>().Length, states.Count);
        var originalNames = states.Select(row => row.Name).ToArray();
        var entities = states.Select((state, index) => new Item
        {
            Id = 7 + index, RestaurantId = 1, Name = "Mövcuddur", Description = "Bitib",
            CategoryId = 4, Category = new Category { Id = 4, Name = "Məhduddur" }, Status = state,
            StatusId = state.Id, BasePrice = 12.5m, IsActive = true, SalesCount = 8
        }).ToList();
        var mapper = new MapperConfiguration(configuration => configuration.AddProfile<ItemProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var items = mapper.Map<List<ItemDto>>(entities);
        var page = new PaginatedList<ItemDto>(items, 23, 2, 10);
        var menu = new GetAllItemResponse { Items = page };
        await LocalizeAsync(menu, language);
        Assert.Equal(new[] { available, limited, outOfStock }, items.Select(row => row.StatusName));
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            Assert.Equal(states[index].Id, item.StatusId);
            Assert.Equal(7 + index, item.Id);
            Assert.Equal(4, item.CategoryId);
            Assert.Equal("Mövcuddur", item.Name);
            Assert.Equal("Bitib", item.Description);
            Assert.Equal("Məhduddur", item.CategoryName);
            Assert.Equal(12.5m, item.BasePrice);
            Assert.Equal(8, item.SalesCount);
            Assert.True(item.IsActive);
        }
        Assert.Equal(23, page.TotalCount);
        Assert.Equal(2, page.PageIndex);
        Assert.Equal(originalNames, states.Select(row => row.Name));
        Assert.False(db.ChangeTracker.HasChanges());
    }

    [Theory]
    [InlineData("az", "Yeni status")]
    [InlineData("en", "Edited status")]
    [InlineData("ru", "Изменённый статус")]
    public async Task PublishedItemStatusOverridesKeepAllThreeBuildSourceAliasesDeterministic(string language, string expected)
    {
        string[] sources = ["Mövcuddur", "Məhduddur", "Bitib"];
        string[] keys = ["api.literal.catalog.itemAvailable", "api.literal.catalog.itemLimited", "api.literal.catalog.itemOutOfStock"];
        var entries = Bootstrap.Snapshot.Entries.ToDictionary(row => row.Key, row => row.Value);
        foreach (var key in keys) entries[key] = new TranslationText("Yeni status", "Edited status", "Изменённый статус");
        var snapshot = new TranslationSnapshot("published-item-test", entries);
        for (var index = 0; index < sources.Length; index++)
        {
            var item = new ItemDto { Id = 7, StatusId = 5001 + index, StatusName = sources[index] };
            var lookup = new LookupItemResponse { Id = item.StatusId, Code = ((ItemStatus)(index + 1)).ToString(), Name = sources[index] };
            await LocalizeAsync(new object[] { item, lookup }, language, snapshot);
            Assert.Equal(expected, item.StatusName);
            Assert.Equal(expected, lookup.Name);
            Assert.Equal(5001 + index, item.StatusId);
            Assert.Equal(item.StatusId, lookup.Id);
            Assert.Equal(((ItemStatus)(index + 1)).ToString(), lookup.Code);
            Assert.Equal(keys[index], Bootstrap.SourceMatcher.Match(sources[index])!.Key);
        }
    }

    [Theory]
    [InlineData("az")]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task RawStatusesUserNamesAndOtherItemContentRemainUntouched(string language)
    {
        var raw = new Status { Id = StatusIds.Item(ItemStatus.Available), Name = "Mövcuddur" };
        var profile = new ProfileResponseDto { Name = "Mövcuddur", Surname = "Məhduddur", RestaurantName = "Bitib", Role = "Mövcuddur" };
        var reservation = new ReservationResponse { Status = "Mövcuddur", RestaurantName = "Bitib", TableName = "Məhduddur" };
        var publicItem = new ECafe.Application.DTOs.Restaurant.ItemDto
        {
            Name = "Mövcuddur", Description = "Məhduddur", UnavailableReason = "Bitib", BasePrice = 10m
        };
        var item = new ItemDto { Name = "Bitib", StatusId = 5002, FileUrl = "/files/Bitib", StatusName = null };
        await LocalizeAsync(new object[] { raw, profile, reservation, publicItem, item }, language);
        Assert.Equal("Mövcuddur", raw.Name);
        Assert.Equal(5001, raw.Id);
        Assert.Equal("Mövcuddur", profile.Name);
        Assert.Equal("Məhduddur", profile.Surname);
        Assert.Equal("Bitib", profile.RestaurantName);
        Assert.Equal("Mövcuddur", profile.Role);
        Assert.Equal("Mövcuddur", reservation.Status);
        Assert.Equal("Bitib", reservation.RestaurantName);
        Assert.Equal("Məhduddur", reservation.TableName);
        Assert.Equal("Mövcuddur", publicItem.Name);
        Assert.Equal("Məhduddur", publicItem.Description);
        Assert.Equal("Bitib", publicItem.UnavailableReason);
        Assert.Equal(10m, publicItem.BasePrice);
        Assert.Equal("Bitib", item.Name);
        Assert.Equal("/files/Bitib", item.FileUrl);
        Assert.Equal(5002, item.StatusId);
        Assert.Null(item.StatusName);
    }

    /// <summary>Sorğunun sistem lüğətini və həqiqi API filtrini işlədərək yalnız icazəli status görünüşünü dəyişir.</summary>
    private static async Task LocalizeAsync(object response, string language, TranslationSnapshot? snapshot = null)
    {
        var http = new DefaultHttpContext();
        http.Items[TextLocalizer.ContextKey] = (snapshot ?? Bootstrap.Snapshot).ForLanguage(language);
        var text = new TextLocalizer(new HttpContextAccessor { HttpContext = http }, Bootstrap);
        var context = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var result = new OkObjectResult(response);
        await new LocalizedResponseFilter(text).OnResultExecutionAsync(new ResultExecutingContext(context, [], result, new object()),
            () => Task.FromResult(new ResultExecutedContext(context, [], result, new object())));
    }

    /// <summary>İstehsal status seeder-ini ayrıca EF yaddaş bazasında şəbəkəyə çıxmadan yoxlamağa imkan verir.</summary>
    private static ECafeDbContext Database() => new(new DbContextOptionsBuilder<ECafeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
