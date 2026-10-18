namespace ECafe.Application.Services.Payments;

public static class SessionBillTexts
{
    public const string PaidTitle = "[TEST] Masa hesabı ödənildi";
    public const string ReviewTitle = "[TEST] Masa ödənişi yoxlanmalıdır";
    /// <summary>Yalnız sessiya və məbləğlə rekvizitsiz, real pul hərəkəti olmadığını bildirən mətn yaradır.</summary>
    public static string PaymentMessage(int sessionId, decimal amount, bool review)
        => review ? $"Sessiya #{sessionId}: {amount:0.00} AZN test ödənişi dəyişmiş hesaba uyğun gəlmir; restoran məsulu yoxlamalıdır. Real pul hərəkəti yoxdur."
            : $"Sessiya #{sessionId}: {amount:0.00} AZN test ödənişi təsdiqləndi. Real pul hərəkəti yoxdur.";
}
