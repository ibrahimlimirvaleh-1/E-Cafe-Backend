using System.Globalization;
using System.Text;
using ECafe.Application.DTOs.Dashboard;

namespace ECafe.Application.Mappings;

public static class DashboardReportCsvMapper
{
    /// <summary>Tiplənmiş hesabatdan UTF-8 BOM-lu CSV yaradır; daxili qeydlər və provayder rekvizitləri ixraca daxil edilmir.</summary>
    public static DashboardReportFile Map(DashboardReportResponse report)
    {
        var csv = new StringBuilder();
        var context = new object?[] { report.Kind, report.TimeZone, report.From, report.To,
            report.Kind == "payments" ? report.PaymentMode : "not_applicable" };
        var headings = new[] { "report_kind", "time_zone", "period_from", "period_to", "payment_mode" };
        switch (report.Kind)
        {
            case "orders":
                Append(csv, headings.Concat(["order_id", "restaurant_id", "restaurant", "table", "session_id", "source_id", "status_id", "opened_at_utc", "sent_to_kitchen_at_utc", "served_at_utc", "item_quantity", "item_subtotal_azn", "confirmed_amount_azn"]));
                foreach (var row in report.Orders)
                    Append(csv, context.Concat([row.Id, row.RestaurantId, row.RestaurantName, row.TableName, row.TableSessionId,
                        row.SourceId, row.StatusId, row.OpenedAtUtc, row.SentToKitchenAtUtc, row.ServedAtUtc,
                        row.ItemQuantity, row.ItemSubtotal, row.ConfirmedAmount]));
                break;
            case "reservations":
                Append(csv, headings.Concat(["reservation_id", "restaurant_id", "restaurant", "table", "status_id", "reserved_at_utc", "people_count", "requested_deposit_azn", "arrived_at_utc", "seated_at_utc"]));
                foreach (var row in report.Reservations)
                    Append(csv, context.Concat([row.Id, row.RestaurantId, row.RestaurantName, row.TableName, row.StatusId,
                        row.ReservedAtUtc, row.PeopleCount, row.DepositAmount, row.ArrivedAtUtc, row.SeatedAtUtc]));
                break;
            case "payments":
                Append(csv, headings.Concat(["event_id", "event", "restaurant_id", "restaurant", "occurred_at_utc", "amount_azn", "purpose", "reservation_id", "session_id", "status_id", "is_test"]));
                foreach (var row in report.Payments)
                    Append(csv, context.Concat([row.Id, row.Event, row.RestaurantId, row.RestaurantName, row.OccurredAtUtc,
                        row.Amount, row.Purpose, row.ReservationId, row.TableSessionId, row.StatusId, row.IsTest]));
                break;
            default:
                throw new ArgumentException("Unsupported report kind.", nameof(report));
        }
        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        var mode = report.Kind == "payments" ? "-" + report.PaymentMode : "";
        return new(content, $"ecafe-{report.Kind}-{report.From:yyyy-MM-dd}-{report.To:yyyy-MM-dd}{mode}.csv");
    }

    /// <summary>CSV sətrində hər xananı ayrıca qaçırır; vergül, dırnaq və yeni sətrin struktur pozmasına yol vermir.</summary>
    private static void Append(StringBuilder csv, IEnumerable<object?> values)
        => csv.AppendJoin(',', values.Select(Cell)).Append("\r\n");

    /// <summary>Etibarsız mətnin elektron cədvəldə formul kimi işləməsini bloklayır; rəqəm və UTC tarixlərini sabit formatda saxlayır.</summary>
    private static string Cell(object? value)
    {
        var text = value switch
        {
            null => "",
            DateTime time => DateTime.SpecifyKind(time, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            bool flag => flag ? "true" : "false",
            IFormattable formatted => formatted.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? ""
        };
        if (value is string)
        {
            var index = 0;
            while (index < text.Length && (char.IsWhiteSpace(text[index]) || char.IsControl(text[index]) ||
                char.GetUnicodeCategory(text[index]) == UnicodeCategory.Format)) index++;
            if (index < text.Length && text[index] is '=' or '+' or '-' or '@') text = "'" + text;
        }
        return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
