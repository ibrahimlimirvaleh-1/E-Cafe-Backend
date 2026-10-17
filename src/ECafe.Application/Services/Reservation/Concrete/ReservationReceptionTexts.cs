namespace ECafe.Application.Services.Reservation.Concrete;

internal static class ReservationReceptionTexts
{
    public const string ArrivedTitle = "Müştəri gəlib, masa gözləyir";
    public const string WaitingTitle = "Müştərinin gözləmə müddəti uzanıb";
    public const string ReceptionReviewTitle = "Rezervasiyanın nəticəsini yoxlayın";
    public const string TableChangedTitle = "Rezervasiyanın masası dəyişdirildi";
    public const string TableChangedMessage = "Müştərinin razılığı ilə masa dəyişdirildi. Rezervasiya və çıxış vaxtı, depozit və geri ödəniş şərtləri dəyişmədi.";
    public const string SeatedHistory = "Müştəri masaya əyləşdi və masa açıldı.";
    public const string SeatedTitle = "Müştəri masaya əyləşdirildi";
    public const string DepartureTitle = "Gözləyən rezervasiya bağlandı";
    public const string RestaurantDepartureRefundReason = ECafe.Application.Services.Payments.PaymentTexts.RestaurantDepartureRefundReason;
    public const string GuestDepartureRefundReason = ECafe.Application.Services.Payments.PaymentTexts.GuestDepartureRefundReason;
    public const string WaitingSection = "waiting";

    /// <summary>Ofisianta gəliş və masa barədə maliyyə məlumatı olmayan bildiriş mətnini hazırlayır.</summary>
    public static string ArrivedMessage(int reservationId, string? tableName)
        => $"Rezervasiya #{reservationId} üzrə müştəri gəlib. Masa: {tableName}. Müştəri hələ əyləşməyib.";

    /// <summary>Menecerə rezervasiya vaxtından sonrakı xidmət gecikməsini bildirir.</summary>
    public static string WaitingMessage(int reservationId, int delayMinutes)
        => $"Rezervasiya #{reservationId} üzrə müştəri {delayMinutes} dəqiqədir əyləşdirilməyi gözləyir. Vəziyyəti yoxlayın.";

    /// <summary>Bitmiş və ya müəyyən edilə bilməyən xidmət intervalını real gözləmə kimi təqdim etmədən menecerdən nəticəni dəqiqləşdirməsini istəyir.</summary>
    public static string ReceptionReviewMessage(int reservationId)
        => $"Rezervasiya #{reservationId} üzrə gəliş qeydə alınıb, lakin əyləşdirilmə və ya ayrılma qeydi yoxdur. Xidmət intervalı bitib və ya müəyyən edilə bilmir. Faktiki vəziyyəti yoxlayıb nəticəni qeyd edin. Bu bildiriş müştərinin hazırda gözlədiyini göstərmir.";

    /// <summary>Restoran əməkdaşlarına gözləmə növbəsindən çıxarılmalı olan rezervasiyanı bildirir.</summary>
    public static string ClosedMessage(int reservationId)
        => $"Rezervasiya #{reservationId} artıq masa gözləmir. Siyahıdakı vəziyyət yeniləndi.";
}
