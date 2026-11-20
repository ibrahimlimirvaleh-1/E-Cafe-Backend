# Təsdiqlənən e-poçt dəyişikliyi

Tarix: 20.11.2026. Backend branch-i: `security/verified-email-change`.

## Məqsəd

Açıq qalmış hesabı əldə edən şəxs sadəcə profil formasını dolduraraq hesabın
e-poçtunu öz ünvanına keçirə bilməsin. Başqa hesabın ünvanını dəyişmək, köhnə
linki təkrar işlətmək və paralel sorğularla qorumadan yayınmaq da rədd edilsin.

## Trello kartları

1. **İstifadəçinin yenidən təsdiqlənməsi:** cari parol BCrypt ilə yoxlanır.
   İstifadəçi identifikatoru payload-dan deyil, təsdiqlənmiş principal-dan gəlir.
   Kiliddən sonra hesabın aktivliyi, sessiya versiyası və cihaz sessiyası yenidən yoxlanır.
2. **Yeni ünvanın təsdiqi:** yeni e-poçta 30 dəqiqəlik, birdəfəlik link gedir.
   Təsdiqədək əvvəlki ünvan qüvvədə qalır. Cədvəldə tokenin yalnız SHA-256 hash-i saxlanır.
3. **Təkrar/paralel sorğular:** mövcud `auth.users FOR UPDATE` kilidi eyni hesabın
   sorğu və təsdiqlərini ardıcıllaşdırır. Yeni sorğu əvvəlki gözləyən sorğunu ləğv edir.
   Hesab/sessiya versiyası və əvvəlki ünvan dəyişibsə köhnə link etibarsız olur.
   Ayrı hesabların eyni ünvanı paralel seçməsini DB-dəki unikal email indeksi də bloklayır.
4. **Limit, audit və sessiya siyasəti:** parol səhvləri mövcud login cəhdləri və
   bloklama mexanizminə yazılır. E-poçt sorğuları üçün DB əsaslı limit hesab üzrə
   15 dəqiqədə 3 sorğudur; bütün API nüsxələri üçün ortaqdır. Endpoint səviyyəsində
   əlavə istifadəçi limiti var. Sorğu zamanı əvvəlki ünvana xəbərdarlıq göndərilir.
   Təsdiqdən sonra hər iki ünvana xəbərdarlıq gedir, bütün refresh sessiyaları və
   köhnə parol bərpa/təyin linkləri ləğv edilir, sessiya versiyası artırılır.

Bu dörd kartın texniki icrası backend və frontenddə tamamlanıb; Trello statusu
**Testə hazır** olmalıdır, deploydan sonrakı tester təsdiqi hələ gözləyir.
Backend avtomatik testləri, frontend testləri və əl ilə qəbul nəticəsi ayrı saxlanılır. İnfrastruktur üzrə
5-8-ci kartlar (GitHub/deploy, SSH, backup bərpası, şəbəkə) bu işdə icra edilmir.

## Axın və API müqaviləsi

Hər iki endpoint `[Authorize]` və JSON payload tələb edir. GET/səhifəni açmaq
dəyişiklik etmir; frontend istifadəçinin açıq təsdiq əmrinə qədər POST göndərmir.

| Endpoint | Payload | Uğurlu cavab |
| --- | --- | --- |
| `POST /api/v1/profile/email-change/request` | `newEmail`, `currentPassword` | `202`, boş body |
| `POST /api/v1/profile/email-change/confirm` | `token` | `204`, yenidən login tələb olunur |

Başqa hesaba aid ünvan üçün sorğu da eyni `202` cavabını alır; təsdiq məktubu
göndərilmir. Eyni ünvanın seçilməsi də eyni xarici cavabı verir. Bu hallarda da
sorğu hesab limitinə daxildir. Təsdiq zamanı ünvan artıq tutulubsa ümumi
`EmailChangeInvalidOrExpired` xətası gəlir; digər hesabın kimliyi açıqlanmır.
Bu, cavab məzmununu ümumiləşdirir; bütün şəbəkə vaxtlarının eyniləşdirilməsi zəmanəti deyil.

