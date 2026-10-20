using System.Text;

namespace ECafe.Infrastructure.Migrations.Seeds;

public static class RelationalTranslationCatalogSeed
{
    /// <summary>Migration üçün dondurulmuş SQL resursunu oxuyur; sonrakı build-lərin bootstrap lüğətindən istifadə etmir.</summary>
    public static string ReadSql()
    {
        using var stream = typeof(RelationalTranslationCatalogSeed).Assembly.GetManifestResourceStream(
            "ECafe.Infrastructure.Migrations.Seeds.RelationalTranslationCatalog.sql")
            ?? throw new InvalidOperationException("The frozen relational translation seed is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
