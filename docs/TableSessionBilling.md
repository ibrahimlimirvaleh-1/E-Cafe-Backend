# Masa sessiyasının yekun hesabı

Bu mərhələ yalnız test simulyasiyasıdır. Real pul tutulmur. `Payments` ayarları depozit və yekun hesab üçün eynidir; canlı provayder ayrıca implementasiya olunmadan real ödəniş rejimi yoxdur.

## Hesab qaydası

- Sifarişin saxlanmış `LineTotal` və `UnitPriceSnapshot` qiymətləri istifadə olunur; cari menyu qiyməti və browser məbləği qəbul edilmir. Ləğv edilmiş/silinmiş sifarişlər cəmə daxil deyil. Vergi, servis haqqı və endirim üçün yeni biznes qaydası əlavə edilməyib.
- Yalnız həmin rezervasiyanın ilk sessiyası təsdiqlənmiş, qaytarılmamış depoziti istifadə edir. 50 AZN sifariş və 10 AZN depozit üçün qalan borc 40 AZN-dir. Tarixi təsdiqlənmiş depozit çeki yeni çek qəbul etmədən nəzərə alınır; refund açılmış depozit ikinci dəfə istifadə edilmir.
- Depozit hesabdan böyükdürsə yalnız hesab qədər kredit tətbiq olunur. Artıq məbləğ avtomatik tutulmur və ya qaytarılmış sayılmır; mövcud refund hüququ öz qaydaları ilə qalır. Depozitin artıq hissəsi üçün yeni avtomatik refund siyasəti bu işə daxil deyil.
- Sessiya üzrə yalnız provayder metadata-sı və `PaidAt` ilə təsdiqlənmiş ödəniş borcu azaldır. `Pending`, `Failed`, `Cancelled`, `Refunded` və `RequiresRefund` borcu ödənilmiş saymır.
- Bütün aktiv sifarişlər təqdim edilmədən ödəniş və bağlanış mümkün deyil. Sıfır məbləğ üçün saxta ödəniş yaradılmır.

## API və icazə

Aktiv müştəri yalnız öz sessiyasına, ofisiant yalnız cari təyin olunduğu sessiyaya, aktiv menecer/sahibkar yalnız öz restoranına daxil ola bilər. Bağlı sessiyanın hesabı tarixçə üçün oxunur; ona yeni ödəniş açıla bilməz.

1. `GET /api/v1/table-sessions/{id}/bill`: cəm, depozit krediti, ödənilmiş məbləğ, qalıq, `CanPay`, `CanClose` və son ödəniş.
2. `POST /api/v1/table-sessions/{id}/bill/checkout`: body yoxdur. Boş olmayan UUID `Idempotency-Key` məcburidir. Eyni cəhdin təkrarında eyni açar istifadə edilir; qeyri-müəyyən cavab üçün yeni açar yaradılmır.
3. `POST /api/v1/table-sessions/{id}/bill/payments/{paymentId}/refresh`: nəticəni adapterdən serverdə yoxlayır.
4. `POST /api/v1/table-sessions/{id}/bill/payments/{paymentId}/test-outcome`: yalnız testdə `outcome` olaraq `Pending`, `Succeeded`, `Failed`, `Cancelled` qəbul edilir. Real əməliyyat simulyasiya edilmir.

Ödəniş yaratma, nəticənin tətbiqi, yeni sifariş və mövcud menecer/ofisiant bağlanışları eyni PostgreSQL masa advisory transaction kilidindən istifadə edir. Hər sessiya üçün yalnız bir gözləmədə ödənişə DB unique indeksi də nəzarət edir. Yeni checkout sifariş, depozit və məbləğin fingerprint-ini saxlayır. Gözləmədə checkout zamanı yeni sifariş bloklanır; uğursuz/ləğv cəhddən sonra yenidən mümkündür. Əvvəlki sifarişin idempotent təkrarı yeni sifariş sayılmır.

Uğurlu ödənişdən sonra əlavə sifariş yaratmaq olar; yalnız yeni qalan borc ödənilir. Əvvəlki ödəniş açarının təkrarı həmin əvvəlki nəticəni qaytarır. Yeni ayrıca ödəniş niyyəti üçün yeni açar lazımdır.

Uğur cavabı zamanı hesab dəyişibsə ödəniş `RequiresRefund` kimi yoxlamaya düşür, borcu silmir və yenidən ödəniş/bağlanış bloklanır. Test rejimində pul hərəkəti yoxdur; belə korlanmış məlumat operator tərəfindən uzlaşdırılmalıdır. Yekun hesab refundunun avtomatik icra və operator həlli ayrıca işdir, rezervasiya refund endpointinə ötürülmür.

## Bağlanış və bildiriş

`Complete` və `CompleteOwnReservation` endpointləri saxlanılır. Server `SessionBillUnpaid`, `SessionBillOrdersUnserved`, `SessionBillPaymentPending` və ya uyğunsuz məlumat üçün `SessionBillInvalid` ilə 409 qaytarır. UI düyməsinin deaktivliyi təhlükəsizlik sərhədi deyil. Uğurlu ödəniş sessiyanı avtomatik bağlamır; yalnız məsul işçi xidməti bitirir.

Təsdiq bir dəfə auditə və mövcud bildiriş outbox-una yazılır. Müştəri, cari məsul ofisiant və aktiv menecer/sahibkar test etiketi ilə xəbərdar edilir. Telefon çatdırılması mövcud qeydiyyat və restoran push icazələrinə tabedir.

## Migration və yoxlama

`20261018071354_AddTableSessionBilling` migration-ı `Payment.TableSessionId`, fingerprint, sessiya FK-si və pending unique indeksini əlavə edir. Köhnə depozit/refund məlumatı silinmir. Əvvəlcə test bazasında migration tətbiq edilməli, sonra UI və API eyni versiya ilə buraxılmalıdır.

Avtomatik EF InMemory testləri hesablama, idempotentlik, səlahiyyət, ortaq kilid simulyasiyası və hər iki bağlanış qorumasını yoxlayır. Bu testlər real PostgreSQL paralellik/rollback və bank sandbox testi əvəzi deyil. Canlı adapterə keçiddə durable intent/outbox, timeout, imzalı webhook, reconciliation və provider sandbox sınaqları tələb olunur; hazırkı lokal gateway-i transaction daxilində canlı HTTP ilə birbaşa əvəz etməyin.
