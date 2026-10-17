namespace ECafe.Application.Services.Payments;

public static class PaymentTexts
{
    public const string TestNotice = "Test ödənişidir. Real pul tutulmur və geri köçürülmür.";
    public const string Unavailable = "Onlayn ödəniş hazırda aktiv deyil. Restoranla əlaqə saxlayın.";
    public const string Authorized = "Restoran ödənişi təsdiqlədi. Depoziti onlayn ödəyə bilərsiniz.";
    public const string Confirmed = "Provayder depozit ödənişini təsdiqlədi.";
    public const string RefundPending = "Geri ödəniş ilkin ödənişə qaytarılması üçün emal edilir. Rekvizit göndərmək lazım deyil.";
    public const string RefundCompleted = "Provayder geri ödənişi təsdiqlədi. Vəsait ilkin ödəniş üsuluna qaytarılıb.";
    public const string RefundFailed = "Geri ödəniş uğursuz oldu. Restoran vəziyyəti yoxlamalıdır; məbləğ geri qaytarılmış sayılmır.";
    public const string ManualReview = "Əvvəlki əl ilə ödənişin provayder əməliyyatı yoxdur. Restoran ayrıca yoxlama aparmalıdır; burada kart/hesab məlumatı göndərməyin.";
    public const string LatePaymentRefund = "Ödəniş masa üçün ayrılmış vaxt bitdikdən sonra təsdiqləndi. Rezervasiya bərpa edilmir, məbləğ geri qaytarılmalıdır.";
    public const string RestaurantDepartureRefundReason = "Restoran gəlmiş müştərini masaya əyləşdirə bilmədiyi üçün rezervasiya ləğv edilib.";
    public const string GuestDepartureRefundReason = "Müştərinin ayrılması qeyd edilib; geri ödəniş hüququ mövcud müştəri ləğv qaydasına uyğun təsdiqlənib.";
}
