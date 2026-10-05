using System.Globalization;
using ECafe.Application.DTOs.RestaurantContract;
using ECafe.Application.Services.RestaurantContract.Abstract;
using ECafe.Domain.Enums;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using PdfSharp.Fonts;
using PdfTable = MigraDoc.DocumentObjectModel.Tables.Table;

namespace ECafe.Application.Services.RestaurantContract.Concrete;

public sealed class ContractDocumentGenerator : IContractDocumentGenerator
{
    public const string ContentType = "application/pdf";

    private static readonly CultureInfo Azerbaijani = CultureInfo.GetCultureInfo("az-AZ");
    private static readonly object FontRegistrationLock = new();
    private static readonly byte[] Logo = LoadResource("ecafe-icon.png");
    private static readonly Color Ink = Color.FromRgb(24, 31, 34);
    private static readonly Color Muted = Color.FromRgb(99, 111, 118);
    private static readonly Color Accent = Color.FromRgb(0, 113, 91);
    private static readonly Color Border = Color.FromRgb(219, 226, 228);
    private static readonly Color Surface = Color.FromRgb(246, 249, 248);

    public GeneratedContractDocument Generate(RestaurantContractDocumentData data)
    {
        EnsureFontsRegistered();
        var document = BuildDocument(data);
        var renderer = new PdfDocumentRenderer { Document = document };
        renderer.RenderDocument();

        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return new GeneratedContractDocument
        {
            FileName = $"{data.ContractNumber}.pdf",
            ContentType = ContentType,
            Bytes = stream.ToArray()
        };
    }

