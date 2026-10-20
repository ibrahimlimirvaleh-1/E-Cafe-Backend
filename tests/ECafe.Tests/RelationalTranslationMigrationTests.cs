using ECafe.Infrastructure.Migrations;
using ECafe.Infrastructure.Migrations.Seeds;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace ECafe.Tests;

public sealed class RelationalTranslationMigrationTests
{
    [Fact]
    public void UpCopiesOldHistoryBeforeRemovingJsonColumns()
    {
        var operations = new RelationalMultilingualContent().UpOperations;
        var copy = Assert.Single(operations.OfType<SqlOperation>(), operation =>
            operation.Sql.Contains("jsonb_each(r.\"MessagesJson\")", StringComparison.Ordinal));
        var copyIndex = operations.ToList().IndexOf(copy);
        var removed = operations.OfType<DropColumnOperation>().ToList();
        Assert.Equal(new[] { "DraftJson", "MessagesJson" }, removed.Select(operation => operation.Name).Order().ToArray());
        Assert.All(removed, operation => Assert.True(operations.ToList().IndexOf(operation) > copyIndex));
        Assert.Contains("ON CONFLICT (\"KeywordId\", \"LanguageId\") DO UPDATE", copy.Sql);
        Assert.Contains("FROM ops.tables", copy.Sql);
        Assert.Contains("FROM inventory.units", copy.Sql);
    }

    [Fact]
    public void FrozenSeedIsEmbeddedAndIndependentOfRuntimeBootstrap()
    {
        var seed = RelationalTranslationCatalogSeed.ReadSql();
        Assert.Contains("INSERT INTO core.keywords", seed);
        Assert.Contains("INSERT INTO core.keyword_translations", seed);
        Assert.Contains("ON CONFLICT", seed);
        Assert.True(seed.Length > 300_000);
        var operations = new RelationalMultilingualContent().UpOperations;
        Assert.Contains(operations.OfType<SqlOperation>(), operation => operation.Sql == seed);
    }

    [Fact]
    public void DownProtectsBusinessTranslationsAndReconstructsHistoryBeforeDroppingTables()
    {
        var operations = new RelationalMultilingualContent().DownOperations.ToList();
        var guard = Assert.IsType<SqlOperation>(operations[0]);
        Assert.Contains("RAISE EXCEPTION", guard.Sql);
        Assert.Contains("\"LanguageId\" <> 1", guard.Sql);
        var columns = operations.OfType<AddColumnOperation>().ToList();
        Assert.Equal(2, columns.Count);
        Assert.All(columns, operation => Assert.Equal("{}", operation.DefaultValue));
        var restore = Assert.Single(operations.OfType<SqlOperation>(), operation =>
            operation.Sql.Contains("UPDATE core.translation_releases", StringComparison.Ordinal));
        Assert.Contains("jsonb_object_agg", restore.Sql);
        Assert.All(operations.OfType<DropTableOperation>(), operation =>
            Assert.True(operations.IndexOf(operation) > operations.IndexOf(restore)));
    }
}
