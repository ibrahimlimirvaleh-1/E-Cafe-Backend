using ECafe.Domain.Entities;
using ECafe.Domain.Enums;
using ECafe.Domain.Exceptions;
using ECafe.Domain.Policies;
using Xunit;

namespace ECafe.Tests;

public sealed class ContentTranslationPolicyTests
{
    [Fact]
    public void CollectionPolicyExtendsToNewTranslationTypeWithoutEntityTypeSwitch()
    {
        var existing = new ExtraTranslation { LanguageId = 2, Caption = "Before", Revision = 7 };
        var rows = new List<ExtraTranslation> { existing };
        ContentTranslationCollectionPolicy.Apply(rows,
            [new ExtraTranslation { LanguageId = 2, Caption = "After" }],
            new ExtraTranslation { Caption = "AZ source" }, Copy);
        Assert.Same(existing, rows.Single(row => row.LanguageId == 2));
        Assert.Equal("After", existing.Caption);
        Assert.Equal(8, existing.Revision);
        Assert.Equal("AZ source", rows.Single(row => row.LanguageId == 1).Caption);
        ContentTranslationCollectionPolicy.Apply(rows, null, new ExtraTranslation { Caption = "AZ updated" }, Copy);
        Assert.Equal(2, rows.Count);
        ContentTranslationCollectionPolicy.Apply(rows, [], new ExtraTranslation { Caption = "AZ updated" }, Copy);
        Assert.Equal(2, rows.Count);
        Assert.Null(existing.Caption);
        Assert.Equal(9, existing.Revision);
        ContentTranslationCollectionPolicy.Apply(rows,
            [new ExtraTranslation { LanguageId = 2, Caption = "Recreated" }], new ExtraTranslation(), Copy);
        Assert.Equal(10, existing.Revision);
    }

    [Fact]
    public void InvalidReplacementDoesNotMutateTrackedCollection()
    {
        var existing = new ExtraTranslation { LanguageId = 2, Caption = "Keep", Revision = 3 };
        var rows = new List<ExtraTranslation> { existing };
        Assert.Throws<BusinessRuleException>(() => ContentTranslationCollectionPolicy.Apply(rows,
            [new ExtraTranslation { LanguageId = 2 }, new ExtraTranslation { LanguageId = 2 }], new ExtraTranslation(), Copy));
        Assert.Same(existing, Assert.Single(rows));
        Assert.Equal("Keep", existing.Caption);
        Assert.Equal(3, existing.Revision);
    }

