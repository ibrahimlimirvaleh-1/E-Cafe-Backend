# Provayder ödənişinin test rejimi

Bu modul rezervasiya depoziti, mövcud refund hüququ və masa sessiyasının yekun hesabı üçün rekvizitsiz test ödəniş axınıdır. Müqavilə ödənişi və cüzdan üçün yeni biznes funksiyası əlavə etmir. Restoranın cavab müddəti, ödəniş hold-u, depozitsiz rezervasiya və ləğv güzəşti əvvəlki qaydalarla işləyir.

## Test rejiminin açılması

Hazırda real müştərisi olmayan test layihəsi üçün standart rejim `Simulation`, `EnableSimulation=true`-dur. Bu ayarlar həm kodun standartında, həm də `appsettings.json` daxilində verilib:

```json
"Payments": {
  "Mode": "Simulation",
  "EnableSimulation": true
}
```

Test serveri `ASPNETCORE_ENVIRONMENT=Production` ilə işləsə də simulyasiya aktivdir; host adı ödənişin real olması demək deyil. Local, Development, Staging, Testing və Production host adları dəstəklənir; tanınmayan host mühiti bloklanır. Hostun təhlükəsizlik ayarlarını dəyişmək tələb olunmur.

Söndürmək üçün `Payments__Mode=Disabled` və ya `Payments__EnableSimulation=false` verin. Environment dəyişənləri və mühitə aid konfiqurasiya standart ayarları üstələyə bilər; serverdə əvvəlki disable ayarı qalarsa onu ayrıca yeniləmək lazımdır. Real müştərilər açılmadan əvvəl bu müvəqqəti standart simulyasiya söndürülməlidir. Test bazası və bildiriş alıcıları canlıdan ayrılmalıdır. `IsTest=false` olan ödəniş və refund simulyasiya endpointləri ilə dəyişdirilə bilməz.

`AddProviderReadyTestPayments` migration-ını əvvəl test bazasında tətbiq edin. Bu migration kart məlumatı yaratmır, köhnə məlumatı silmir və əvvəlki əməliyyatları test əməliyyatına çevirmir. Yeni əməliyyatlar `IsTest` ilə işarələnir. Pul tutulmur və banka refund göndərilmir.

## Rezervasiya depoziti

1. Restoranın meneceri/sahibkarı `POST /api/v1/restaurants/{restaurantId}/reservations/{reservationId}/payment/authorize` çağırır. Rekvizit və body yoxdur. Cavab müddəti və masa uyğunluğu yoxlanır; təkrar çağırış hold-u uzatmır.
2. Müştəri `POST /api/v1/reservations/{reservationId}/payment/checkout` çağırır. `Idempotency-Key` UUID formatındadır; məbləğ serverdən gəlir. Eyni cəhd eyni açarla təkrarlanır. Pending checkout mövcuddursa başqa açarla ikinci əməliyyat açılmır.
3. Testdə `POST /api/v1/payments/{paymentId}/test-outcome` üçün `outcome` olaraq `Pending`, `Succeeded`, `Failed` və ya `Cancelled` göndərilir. Kart, CVV, PIN, IBAN, məbləğ və çek forması yoxdur.
4. `POST /api/v1/payments/{paymentId}/refresh` provayder nəticəsini serverdə yoxlayır. Browser redirect-i uğurun mənbəyi sayılmır. Gecikmiş uğur bitmiş rezervasiyanı bərpa etmir; həmin ödəniş üçün kompensasiya refundu yaradılır.

## Geri ödəniş

Müştərinin mövcud refund müraciət endpoint-i saxlanılır. Əvvəlki ləğv qaydasının saxladığı `RefundEligible` hüququ yoxlanır. Provayder əməliyyatı varsa refund ilkin ödənişə bağlanır, yeni rekvizit və müştərinin köçürmə təsdiqi tələb olunmur.

