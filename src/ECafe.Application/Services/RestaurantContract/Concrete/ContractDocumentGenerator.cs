using ECafe.Application.DTOs.RestaurantContract;
using ECafe.Application.Services.RestaurantContract.Abstract;
using System.Globalization;
using System.Text;

namespace ECafe.Application.Services.RestaurantContract.Concrete
{
    public class ContractDocumentGenerator : IContractDocumentGenerator
    {
        public const string ContentType = "application/pdf";

        public GeneratedContractDocument Generate(RestaurantContractDocumentData data)
        {
            return new GeneratedContractDocument
            {
                FileName = $"{data.ContractNumber}.pdf",
                ContentType = ContentType,
                Bytes = BuildPdf(data)
            };
        }

        private static byte[] BuildPdf(RestaurantContractDocumentData data)
        {
            var lines = BuildContractLines(data);
            var content = BuildPageContent(lines);
            var objects = new List<string>
            {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> /Contents 6 0 R >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>",
                $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream"
            };

            return WritePdf(objects);
        }

        private static IReadOnlyList<PdfLine> BuildContractLines(RestaurantContractDocumentData data)
            => new List<PdfLine>
            {
                PdfLine.Title("E-Cafe Restoran Muqavilesi"),
                PdfLine.Subtitle(data.ContractNumber),
                PdfLine.Empty(),
                PdfLine.Section("Terefler"),
                PdfLine.Body($"Restoran: {data.RestaurantName}"),
                PdfLine.Body($"Huquqi ad: {data.LegalName}"),
                PdfLine.Body($"Filial: {data.BranchName}"),
                PdfLine.Body($"Unvan: {data.Location}"),
                PdfLine.Body($"Telefon: {data.Phone}"),
                PdfLine.Body($"Email: {data.Email}"),
                PdfLine.Empty(),
                PdfLine.Section("Muqavile sertleri"),
                PdfLine.Body($"Baslama tarixi: {FormatDate(data.StartDate)}"),
                PdfLine.Body($"Bitme tarixi: {FormatDate(data.EndDate)}"),
                PdfLine.Body($"Komissiya: {FormatPercent(data.CommissionPercent)}"),
                PdfLine.Body($"Personal hesablasma dovru: {FormatSettlementPeriod(data.StaffSettlementPeriod)}"),
                PdfLine.Body($"Odenis siyaseti ID: {data.PaymentPolicyId}"),
                PdfLine.Empty(),
                PdfLine.Section("Qeyd"),
                PdfLine.Body("Bu sened sistem terefinden avtomatik yaradilib."),
                PdfLine.Body("Restoran sahibi senedi oxuyub tesdiq etdikden sonra platforma admini muqavileni aktivlesdirir.")
            };

        private static string BuildPageContent(IReadOnlyList<PdfLine> lines)
        {
            var builder = new StringBuilder();
            var y = 790;

            foreach (var line in lines)
            {
                if (line.IsEmpty)
                {
                    y -= 16;
                    continue;
                }

                var font = line.Kind == PdfLineKind.Text ? "F1" : "F2";
                var fontSize = line.Kind switch
                {
                    PdfLineKind.Title => 20,
                    PdfLineKind.Subtitle => 14,
                    PdfLineKind.Section => 12,
                    _ => 10
                };

                builder.Append("BT ")
                    .Append('/').Append(font).Append(' ').Append(fontSize).Append(" Tf ")
                    .Append("50 ").Append(y.ToString(CultureInfo.InvariantCulture)).Append(" Td ")
                    .Append('(').Append(EscapePdfText(NormalizePdfText(line.Value))).Append(") Tj ET\n");

                y -= line.Kind == PdfLineKind.Title ? 28 : 20;
            }

            return builder.ToString();
        }

        private static byte[] WritePdf(IReadOnlyList<string> objects)
        {
            using var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true);
            var offsets = new List<long> { 0 };

            writer.Write("%PDF-1.4\n");
            writer.Write("%\u00E2\u00E3\u00CF\u00D3\n");
            writer.Flush();

            for (var index = 0; index < objects.Count; index++)
            {
                offsets.Add(stream.Position);
                writer.Write(index + 1);
                writer.Write(" 0 obj\n");
                writer.Write(objects[index]);
                writer.Write("\nendobj\n");
                writer.Flush();
            }

            var xrefOffset = stream.Position;
            writer.Write("xref\n");
            writer.Write("0 ");
            writer.Write(objects.Count + 1);
            writer.Write("\n");
            writer.Write("0000000000 65535 f \n");

            for (var index = 1; index < offsets.Count; index++)
            {
                writer.Write(offsets[index].ToString("0000000000", CultureInfo.InvariantCulture));
                writer.Write(" 00000 n \n");
            }

            writer.Write("trailer\n");
            writer.Write($"<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
            writer.Write("startxref\n");
            writer.Write(xrefOffset.ToString(CultureInfo.InvariantCulture));
            writer.Write("\n%%EOF");
            writer.Flush();

            return stream.ToArray();
        }

        private static string NormalizePdfText(string value)
            => value
                .Replace('ə', 'e').Replace('Ə', 'E')
                .Replace('ı', 'i').Replace('I', 'I')
                .Replace('İ', 'I').Replace('ö', 'o')
                .Replace('Ö', 'O').Replace('ü', 'u')
                .Replace('Ü', 'U').Replace('ğ', 'g')
                .Replace('Ğ', 'G').Replace('ş', 's')
                .Replace('Ş', 'S').Replace('ç', 'c')
                .Replace('Ç', 'C');

        private static string EscapePdfText(string value)
            => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

        private static string FormatDate(DateTime? value)
            => value.HasValue ? value.Value.ToString("yyyy-MM-dd") : "-";

        private static string FormatPercent(decimal? value)
            => value.HasValue ? $"{value:0.##}%" : "-";

        private static string FormatSettlementPeriod(int? value)
            => value.HasValue ? $"{value} gun" : "-";

        private sealed record PdfLine(PdfLineKind Kind, string Value)
        {
            public bool IsEmpty => Kind == PdfLineKind.Empty;

            public static PdfLine Title(string text) => new(PdfLineKind.Title, text);

            public static PdfLine Subtitle(string text) => new(PdfLineKind.Subtitle, text);

            public static PdfLine Section(string text) => new(PdfLineKind.Section, text);

            public static PdfLine Body(string text) => new(PdfLineKind.Text, text);

            public static PdfLine Empty() => new(PdfLineKind.Empty, string.Empty);
        }

        private enum PdfLineKind
        {
            Empty,
            Title,
            Subtitle,
            Section,
            Text
        }
    }
}
