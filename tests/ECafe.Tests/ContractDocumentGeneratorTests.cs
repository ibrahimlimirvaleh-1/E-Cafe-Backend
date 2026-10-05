using System.Text;
using ECafe.Application.DTOs.RestaurantContract;
using ECafe.Application.Services.RestaurantContract.Concrete;
using PdfSharp.Pdf.IO;
using Xunit;

namespace ECafe.Tests;

public sealed class ContractDocumentGeneratorTests
{
    [Fact]
    public void GeneratesBrandedPdfWithAzerbaijaniTextAndLongFields()
    {
        var data = new RestaurantContractDocumentData
        {
            ContractNumber = "EC-2026-00025-00002",
            RestaurantName = "Dolce Vita Port Bakı",
            LegalName = "Bakı Restoran Xidmətləri və İctimai İaşə Məhdud Məsuliyyətli Cəmiyyəti",
            BranchName = "Dolce Vita Port Bakı - Neftçilər prospekti filialı",
            Location = "Port Baku, Neftçilər prospekti 153, Nəsimi rayonu, Bakı, Azərbaycan",
            Phone = "+994 50 123 45 67",
            Email = "info@example.az",
            StartDate = new DateTime(2026, 10, 5),
            EndDate = new DateTime(2027, 10, 5),
            Amount = 12000.50m,
            CommissionPercent = 12.5m,
            StaffSettlementPeriod = 14,
            PaymentPolicyId = 1
        };

        var generated = new ContractDocumentGenerator().Generate(data);

        Assert.Equal("EC-2026-00025-00002.pdf", generated.FileName);
        Assert.Equal("application/pdf", generated.ContentType);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(generated.Bytes, 0, 5));
        Assert.True(generated.Bytes.Length > 10_000);
        using var pdf = PdfReader.Open(new MemoryStream(generated.Bytes), PdfDocumentOpenMode.Import);
        Assert.InRange(pdf.PageCount, 1, 2);

        var previewPath = Environment.GetEnvironmentVariable("ECAFE_CONTRACT_PREVIEW_PATH");
        if (!string.IsNullOrWhiteSpace(previewPath))
            System.IO.File.WriteAllBytes(previewPath, generated.Bytes);
    }

    [Fact]
    public void OptionalFieldsDoNotPreventPdfGeneration()
    {
        var generated = new ContractDocumentGenerator().Generate(new RestaurantContractDocumentData
        {
            ContractNumber = "EC-2026-1",
            RestaurantName = "Test",
            LegalName = "Test",
            BranchName = "Test",
            Location = "Bakı",
            Phone = "+994 12 000 00 00",
            StartDate = new DateTime(2026, 10, 5),
            Amount = 0,
            PaymentPolicyId = 1
        });

        using var pdf = PdfReader.Open(new MemoryStream(generated.Bytes), PdfDocumentOpenMode.Import);
        Assert.Equal(1, pdf.PageCount);
    }
}
