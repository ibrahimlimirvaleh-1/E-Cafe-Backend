# Ödəniş sorğularının limiti və PostgreSQL paralellik testləri

Tarix: 24.11.2026. Branch: `security/payment-request-limits`.

## Nə dəyişdi?

Ödəniş controller-ləri layihənin mövcud ASP.NET Core rate-limit mexanizminə qoşuldu.
Autentifikasiya olunmuş hesab üçün sayğac istifadəçi ID-si ilə qurulur.
Eyni istifadəçinin IP, restoran, masa, payment ID və idempotency açarını dəyişməsi
eyni policy üzrə yeni limit yaratmır. Anonim sorğularda IP istifadə olunur;
bu, autentifikasiyanı əvəz etmir.

| Ayrı sayğac | Əhatə | Standart limit |
|---|---|---|
| `PaymentMutation` | Depozit icazəsi, checkout, refund sorğusu, test nəticəsi | 30 / 60 saniyə |
| `PaymentRefresh` | Depozit, masa hesabı və refund statusunu provayderdən yeniləmə | 120 / 60 saniyə |
| `PaymentRead` | Ödəniş imkanları, rezervasiya ödənişi və masa hesabını oxuma | 120 / 60 saniyə |

Bir qrupun limitini doldurmaq digər qrupun limitini sərf etmir.
Standartda növbə yoxdur: artıq sorğu biznes handler-inə çatmadan `429` alır.
Mövcud `TooManyRequests` JSON cavabı saxlanır; `Retry-After` HTTP başlığı əlavə edilib.
Bu başlıq mövcud digər rate-limit policy-lərinin cavablarına da əlavə olunur.
Limit bitdikdən sonra sorğu yenidən verilə bilər; checkout retry-si eyni
idempotency açarı ilə edilməlidir.

Mövcud konfiqurasiya mexanizmi ilə dəyişmək olar:
`RateLimiting:{PaymentMutation|PaymentRefresh|PaymentRead}:{PermitLimit|WindowSeconds|QueueLimit}`.
Məsələn environment açarı: `RateLimiting__PaymentMutation__PermitLimit`.
`appsettings`, məxfi ayarlar, paketlər və DB sxemi dəyişməyib; migration lazım deyil.

## Hansı qorumalar saxlanıldı?

- Autentifikasiya, rollar, icazələr, aktiv istifadəçi və cari restoran profili yoxlamaları.
- Məbləğin serverdə hesablanması, masa hesabının fingerprint-i və provayder nəticəsinin yoxlanması.
- İstifadəçi/masa kilidləri, transaction sərhədləri, unikal indekslər və idempotentlik.
- Eyni təsdiqin təkrar pul, refund, tarixçə, audit və bildiriş yaratmaması.
- Gecikmiş ödənişin vaxtı bitmiş rezervasiyanı yenidən aktivləşdirməməsi.
- Köhnə əl ilə ödəniş endpoint-lərinin `410` davranışı və simulyasiya qorumaları.

Maliyyə servis/repository kodu dəyişdirilməyib; yeni DB testləri mövcud qorumaları yoxlayır.

## Yeni testlər

`PaymentRateLimitTests.cs`: 21 test nümunəsi. Policy/authorization metadata,
real loopback HTTP/MVC limitləri, `429`/`Retry-After`, ayrı istifadəçi və sayğaclar,
anonim IP fallback, limitin bərpası və paralel HTTP burst yoxlanılır.
Mediator yalnız bu HTTP testlərində əvəz edilir; onlar maliyyə hesablaması testi deyil.

`PaymentConcurrencyPostgresTests.cs`: 28 test nümunəsi. Ayrı Npgsql bağlantıları,
həqiqi PostgreSQL kilidləri/transaction-ları və production maliyyə servisləri ilə:

- Paralel eyni/fərqli açarlı depozit və masa checkout-u; bir pending ödəniş.
- İki restoran arasında eyni checkout açarının paralel istifadəsi.
- Müştəri, menecer və məsul ofisiantın paralel masa ödənişləri.
- Paralel ödəniş/refund təsdiqi: bir dəfə məbləğ və bildiriş.
- Gateway, SQL yazısından sonrakı audit/bildiriş xətası və cancellation zamanı rollback.
- Rollback-dən sonra kilidin/açarın azad olması və uğurlu retry.
- Kilid gözləyərkən hesab/profil deaktivliyi: köhnə icazə ilə ödəniş yaranmır.
- Başqa müştəri/restoran və qeyri-test ödənişinə girişin rəddi.
- Gecikmiş ödəniş üçün yalnız bir kompensasiya refund-u; onun xəta zamanı rollback-i.
- Birbaşa SQL yazısında pending ödəniş unikal indekslərinin qoruması (`23505`).

Audit, bildiriş, push/e-poçt outbox qeydləri real test bazasına yazılır.
Xarici göndəriş worker-ləri işə salınmır; bank, e-poçt və mobil push çağırışı edilmir.
Rollback lokal SQL məlumatlarına aiddir: real bank əməliyyatının SQL rollback ilə
geri alınacağını iddia etmir. Layihədə hazırda simulyasiya adapteri istifadə olunur.

DB testləri mövcud təhlükəsiz fixture-i istifadə edir: yalnız loopback,
standart olmayan PostgreSQL portu, `postgres` admin bazası və hər test üçün
yeni GUID-li müvəqqəti baza. `ECAFE_STOCK_TEST_CONNECTION` verilməsə skip olunur;
mövcud CI bu dəyişəni və ayrıca PostgreSQL servis konteynerini artıq verir.

Təkrar işə salmaq üçün ayrıca lokal test PostgreSQL bağlantısını həmin environment
dəyişəninə yazıb `dotnet test tests/ECafe.Tests/ECafe.Tests.csproj --filter "FullyQualifiedName~PaymentPg|FullyQualifiedName~PaymentRateLimitTests"`
işlətmək olar. Canlı server bağlantısı istifadə edilməməlidir.

## Sərhədlər

Limit fixed-window və API prosesinə məxsusdur; distributed Redis kvotası deyil.
Bir neçə API instansiyasında ümumi kvota lazım olarsa gateway/distributed həll tələb olunur.
Bu iş DDoS müdafiəsini, real bank adapterini, webhook imzasını və xarici provayderdə
retry/idempotentlik tələblərini əvəz etmir. Frontend, proxy və canlı server dəyişməyib.
Commit və push edilməyib.

## Yoxlama nəticələri

Release build: uğurlu, 0 xəta və 0 xəbərdarlıq.
Tam regressiya: **1932 keçdi, 0 uğursuz, 0 skip**; müddət 8 dəqiqə 5 saniyə.
Buraya 21 yeni HTTP/metadata və 28 yeni real PostgreSQL test nümunəsi daxildir.
Lokal sınaqda PostgreSQL 17 və ayrıca ACL-li Redis 7 konteynerləri istifadə olundu;
sessiya, icazə keşi, real vaxt bağlantıları və mövcud biznes testləri də işlədi.
CI nəticəsi deyil: istifadəçinin istəyinə əsasən hələ commit/push edilməyib.