`PUT /api/v1/profile` ad, soyad, telefon və şəkli əvvəlki kimi yeniləyir, lakin
fərqli email üçün `EmailChangeRequiresVerification` qaytarır. Personal redaktəsində
də bütün rollar üçün birbaşa email dəyişikliyi bağlıdır. Mapper-lər emaili yazmır.

## Məlumat bütövlüyü və məxfilik

- Sorğu/təsdiq, audit və email outbox yazıları eyni DB transaction-ındadır.
  Məktubun SMTP-yə faktiki çatdırılması mövcud outbox worker-in məsuliyyətidir.
- Məktub növbəsinə yazı alınmırsa hesab dəyişikliyi, tokenin istifadəsi və
  sessiya ləğvləri birlikdə geri qaytarılır.
- Təsdiq yalnız həmin hesabın cari aktiv girişində mümkündür. Başqa hesabın
  sessiyası tokeni istifadə edə bilmir. Köhnə sessiya ilə uğurlu təkrar təsdiq yoxdur.
- Link `Frontend:BaseUrl/confirm-email-change#token=...` formasındadır.
  Fragment serverə query kimi getmir. Frontend fragmenti dərhal təmizləməli,
  tokeni yalnız yaddaşda saxlamalı və log/analytics/storage-a yazmamalıdır.
- Base URL konfiqurasiyadan götürülür; request Host və redirect parametrindən deyil.
  HTTPS tələb edilir; lokal inkişaf üçün yalnız loopback HTTP qəbul edilir.
- Token və parol auditə yazılmır. Məktub göndərmək üçün link email outbox
  payload-ında mövcuddur; outbox artıq məxfi məlumat kimi qorunmalı və icazəli
  operatorlardan başqasına açılmamalıdır. Hash saxlamaq bunu şifrələmə ilə eyniləşdirmir.
- Başlıq/sətir inyeksiyası və çoxsaylı email alıcıları validator tərəfindən rədd edilir.
- Bütün sessiyaların ləğvi növbəti təhlükəsizlik yoxlamasında qüvvəyə minir;
  açıq WebSocket-in dərhal kəsilməsi və başlamış əməliyyatın geri alınması zəmanəti deyil.

## Deploy şərtləri

1. Yeni `AddVerifiedEmailChanges` miqrasiyasını mövcud migrator qaydası ilə tətbiq et.
   Miqrasiya yalnız yeni `auth.email_change_requests` cədvəli və indekslərini yaradır.
2. Backend və uyğun frontend birlikdə yayımlanmalıdır. Köhnə frontend emaili
   dəyişməyə çalışarsa server onu qəbul etməyəcək; təhlükəsiz fallback budur.
3. `Frontend:BaseUrl` düzgün HTTPS sayt ünvanı olmalıdır. SMTP və email outbox
   worker işlək olmalıdır. Məxfi açarları, Redis ACL-lərini və keyring-i dəyişmə.
4. Miqrasiyanı silən rollback gözləyən təsdiq sorğularını itirir. Köhnə tətbiqə
   rollback birbaşa profil emaili dəyişmə riskini qaytara bilər; ayrıca qiymətləndir.
5. Canlı serverdə Redis/DB dayandırılmır və test üçün real istifadəçi hesabı dəyişdirilmir.

## Tester üçün əsas sınaqlar

1. Ad/telefon/şəkli saxla: normal profil redaktəsi işləməlidir.
2. E-poçtu dəyişməyə səhv parolla çalış: ünvan dəyişməməli, məktub getməməlidir.
3. Düzgün parolla yeni ünvan soruş: köhnə ünvan qalır; yeni ünvana təsdiq,
   əvvəlki ünvana xəbərdarlıq gəlir.
4. Linki aç, hələ təsdiqləmə: ünvan dəyişməməlidir; URL-də token qalmamalıdır.
5. Başqa hesabla həmin linki təsdiqlə: rədd edilməlidir.
6. Düzgün hesabda təsdiqlə: yeni ünvan aktiv, bütün köhnə girişlər ləğv,
   yeni ünvan və mövcud parolla yenidən giriş mümkündür.
7. İkinci sorğu yaratdıqdan sonra birinci linki yoxla: işləməməlidir.
8. Vaxtı bitmiş/təkrar istifadə olunan linki və parol bərpasından sonra köhnə
   linki ayrıca test mühitində yoxla: dəyişiklik etməməlidir.