    private static Document BuildDocument(RestaurantContractDocumentData data)
    {
        var document = new Document();
        document.Info.Title = $"E-Cafe restoran müqaviləsi - {data.ContractNumber}";
        document.Info.Author = "E-Cafe";

        var normal = document.Styles[StyleNames.Normal]!;
        normal.Font.Name = EmbeddedNotoFontResolver.FamilyName;
        normal.Font.Size = 9;
        normal.Font.Color = Ink;
        normal.ParagraphFormat.SpaceAfter = Unit.FromPoint(0);

        var section = document.AddSection();
        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.8);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.8);
        section.PageSetup.TopMargin = Unit.FromCentimeter(2.7);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(2.1);
        section.PageSetup.HeaderDistance = Unit.FromCentimeter(0.9);
        section.PageSetup.FooterDistance = Unit.FromCentimeter(0.9);

        AddHeader(section);
        AddFooter(section, data.ContractNumber);
        AddTitle(section, data);

        AddSectionHeading(section, "01", "Tərəflər və restoran məlumatları");
        var parties = CreateDetailsTable(section);
        AddDetail(parties, "Platforma", "E-Cafe");
        AddDetail(parties, "Restoran", data.RestaurantName);
        AddDetail(parties, "Hüquqi ad", data.LegalName);
        AddDetail(parties, "Filial", data.BranchName);
        AddDetail(parties, "Ünvan", data.Location);
        AddDetail(parties, "Telefon", data.Phone);
        AddDetail(parties, "E-poçt", Display(data.Email));

        AddSectionHeading(section, "02", "Müqavilə şərtləri");
        var terms = CreateDetailsTable(section);
        AddDetail(terms, "Başlama tarixi", FormatDate(data.StartDate));
        AddDetail(terms, "Bitmə tarixi", FormatDate(data.EndDate));
        AddDetail(terms, "Müqavilə məbləği", FormatAmount(data.Amount));
        AddDetail(terms, "Komissiya", data.CommissionPercent.HasValue
            ? $"{data.CommissionPercent.Value.ToString("0.##", Azerbaijani)}%" : "—");
        AddDetail(terms, "Personal hesablaşması", data.StaffSettlementPeriod.HasValue
            ? $"Hər {data.StaffSettlementPeriod.Value} gündən bir" : "—");
        AddDetail(terms, "Ödəniş siyasəti", data.PaymentPolicyId == (int)ContractPaymentPolicy.OnlineOnly
            ? "Yalnız onlayn ödəniş" : $"Kod: {data.PaymentPolicyId}");

        AddSectionHeading(section, "03", "Təsdiq");
        var note = section.AddParagraph();
        note.Format.SpaceAfter = Unit.FromPoint(15);
        note.Format.Font.Color = Muted;
        note.AddText("Bu sənəd sistem tərəfindən avtomatik yaradılıb. Restoran sahibi sənədi oxuyub " +
            "təsdiq etdikdən sonra platforma admini müqaviləni aktivləşdirir.");

        AddSignatures(section);
        return document;
    }

    private static void AddHeader(Section section)
    {
        var header = section.Headers.Primary;
        var table = header.AddTable();
        table.AddColumn(Unit.FromCentimeter(1.25));
        table.AddColumn(Unit.FromCentimeter(15.55));
        var row = table.AddRow();
        row.VerticalAlignment = VerticalAlignment.Center;
        var image = row.Cells[0].AddImage("base64:" + Convert.ToBase64String(Logo));
        image.Width = Unit.FromCentimeter(1.15);
        image.LockAspectRatio = true;

        var brand = row.Cells[1].AddParagraph("E-Cafe");
        brand.Format.Font.Size = 15;
        brand.Format.Font.Bold = true;
        brand.Format.Font.Color = Ink;

        var rule = header.AddParagraph();
        rule.Format.SpaceBefore = Unit.FromPoint(5);
        rule.Format.Borders.Bottom.Width = Unit.FromPoint(1);
        rule.Format.Borders.Bottom.Color = Accent;
    }

    private static void AddFooter(Section section, string contractNumber)
    {
        var footer = section.Footers.Primary.AddParagraph();
        footer.Format.Borders.Top.Width = Unit.FromPoint(0.5);
        footer.Format.Borders.Top.Color = Border;
        footer.Format.SpaceBefore = Unit.FromPoint(6);
        footer.Format.Font.Size = 8;
        footer.Format.Font.Color = Muted;
        footer.Format.AddTabStop(Unit.FromCentimeter(16.8), TabAlignment.Right);
        footer.AddText($"E-Cafe  ·  {contractNumber}");
        footer.AddTab();
        footer.AddText("Səhifə ");
        footer.AddPageField();
    }

    private static void AddTitle(Section section, RestaurantContractDocumentData data)
    {
        var eyebrow = section.AddParagraph("RESTORAN XİDMƏTLƏRİ");
        eyebrow.Format.Font.Size = 8;
        eyebrow.Format.Font.Bold = true;
        eyebrow.Format.Font.Color = Accent;
        eyebrow.Format.SpaceAfter = Unit.FromPoint(7);

        var title = section.AddParagraph("Müqavilə");
        title.Format.Font.Size = 23;
        title.Format.Font.Bold = true;
        title.Format.Font.Color = Ink;
        title.Format.SpaceAfter = Unit.FromPoint(8);

        var number = section.AddParagraph();
        number.Format.SpaceAfter = Unit.FromPoint(21);
        number.AddFormattedText("Müqavilə №  ").Font.Color = Muted;
        number.AddFormattedText(data.ContractNumber).Bold = true;
    }

    private static void AddSectionHeading(Section section, string number, string title)
    {
        var heading = section.AddParagraph();
        heading.Format.SpaceBefore = Unit.FromPoint(17);
        heading.Format.SpaceAfter = Unit.FromPoint(9);
        heading.Format.KeepWithNext = true;
        heading.AddFormattedText(number + "   ").Font.Color = Accent;
        var text = heading.AddFormattedText(title);
        text.Bold = true;
        text.Font.Size = 11;
    }

    private static PdfTable CreateDetailsTable(Section section)
    {
        var table = section.AddTable();
        table.AddColumn(Unit.FromCentimeter(5.1));
        table.AddColumn(Unit.FromCentimeter(11.7));
        table.Borders.Bottom.Width = Unit.FromPoint(0.4);
        table.Borders.Bottom.Color = Border;
        return table;
    }

    private static void AddDetail(PdfTable table, string label, string value)
    {
        var row = table.AddRow();
        row.TopPadding = Unit.FromPoint(6);
        row.BottomPadding = Unit.FromPoint(6);
        row.Borders.Bottom.Width = Unit.FromPoint(0.4);
        row.Borders.Bottom.Color = Border;
        row.Cells[0].Shading.Color = Surface;
        var labelParagraph = row.Cells[0].AddParagraph(label);
        labelParagraph.Format.LeftIndent = Unit.FromPoint(8);
        labelParagraph.Format.Font.Size = 8.5;
        labelParagraph.Format.Font.Color = Muted;
        var valueParagraph = row.Cells[1].AddParagraph(Display(value));
        valueParagraph.Format.LeftIndent = Unit.FromPoint(10);
        valueParagraph.Format.Font.Size = 9;
        valueParagraph.Format.Font.Bold = true;
    }

    private static void AddSignatures(Section section)
    {
        var table = section.AddTable();
        table.AddColumn(Unit.FromCentimeter(8.1));
        table.AddColumn(Unit.FromCentimeter(0.6));
        table.AddColumn(Unit.FromCentimeter(8.1));
        var row = table.AddRow();
        foreach (var (index, title) in new[] { (0, "Platforma nümayəndəsi"), (2, "Restoran nümayəndəsi") })
        {
            var cell = row.Cells[index];
            var label = cell.AddParagraph(title);
            label.Format.Font.Size = 8;
            label.Format.Font.Color = Muted;
            label.Format.SpaceAfter = Unit.FromPoint(25);
            var line = cell.AddParagraph("Ad, soyad / imza");
            line.Format.Borders.Top.Width = Unit.FromPoint(0.5);
            line.Format.Borders.Top.Color = Border;
            line.Format.SpaceBefore = Unit.FromPoint(3);
            line.Format.Font.Size = 7.5;
            line.Format.Font.Color = Muted;
        }
    }

    private static string Display(string? value)
        => string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

    private static string FormatDate(DateTime? value)
        => value.HasValue ? value.Value.ToString("dd.MM.yyyy", Azerbaijani) : "—";

    private static string FormatAmount(decimal value)
        => value.ToString("N2", Azerbaijani) + " AZN";

    private static void EnsureFontsRegistered()
    {
        if (GlobalFontSettings.FontResolver is not null)
            return;

        lock (FontRegistrationLock)
        {
            GlobalFontSettings.FontResolver ??= new EmbeddedNotoFontResolver();
        }
    }

    private static byte[] LoadResource(string fileName)
    {
        var name = typeof(ContractDocumentGenerator).Namespace!
            .Replace("Services.RestaurantContract.Concrete", "Templates.Contracts") + "." + fileName;
        using var stream = typeof(ContractDocumentGenerator).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing contract document asset: {name}");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private sealed class EmbeddedNotoFontResolver : IFontResolver
    {
        public const string FamilyName = "Noto Sans ECafe";
        private static readonly byte[] Regular = LoadResource("NotoSans-Regular.ttf");
        private static readonly byte[] Bold = LoadResource("NotoSans-Bold.ttf");

        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
            => new(isBold ? "NotoSans-Bold" : "NotoSans-Regular");

        public byte[]? GetFont(string faceName)
            => faceName == "NotoSans-Bold" ? Bold : Regular;
    }
}
