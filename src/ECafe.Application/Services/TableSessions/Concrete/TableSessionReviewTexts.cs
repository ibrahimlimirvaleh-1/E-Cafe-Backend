namespace ECafe.Application.Services.TableSessions.Concrete;

public static class TableSessionReviewTexts
{
    public const string RequiredTitle = "Masa sessiyasının bağlanması yoxlanmalıdır";
    public const string Continued = "Masa sessiyasında xidmətin davam etdiyi təsdiqləndi.";
    public const string Section = "session-close-review";
    /// <summary>Əməkdaşa yalnız masa və sessiya identifikatorları ilə yoxlama bildirişi hazırlayır.</summary>
    public static string RequiredMessage(int sessionId, string? tableName)
        => $"{tableName ?? "Masa"} üzrə #{sessionId} sessiyası işgünü bitdikdən sonra açıq qalıb. Müştərinin ayrıldığını və xidmətin bitdiyini yoxlayın.";
}
