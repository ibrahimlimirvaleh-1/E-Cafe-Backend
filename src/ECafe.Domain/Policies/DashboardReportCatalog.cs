using System.Diagnostics.CodeAnalysis;
using ECafe.Domain.Enums;

namespace ECafe.Domain.Policies;

public static class DashboardReportCatalog
{
    public static IReadOnlyList<DashboardReportDefinition> All { get; } = Array.AsReadOnly<DashboardReportDefinition>(
    [
        new(DashboardReportKind.Orders, "orders", false, true),
        new(DashboardReportKind.Reservations, "reservations", false, true),
        new(DashboardReportKind.Payments, "payments", true, false),
        new(DashboardReportKind.Products, "products", false, true),
        new(DashboardReportKind.Kitchen, "kitchen", false, true),
        new(DashboardReportKind.Cancellations, "cancellations", false, true),
        new(DashboardReportKind.ReservationOutcomes, "reservation-outcomes", false, true),
        new(DashboardReportKind.Tables, "tables", false, true),
        new(DashboardReportKind.Settlements, "settlements", true, false),
        new(DashboardReportKind.Branches, "branches", true, false),
        new(DashboardReportKind.Stock, "stock", false, true)
    ]);

    private static readonly IReadOnlyDictionary<string, DashboardReportDefinition> ByCode = All.ToDictionary(row => row.Code, StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<DashboardReportKind, DashboardReportDefinition> ByKind = All.ToDictionary(row => row.Kind);

    /// <summary>Xarici hesabat kodunu eyni rejim və rol tərifinə bağlayır; tanınmayan və null kodu səssiz standart növə çevirmir.</summary>
    public static bool TryGet(string? code, [NotNullWhen(true)] out DashboardReportDefinition? definition)
    {
        definition = null;
        return code != null && ByCode.TryGetValue(code, out definition);
    }

    /// <summary>Daxili enum üçün qeydiyyatlı tərifi qaytarır; yeni enum tərifsiz istifadə edilərsə açıq xəta yaradır.</summary>
    public static DashboardReportDefinition Get(DashboardReportKind kind)
        => ByKind.TryGetValue(kind, out var definition) ? definition : throw new ArgumentOutOfRangeException(nameof(kind));
}