9. Təkrar sorğuların limitini və iki paralel təsdiqi yoxla: əlavə dəyişiklik və
   məktub yaranmamalıdır. Bu sınaqlar üçün real insanlara məktub spamı yaratma.
10. Rezervasiya, sifariş, stok, hesab, ödəniş və bildiriş axınını test hesabları ilə yoxla.
11. Ayrıca brauzer inteqrasiya sınağında gecikmiş refresh cavabından sonra yeni
    login-i və onun növbəti refresh-ini yoxla. JavaScript auth vəziyyətinin qorumaları
    HttpOnly `Set-Cookie` başlığının brauzerə çatma ardıcıllığını öz-özünə idarə etmir.

## Avtomatik yoxlama qeydi

Yekun tam Release test paketi: **1736 keçdi, 0 xəta, 0 buraxılmış test**
(4 dəqiqə 24 saniyə). Nəticə repository xaricində lokal iş qovluğundakı
`email-security-full-serial/*.trx` faylındadır. Bütün DB/Redis sınaqları ayrıca
loopback PostgreSQL/Redis-də işləyib; canlı məlumat və SMTP istifadə edilməyib.

Bu keçiddə real PostgreSQL-də 16 paralel təsdiq, 8 paralel dəyişiklik sorğusu,
iki hesabın eyni ünvanı boş gördükdən sonrakı yazı yarışı, parol cəhdlərinin
ayrı scope-larda bloklanması, transaction rollback və miqrasiyanın Up/Down
sınaqları daxildir. Mövcud Redis ACL və dayanma/bərpa testləri də aktiv olub.

İlkin 60 əlaqəli test də keçmişdi. Geniş paralel keçiddə isə əvvəlki
`OrderCancellationTests.CancellationTextsAreCompleteAndNotificationTemplatePreservesArguments(en)`
testində mövcud `TranslationSourceMatcher` 50 ms regex timeout-u müşahidə edildi:
1735 keçdi, 1 xəta, 0 buraxılmış test. Eyni yekun binarilərlə kolleksiyaları
ardıcıl işlədən son keçid tam uğurludur. Testlərin daxilindəki 8/16-sorğulu
paralellik ssenariləri söndürülməyib. Tərcümə mexanizminin yük/cold-start
həssaslığı ayrıca performans işi kimi qalır; bu işdə timeout böyüdülməyib və
əlaqəsiz production kodu dəyişdirilməyib.

Frontendin TypeScript/build/i18n, 20 izolə edilmiş brauzer ssenarisi və 33
desktop/mobil screenshot yoxlaması keçib. Fayl yükləmə və personal rolunun
lokalizasiya regresiyaları da keçib. AZ/EN/RU üçün 26 `ui.emailChange.*` açarı
backend bootstrap kataloquna əlavə edilib. Əlavə 16 deterministik auth-race
sınağı köhnə refresh-in uğurlu/xətalı cavabının yeni login-in JavaScript
token vəziyyətini əvəz etmədiyini və yeni sessiyanı bağlamadığını yoxlayır.
Refresh deduplication auth revision-a bağlıdır; normal refresh/retry/bootstrap
və expiry axınları da sınaqdadır. Bu, brauzerin HttpOnly cookie yazı ardıcıllığının
real HTTP inteqrasiya testi ilə eyni deyil; həmin sərhəd tester yoxlamasında qalır.
Brauzer yoxlamaları saxta API
cavabları ilə aparılıb: real məktub çatdırılması və iki tərəfin birgə HTTP
axını deploydan sonra tester tərəfindən ayrıca təsdiqlənməlidir.

Commit və push ayrıca istifadəçi təsdiqi ilə aparılır; deploy və canlı server
dəyişikliyi bu işə daxil deyil. Əvvəlki sənədləşdirmə faylları və
`IUserRestaurantAccessCache.cs` rəyindəki lokal dəyişiklik bu işin commitinə daxil edilmir.

Texniki istinad: [OWASP - həssas əməliyyatlarda təkrar təsdiq və email dəyişikliyi](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html).
Bu dəyişiklik bütün hesab ələ keçirmə ssenarilərini və bütün layihə risklərini
bağladığı barədə təsdiq deyil.
