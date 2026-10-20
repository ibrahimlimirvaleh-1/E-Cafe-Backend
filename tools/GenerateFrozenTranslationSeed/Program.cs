using System.Security.Cryptography;
using System.Text;
using ECafe.Application.Services.Localization;
using ECafe.Domain.Policies;

if (args.Length != 1) throw new ArgumentException("Pass the absolute destination SQL path.");
var snapshot = new TranslationBootstrap().Snapshot;
var entries = snapshot.Entries.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
foreach (var entry in entries) TranslationPolicy.ValidateText(entry.Value);
var sql = new StringBuilder();
sql.AppendLine("-- Frozen relational catalog defaults. Do not regenerate an applied migration seed.");
sql.AppendLine($"-- Source: {snapshot.Version}; keywords: {entries.Length}; translations: {entries.Length * 3}.");
sql.AppendLine("WITH defaults(\"Key\", az, en, ru) AS (");
sql.AppendLine("    VALUES");
for (var index = 0; index < entries.Length; index++)
{
    var entry = entries[index];
    sql.Append("    (").Append(Literal(entry.Key)).Append(", ").Append(Literal(entry.Value.Az))
        .Append(", ").Append(Literal(entry.Value.En)).Append(", ").Append(Literal(entry.Value.Ru))
        .AppendLine(index == entries.Length - 1 ? ")" : "),");
}
sql.AppendLine("""
), inserted_keywords AS (
    INSERT INTO core.keywords ("Key")
    SELECT "Key" FROM defaults ORDER BY "Key" COLLATE "C"
    ON CONFLICT ("Key") DO NOTHING
    RETURNING "Id", "Key"
), all_keywords AS (
    SELECT "Id", "Key" FROM core.keywords
    UNION ALL
    SELECT "Id", "Key" FROM inserted_keywords
)
INSERT INTO core.keyword_translations ("KeywordId", "LanguageId", "Value")
SELECT k."Id", l."Id", t.value
FROM defaults d
JOIN all_keywords k ON k."Key" = d."Key"
CROSS JOIN LATERAL (VALUES ('az', d.az), ('en', d.en), ('ru', d.ru)) t(code, value)
JOIN core.languages l ON l."Code" = t.code
ORDER BY d."Key" COLLATE "C", l."Code" COLLATE "C"
ON CONFLICT ("KeywordId", "LanguageId") DO NOTHING;
""");
var content = sql.ToString().Replace("\r\n", "\n");
var path = Path.GetFullPath(args[0]);
if (File.Exists(path)) throw new InvalidOperationException("Refusing to overwrite a frozen migration seed; select a new destination.");
Directory.CreateDirectory(Path.GetDirectoryName(path)!);
File.WriteAllText(path, content, new UTF8Encoding(false));
Console.WriteLine($"Keywords: {entries.Length}; translations: {entries.Length * 3}; SHA256: {Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)))}");

// PostgreSQL escape literalında dırnaq, əks xətt və idarə simvollarını təhlükəsiz sabit mətnə çevirir.
static string Literal(string value)
    => "E'" + value.Replace("\\", "\\\\").Replace("'", "''").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "'";
