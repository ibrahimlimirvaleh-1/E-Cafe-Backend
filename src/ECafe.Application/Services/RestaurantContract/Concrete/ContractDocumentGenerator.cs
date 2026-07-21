using ECafe.Application.DTOs.RestaurantContract;
using ECafe.Application.Services.RestaurantContract.Abstract;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace ECafe.Application.Services.RestaurantContract.Concrete
{
    public class ContractDocumentGenerator : IContractDocumentGenerator
    {
        public const string ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        private static readonly string TemplatePath = Path.Combine("Templates", "Contracts", "restaurant-contract-document.xml");

        public GeneratedContractDocument Generate(RestaurantContractDocumentData data)
        {
            var documentXml = ApplyTemplate(ReadTemplate(), data);

            return new GeneratedContractDocument
            {
                FileName = $"{data.ContractNumber}.docx",
                ContentType = ContentType,
                Bytes = BuildDocx(documentXml)
            };
        }

        private static string ReadTemplate()
        {
            var path = Path.Combine(AppContext.BaseDirectory, TemplatePath);
            if (!System.IO.File.Exists(path))
                throw new InvalidOperationException($"Contract template was not found: {path}");

            return System.IO.File.ReadAllText(path);
        }

        private static string ApplyTemplate(string template, RestaurantContractDocumentData data)
        {
            var values = new Dictionary<string, string>
            {
                ["{{ContractNumber}}"] = data.ContractNumber,
                ["{{RestaurantName}}"] = data.RestaurantName,
                ["{{LegalName}}"] = data.LegalName,
                ["{{BranchName}}"] = data.BranchName,
                ["{{Location}}"] = data.Location,
                ["{{Phone}}"] = data.Phone,
                ["{{Email}}"] = data.Email,
                ["{{StartDate}}"] = FormatDate(data.StartDate),
                ["{{EndDate}}"] = FormatDate(data.EndDate),
                ["{{CommissionPercent}}"] = FormatPercent(data.CommissionPercent),
                ["{{StaffSettlementPeriod}}"] = FormatSettlementPeriod(data.StaffSettlementPeriod),
                ["{{PaymentPolicyId}}"] = data.PaymentPolicyId.ToString()
            };

            foreach (var value in values)
                template = template.Replace(value.Key, WebUtility.HtmlEncode(value.Value));

            return template;
        }

        private static byte[] BuildDocx(string documentXml)
        {
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                WriteZipEntry(archive, "[Content_Types].xml", """
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                      <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                      <Default Extension="xml" ContentType="application/xml"/>
                      <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                    </Types>
                    """);

                WriteZipEntry(archive, "_rels/.rels", """
                    <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                    <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                      <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
                    </Relationships>
                    """);

                WriteZipEntry(archive, "word/document.xml", documentXml);
            }

            return stream.ToArray();
        }

        private static void WriteZipEntry(ZipArchive archive, string entryName, string content)
        {
            var entry = archive.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(content);
        }

        private static string FormatDate(DateTime? value)
            => value.HasValue ? value.Value.ToString("yyyy-MM-dd") : "-";

        private static string FormatPercent(decimal? value)
            => value.HasValue ? $"{value:0.##}%" : "-";

        private static string FormatSettlementPeriod(int? value)
            => value.HasValue ? $"{value} g\u00FCn" : "-";
    }
}
