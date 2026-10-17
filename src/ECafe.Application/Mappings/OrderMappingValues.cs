namespace ECafe.Application.Mappings;

internal static class OrderMappingValues
{
    /// <summary>Ad verilməmiş masanı nömrəsi ilə göstərir; cavab və bildirişlərdə eyni masa adını saxlayır.</summary>
    internal static string TableName(Domain.Entities.Table table)
        => string.IsNullOrWhiteSpace(table.Name) ? $"Masa-{table.TableNo}" : table.Name;

    /// <summary>Bazada UTC kimi saxlanmış vaxtı API-də sıfır offset ilə qaytarır.</summary>
    internal static DateTimeOffset AsUtc(DateTime value)
        => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    /// <summary>Hələ baş verməmiş mərhələni null saxlayır; mövcud vaxtı UTC offset ilə göstərir.</summary>
    internal static DateTimeOffset? AsNullableUtc(DateTime? value)
        => value.HasValue ? AsUtc(value.Value) : null;
}
