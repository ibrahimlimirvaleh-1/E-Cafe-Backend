using System.Globalization;
using ECafe.Application.DTOs.Dashboard;
using ECafe.Application.Repositories.Dashboard;

namespace ECafe.Application.Mappings;

public static class DashboardReportDetailMapper
{
    /// <summary>Sabit sütun sxemindən tipli cədvəl yaradır; miqdar vahidlərini bir-birinə qarışdırmadan ekran və CSV üçün eyni kontraktı saxlayır.</summary>
    public static DashboardReportData Map(int count, string schema, IEnumerable<DashboardDetailRow> rows, params DashboardMetric[] metrics)
        => new(new() { RowCount = count }, [], [], [], new(schema.Split(' ').Select(column =>
        {
            var pair = column.Split(':');
            return new DashboardReportColumn(pair[0], pair.Length == 2 ? pair[1] : "text");
        }).ToArray(), rows.ToArray()), metrics);

    /// <summary>SQL proyeksiyasının mətn, rəqəm və UTC vaxtını ayrıca sahələrə çevirir; HTML və yerli formatlı rəqəm yaratmır.</summary>
    public static DashboardDetailRow Row(string id, params object?[] values)
        => new(id, values.Select(value => value switch
        {
            null => new DashboardReportCell(),
            DateTime time => new DashboardReportCell(Utc: DateTime.SpecifyKind(time, DateTimeKind.Utc)),
            decimal number => new DashboardReportCell(Number: number),
            int number => new DashboardReportCell(Number: number),
            long number => new DashboardReportCell(Number: number),
            double number when double.IsFinite(number) => new DashboardReportCell(Number: decimal.Round((decimal)number, 2)),
            bool flag => new DashboardReportCell(Text: flag ? "yes" : "no"),
            _ => new DashboardReportCell(Text: Convert.ToString(value, CultureInfo.InvariantCulture))
        }).ToArray());

    /// <summary>Əvvəlki dövr məlumatı olmayan ölçünü saxta faiz müqayisəsi olmadan təqdim edir.</summary>
    public static DashboardMetric Metric(string key, decimal value, string unit = "count") => new(key, value, unit, null, null);
}
