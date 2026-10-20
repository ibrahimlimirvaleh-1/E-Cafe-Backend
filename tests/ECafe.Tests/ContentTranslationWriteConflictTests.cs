using ECafe.Domain.Entities;
using ECafe.Domain.Exceptions;
using ECafe.Infrastructure.Context;
using ECafe.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace ECafe.Tests;

public sealed class ContentTranslationWriteConflictTests
{
    [Fact]
    public async Task UnrelatedDatabaseConflictIsNotReclassifiedAsTranslationError()
    {
        var options = new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var context = new Mock<ECafeDbContext>(options);
        var original = new DbUpdateConcurrencyException("Unrelated write conflict");
        context.Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(original);

        var error = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => new BaseRepository<Category>(context.Object).SaveChangesAsync());

        Assert.Same(original, error);
    }

    [Fact]
    public async Task SourceCrudReportsTranslationConflictWithoutOverwritingNewerText()
    {
        var options = new DbContextOptionsBuilder<ECafeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var first = new ECafeDbContext(options);
        first.CategoryTranslations.Add(new CategoryTranslation { CategoryId = 7, LanguageId = 2, Name = "First" });
        await first.SaveChangesAsync();
        await using var second = new ECafeDbContext(options);
        var newer = await second.CategoryTranslations.IgnoreQueryFilters().SingleAsync();
        newer.Name = "Newer";
        newer.Revision++;
        await second.SaveChangesAsync();
        first.CategoryTranslations.Local.Single().Name = "Stale";
        first.CategoryTranslations.Local.Single().Revision++;

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => new BaseRepository<Category>(first).SaveChangesAsync());

        Assert.Equal(ErrorCode.LocalizationDraftConflict, error.Code);
        Assert.Equal(409, error.StatusCode);
        Assert.Equal("Newer", (await second.CategoryTranslations.IgnoreQueryFilters().AsNoTracking().SingleAsync()).Name);
    }
}
