# Təkrarlanan string sabitlərinin mərkəzləşdirilməsi

Branch: `security/logging-production-hardening`. Əvvəlki təhlükəsizlik və
private-helper dəyişiklikləri qorunub. Commit, push və canlı deploy yoxdur.

## Nə dəyişdi

1. `Shared/Security/AuthenticationClaimTypes` JWT üçün 13 mövcud claim adını
   saxlayır. Token yazan `JwtManager` və onu oxuyan middleware, SignalR,
   icazə, fayl, audit, profil, mobil push, email dəyişmə və hesab silmə axınları
   eyni adlardan istifadə edir. `userId`, `sessionId`, `restaurantRoles` kimi
   xarici adlar dəyişməyib. Standart `ClaimTypes.Role` və `NameIdentifier`
   fallback-ləri saxlanılıb.
2. `Shared/Http/RequestHeaderNames` tətbiqə aid `Idempotency-Key`,
   `X-Active-Restaurant-Id`, `X-Active-Role-Id`, `X-Correlation-Id` adlarını
   saxlayır. Ayrı siniflərdəki eyni header sabitləri çıxarılıb. Standart
   `User-Agent` üçün yeni sabit uydurulmayıb: framework-in `HeaderNames.UserAgent`
   dəyəri istifadə olunur.
3. `Common/Configuration/AuthenticationConfigurationKeys` mövcud `Frontend:BaseUrl`
   və dörd `Jwt:*` yolunu bir yerdə saxlayır. API başlanğıcı, JWT servisi,
   parol linki və email dəyişmə eyni adları oxuyur. Bunlar yalnız ayarların
   adlarıdır; məxfi dəyərlər burada saxlanılmır. Env/appsettings dəyişməsi tələb olunmur.
4. Workflow kodları məsuliyyətlərinə görə `ContractActionCode`, `ReservationActionCode`
   və `RefundActionCode` siniflərində saxlanılır. Əvvəlki ümumi `WorkflowActionCode`
   sinfi çıxarılıb. Müqavilənin `sendForSignature`, `approve`, `activate`, `terminate`
   və rezervasiyanın `complete` kodları da mərkəzləşdirilib. Seeder və runtime eyni
   sabitləri oxuyur. ID sırası, status, rol, endpoint, label və isEnabled dəyişməyib.
5. Təkrarlanan `application/json` əvəzinə hazır `MediaTypeNames.Application.Json`,
   workflow seederinin `POST`/`GET` dəyərlərində hazır `HttpMethods` istifadə edilir.
6. Auth outbox əlaqəsindəki `User` və audit interfeysinin standart `Restaurant`
   dəyəri üçün mövcud `AuditEntityTypes` istifadə edilir; yeni paralel kataloq yaradılmayıb.

Məsələn, əvvəl `principal.FindFirst("userId")`, indi
`principal.FindFirst(AuthenticationClaimTypes.UserId)` yazılır. Compiler adın
səhv yazılmasını aşkar edir, amma müştəriyə verilən token yenə `userId` saxlayır.
Sabitlərə keçid öz-özlüyündə yeni təhlükəsizlik yoxlaması əlavə etmir;
mövcud yoxlamaların yanlış yazılmış fərqli string-lərdən istifadə riskini azaldır.

## Workflow Məsuliyyətlərinin Bölgüsü

İstifadəçinin SRP üzrə rəyindən sonra ümumi, nested qruplu `WorkflowActionCode`
çıxarıldı. Hər workflow-un identifikatorları ayrıca sinif və faylda saxlanılır:
`ContractActionCode` (4 kod), `ReservationActionCode` (10 kod), `RefundActionCode`
(5 kod). Məsələn, `WorkflowActionCode.Contract.Terminate` əvəzinə
`ContractActionCode.Terminate` istifadə edilir. Bütün 9 çağırış faylı uyğunlaşdırılıb.

Faylların ayrılması təkbaşına SRP-nin sübutu deyil: məqsəd fərqli workflow-ların
identifikatorlarını eyni ümumi tipdən asılı etməməkdir. Yeni siniflər yalnız öz
identifikatorlarını saxlayır; biznes davranışı əvvəlki servis və policy-lərdədir.
Lazımsız interface, DI qeydiyyatı və ya köhnə ümumi façade əlavə edilməyib.

## Sərhədlər

- Eyni mətnin hər istifadəsi eyni məna daşımır. Route/query parametrinin
  `restaurantId` adı claim adı ilə təsadüfən eyni olsa da, JWT sabitinə bağlanmayıb.
- DB sütunları, SQL, schema və köhnə migration/snapshot faylları mexaniki dəyişdirilməyib.
- Mövcud raw biznes mesajları başqa error code-a çevrilməyib: bu, cavab kodu və
  lokalizasiya davranışını dəyişərdi. Bu işin əhatəsi təkrarlanan texniki və
  workflow identifikatorlarıdır; bütün mətnlərin ayrıca lokalizasiya miqrasiyası deyil.
- Yalnız bir yerdə işlənən təbii sabitlər məqsədsiz yeni siniflərə daşınmayıb.
- Frontend və mobil kodu dəyişmək lazım deyil: xarici claim/header/action dəyərləri eynidir.
- İcazə, tenant seçimi, header prioriteti, token TTL-i, sessiya/keş, rate limit,
  transaction və idempotency qaydaları dəyişməyib.
- Dəyişən private və köməkçi metodlarda Azərbaycan dilində qısa XML izahları saxlanılıb/əlavə edilib.

## Yoxlamalar

`StringConstantRefactoringTests` 35 yeni sınaqla xarici claim/header/ayar/action
adlarını, köhnə literal claim-lərin oxunmasını, real JWT payload-unu,
idempotency endpoint binding-lərini, JSON consume atributlarını və EF seed/schema
uyğunluğunu yoxlayır. Testlərin gözlənilən dəyərləri qəsdən literal saxlanılır:
test və implementation eyni səhv sabitə baxıb yalnış müsbət nəticə verməməlidir.

İlkin məqsədli Release paketi: **96/96 keçdi**. Buna əvvəlki helper və log
məxfiliyi testləri də daxildir. `git diff --check` təmizdir.
Tam Release regression paketi ayrıca lokal PostgreSQL və ACL-li Redis üzərində:
**2028/2028 keçdi, 0 xəta, 0 skip** (7 dəqiqə 30 saniyə). Əvvəlki runtime
təhlükəsizlik transformlarının 12 Python testi də yenidən keçdi.

Bu tam paket nəticəsi son workflow-kataloq bölgüsündən əvvəlki string refaktoruna
aiddir. Son bölgüdən sonra ayrıca əlaqəli Release paketi **252/252 keçdi,
0 xəta, 0 skip**: 22 yeni kataloq testi, müqavilə/rezervasiya/refund/ödəniş,
retired-payment qorumaları, string müqavilələri, log məxfiliyi və EF schema/seed
uyğunluğu yoxlanıldı. Bütün həllin Release build-i **0 warning, 0 error** ilə keçdi.
Bu son dar dəyişiklik üçün real PostgreSQL/Redis paketi təkrar işlədilməyib.

Son bölgünün TRX-i `tmp/workflow-catalogs-20261124/workflow-catalogs.trx` faylındadır.

TRX workspace-in repodan kənar `tmp/string-refactor-20261124/strings-full.trx`
faylında saxlanılır. Testlər canlı serverə və onun Redis/PostgreSQL məlumatlarına
müraciət etmir. Test üçün yaradılan konteynerlər və təsadüfi ACL parolları işin
sonunda təmizlənir; test hesabatları saxlanılır.
