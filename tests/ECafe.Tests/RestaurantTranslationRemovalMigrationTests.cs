using ECafe.Application.DTOs.Restaurant;
using ECafe.Application.DTOs.RestaurantGroup;
using ECafe.Domain.Enums;
using ECafe.Infrastructure.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ECafe.Tests;

public sealed class RestaurantTranslationRemovalMigrationTests
{
    [Fact]
    public void MigrationDropsOnlyTheTwoRemovedTranslationTables()
    {
        var operations = new RemoveRestaurantTranslations().UpOperations;
        Assert.Equal(2, operations.Count);
        var tables = operations.Select(operation => Assert.IsType<DropTableOperation>(operation)).ToList();
        Assert.All(tables, table => Assert.Equal("core", table.Schema));
        Assert.Equal(new[] { "restaurant_group_translations", "restaurant_translations" }, tables.Select(table => table.Name).Order());
    }

    [Fact]
    public void RollbackRestoresOnlyEmptyTranslationTablesAndTheirIndexes()
    {
        var operations = new RemoveRestaurantTranslations().DownOperations;
        Assert.Equal(2, operations.OfType<CreateTableOperation>().Count());
        Assert.All(operations, operation => Assert.True(operation is CreateTableOperation or CreateIndexOperation));
        Assert.DoesNotContain(operations, operation => operation is InsertDataOperation or UpdateDataOperation or DeleteDataOperation);
    }

    [Fact]
    public void RestaurantRequestsHaveNoTranslationFieldAndOtherContentLockIdsStayStable()
    {
        Assert.Null(typeof(RegisterRestaurantRequest).GetProperty("Translations"));
        Assert.Null(typeof(UpdateRestaurantRequest).GetProperty("Translations"));
        Assert.Null(typeof(CreateRestaurantGroupRequest).GetProperty("Translations"));
        Assert.Equal(new[] { 3, 4, 5, 6, 7 }, Enum.GetValues<ContentEntityType>().Select(type => (int)type));
    }
}