Refund əvvəl `Processing` olur. `POST /api/v1/refunds/{refundId}/refresh` nəticəni yoxlayır. Testdə `POST /api/v1/refunds/{refundId}/test-outcome` ilə `Pending`, `Succeeded` və ya `Failed` seçilir. Yalnız uğurlu nəticə `RefundedAt` yazır. Təkrar nəticə tarix, bildiriş və məbləği təkrarlamır. Bu mərhələdə yalnız tam refund dəstəklənir; uğursuz refund təkrar pul köçürülmüş sayılmır və operator yoxlaması tələb edir.

Əvvəlki əl ilə ödənilmiş depozitin provayder istinadı yoxdur. Belə müraciət `RequiresManualReview` kimi göstərilir; test provider-i onu ödənmiş/refund edilmiş saya bilməz. Əvvəlki `PaymentSubmitted` rezervasiyalar da canlı keçiddən qabaq operator tərəfindən ayrıca uzlaşdırılmalıdır. Köhnə təsdiqlənmiş refund tarixçəsi saxlanılır.

## Manual məlumatların dayandırılması

Rekvizit göndərmə, çek yükləmə/təsdiq/rədd, payout-details, köçürmə çeki və müştəri təsdiqi/etiraz endpointləri model binding-dən əvvəl `410 ManualPaymentRetired` qaytarır. Workflow onları yeni əməliyyat kimi göstərmir. Rezervasiya/refund cavabları köhnə rekvizitləri və çek URL-lərini çıxarmır; PaymentReceipt fayllarının yeni upload və download-u bloklanır.

Köhnə şifrəli rekvizitlər, fayllar, audit və ehtiyat nüsxələr avtomatik silinmir. Onların saxlanma müddəti, hüquqi/mühasibat tələbi, ehtiyat nüsxələrdə silinməsi və operator uzlaşdırması ayrıca təsdiqlənmiş təmizləmə planı tələb edir. Bu dəyişiklik hüquqi uyğunluq sertifikatı deyil.

## Real provayder qoşulanda

`IPaymentGateway` sərhədi və maliyyə vəziyyət policy-ləri mövcuddur, lakin canlı adapter və credentials bu mərhələdə yoxdur. Sadəcə konfiqurasiyanı dəyişmək real ödənişi aktivləşdirmir. Real müştərilərə açılışdan qabaq simulyasiya söndürülməli, ayrıca yoxlanmış canlı provayder implementasiyası və rejimi əlavə olunmalıdır; `IsTest=false` qoruması saxlanılmalıdır.

- Kart sahələri provayderin hosted checkout səhifəsində olmalıdır. Backend məbləğ, valyuta, merchant/restoran aidiyyəti və əməliyyat istinadını yoxlamalıdır.
- Provayder sorğuları üçün saxlanmış payment/refund ID-lərindən sabit idempotency açarı yaradılmalıdır. Xarici sorğunun cavabı itərsə yeni əməliyyat yaradılmamalıdır.
- Canlı şəbəkə sorğuları üçün davamlı intent/outbox, retry, timeout və reconciliation worker əlavə edilməlidir. Hazırkı adapter yalnız lokal simulyasiyadır; onun transaction daxilindəki ani çağırışını canlı HTTP sorğusu ilə birbaşa əvəzləməyin.
- Webhook imzası, təkrar event-lər, gec və ardıcıllığı pozulmuş nəticələr, refund uğursuzluğu, merchant settlement və sandbox uçtan-uca testləri tamamlanmalıdır. İmzasız ümumi success callback açılmamalıdır.
- Canlıya çıxışdan əvvəl operatorun əvvəlki manual əməliyyatları uzlaşdırması və ayrıca PostgreSQL paralellik/rollback sınağı tələb olunur.

CVV-nin avtorizasiyadan sonra saxlanması şifrəli olsa belə PCI DSS ilə qadağandır: [PCI SSC FAQ](https://www.pcisecuritystandards.org/faqs/1319/). Bu, məbləğ və əməliyyat ID-si kimi bütün ödəniş metadata-sının saxlanmasının qadağan olması demək deyil.