    [Fact]
    public void ExhaustedRevisionRejectsWholeReplacementBeforeCopyOrRemoval()
    {
        var en = new ExtraTranslation { LanguageId = 2, Caption = "Keep EN" };
        var ru = new ExtraTranslation { LanguageId = 3, Caption = "Keep RU", Revision = long.MaxValue };
        var rows = new List<ExtraTranslation> { en, ru };
        var error = Assert.Throws<BusinessRuleException>(() => ContentTranslationCollectionPolicy.Apply(rows,
            [new ExtraTranslation { LanguageId = 2, Caption = "Changed" }, new ExtraTranslation { LanguageId = 3 }], new ExtraTranslation(), Copy));
        Assert.Equal(ErrorCode.LocalizationDraftConflict, error.Code);
        Assert.Equal("Keep EN", en.Caption);
        Assert.Equal("Keep RU", ru.Caption);
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void ClearWithExhaustedRevisionIsAtomic()
    {
        var row = new ExtraTranslation { LanguageId = 2, Caption = "Keep", Revision = long.MaxValue };
        var rows = new List<ExtraTranslation> { row };
        var error = Assert.Throws<BusinessRuleException>(() => ContentTranslationCollectionPolicy.Apply(rows,
            [], new ExtraTranslation(), Copy));
        Assert.Equal(ErrorCode.LocalizationDraftConflict, error.Code);
        Assert.Equal("Keep", row.Caption);
        Assert.Single(rows);
    }

    [Theory]
    [InlineData(ContentEntityType.Category, 100)]
    [InlineData(ContentEntityType.Item, 150)]
    [InlineData(ContentEntityType.Table, 50)]
    [InlineData(ContentEntityType.InventoryItem, 150)]
    [InlineData(ContentEntityType.Unit, 50)]
    public void NameLimitsAreSharedByCrudAndEditor(ContentEntityType type, int limit)
    {
        Assert.Equal(limit, ContentTranslationPolicy.For(type).NameLength);
        Assert.Equal(limit, ContentTranslationPolicy.Normalize(type, new(new string('a', limit))).Name!.Length);
        Assert.Throws<BusinessRuleException>(() => ContentTranslationPolicy.Normalize(type, new(new string('a', limit + 1))));
    }

    [Theory]
    [InlineData(ContentEntityType.Category, 3, "categories")]
    [InlineData(ContentEntityType.Item, 4, "items")]
    [InlineData(ContentEntityType.Table, 5, "tables")]
    [InlineData(ContentEntityType.InventoryItem, 6, "inventory-items")]
    [InlineData(ContentEntityType.Unit, 7, "units")]
    public void SupportedKindsRetainOriginalNumericIdsAndEditorRoutes(ContentEntityType type, int id, string route)
    {
        Assert.Equal(id, (int)type);
        Assert.Equal(type, ECafe.Application.Mappings.ContentTranslationEditorMapper.ParseType(route));
        Assert.Equal(5, Enum.GetValues<ContentEntityType>().Length);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void RemovedKindsHaveNoEnumMemberOrTranslationRules(int id)
    {
        var type = (ContentEntityType)id;
        Assert.False(Enum.IsDefined(type));
        var error = Assert.Throws<BusinessRuleException>(() => ContentTranslationPolicy.For(type));
        Assert.Equal(ErrorCode.LocalizationInvalidEntry, error.Code);
        error = Assert.Throws<BusinessRuleException>(() => ContentTranslationPolicy.Normalize(type, new(Name: "Brand")));
        Assert.Equal(ErrorCode.LocalizationInvalidEntry, error.Code);
    }

    [Fact]
    public void EditorAllowsSparseFieldsButCrudCanRequireName()
    {
        var fields = new ContentTranslationFields(Description: " Hot tea ");
        Assert.Equal("Hot tea", ContentTranslationPolicy.Normalize(ContentEntityType.Item, fields).Description);
        Assert.Throws<BusinessRuleException>(() => ContentTranslationPolicy.Normalize(ContentEntityType.Item, fields, requireName: true));
        Assert.Throws<BusinessRuleException>(() => ContentTranslationPolicy.Normalize(ContentEntityType.Category, fields));
        Assert.Throws<BusinessRuleException>(() => ContentTranslationPolicy.Normalize(ContentEntityType.Item, new(Name: "<script>x</script>")));
    }

    [Fact]
    public void PolicyOwnsRevisionBoundariesAndConflictCode()
    {
        Assert.Throws<BusinessRuleException>(() => ContentTranslationPolicy.RequireRevision(null));
        Assert.Throws<BusinessRuleException>(() => ContentTranslationPolicy.RequireRevision(long.MaxValue));
        var error = Assert.Throws<BusinessRuleException>(() => ContentTranslationPolicy.RequireCurrentRevision(4, 3));
        Assert.Equal(ErrorCode.LocalizationDraftConflict, error.Code);
        ContentTranslationPolicy.RequireRevision(0);
        ContentTranslationPolicy.RequireCurrentRevision(4, 4);
    }

    /// <summary>Domen kolleksiya qaydasına yalnız yeni tipin sahə köçürməsini verir; əsas alqoritmi dəyişmir.</summary>
    private static void Copy(ExtraTranslation source, ExtraTranslation target) => target.Caption = source.Caption;

    private sealed class ExtraTranslation : EntityTranslation
    {
        public string? Caption { get; set; }
    }
}
