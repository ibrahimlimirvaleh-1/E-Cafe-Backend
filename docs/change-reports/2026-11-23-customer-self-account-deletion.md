# Müştərinin öz hesabını silməsi

## Nə əlavə edildi?

Profilin təhlükəsizlik bölməsində yalnız müştəri üçün "Hesabımı sil" əməliyyatı var.
Bu, restoran rəhbərliyinin işçini deaktiv etməsindən və platforma admininin ümumi silmə əməliyyatından ayrıdır.
İşçi və admin öz hesabını bu endpoint ilə bağlaya bilməz. Müştəri rolunda olsa da aktiv işçi profili olan hesab bloklanır.

## Müştəri nə görəcək?

1. Silmə pəncərəsi açılanda server hesabın vəziyyətini yoxlayır.
2. Tamamlanmamış iş varsa, səbəblər göstərilir və silmə açılmır.
3. Uyğun hesab cari şifrəsini yenidən yazır və geri dönüşsüzlüyü ayrıca təsdiqləyir.
4. Server şifrəni, hesabın həqiqi rolunu, cihaz sessiyasını və bütün şərtləri təkrar yoxlayır.
5. Uğurlu nəticədə hesabdan çıxış edilir. Digər cihazlara da çıxış hadisəsi göndərilir.

## Hansı hallarda silmək olmur?

| Maneə | Sadə izah |
| --- | --- |
| ActiveStaffProfile | Aktiv restoran işçi profili var. |
| ActiveReservation | Rezervasiya hələ tamamlanmayıb və ya ləğv edilməyib. |
| OpenTableSession | Restoranda masa sessiyası açıqdır. |
| PendingPayment | Ödəniş tamamlanmayıb və ya geri qaytarılması gözlənilir. |
| PendingRefund | Geri ödəniş prosesi həll edilməyib və ya ödənilmiş depozit üçün hələ tələb edilməmiş geri ödəniş hüququ var. |
| UnpaidBill | Hesab borcludur, yoxlama tələb edir və ya hesablaşma bitməyib. |
| WalletBalance | Pul kisəsinin balansı sıfır deyil. |
| UnresolvedOrder | Tamamlanmamış və ya əlaqəsi yoxlanmalı sifariş var. |

Əvvəllər səhvən bağlanmış, amma borcu qalmış masa da silməni bloklayır.
Şübhəli və ya uyğunsuz maliyyə məlumatı sıfır borc kimi qəbul edilmir.
Digər müştərinin əməliyyatları bu qərara daxil edilmir.

## Nə silinir, nə saxlanılır?

Bu funksiya hesabın bağlanması və **profilin anonimləşdirilməsidir**, bütün tarixi məlumatların fiziki silinməsi deyil.

- Giriş bağlanır, ad və əlaqə məlumatları sintetik dəyərlərlə əvəz edilir, şifrə təmizlənir.
- Sessiya versiyası artırılır, bütün aktiv refresh tokenləri ləğv olunur.
- İstifadə olunmamış e-poçt dəyişmə, şifrə bərpa və şifrə qurma tokenləri bağlanır.
- Push cihazları deaktiv edilir və qorunan push tokeni təmizlənir.
- Profil şəkli əlaqədən çıxarılır və anonim açıq yükləmə üçün əlçatmaz edilir; fiziki fayl mövcud orphan təmizləmə axınına qalır.
- İstifadəçi sətri `IsDeleted=true` edilmir: hesabatlardakı məcburi istifadəçi əlaqələri bununla itmir.
- Sifariş, ödəniş, rezervasiya, audit və maliyyə tarixçəsi saxlanılır. Onların əvvəlki mətn/snapshot sahələri toplu redaktə edilmir.
- Əvvəlki e-poçtla yeni qeydiyyat yeni istifadəçi identifikatoru yaradır; köhnə tarixçəni götürmür.

## Təhlükəsizlik və paralel sorğular

Silinən istifadəçi ID-si sorğu gövdəsindən alınmır, təsdiqlənmiş girişdən alınır.
Şifrə və təsdiq serverdə yoxlanılır. Səhv şifrə mövcud uğursuz giriş/hesab kilidi mexanizminə yazılır.
POST üçün istifadəçi üzrə standart limit 15 dəqiqədə 3 sorğudur; mövcud konfiqurasiya ilə tənzimlənir.
İlkin GET yoxlaması mövcud PublicRead limiti ilə qorunur.

Hesab bağlanması bir DB transaction daxilində istifadəçi sətrini kilidləyir.
Yeni rezervasiya da həmin user kilidini alır: rezervasiya əvvəl yaranıbsa silmə bloklanır;
silmə əvvəl bitibsə gecikmiş rezervasiya sorğusu rədd edilir.
İşçi profilinin aktivləşdirilməsi, e-poçt/şifrə və refresh əməliyyatlarının mövcud user kilidi saxlanılır.
Provayder əməliyyatları da user-sonra-masa kilid sırasına uyğunlaşdırılıb.
Restoranın səlahiyyətli rəhbəri deaktiv müştərinin köhnə ödənişlərini idarə edə bilir;
buradakı daxili `requireActiveCustomer=false` seçimi restoran aidiyyəti və rəhbərin canlı icazə yoxlamalarını keçmir.
İki paralel silmə yalnız bir audit və bir bağlanma yaradır.
Yazı uğursuz olsa, hesab və token dəyişiklikləri geri alınır.

Redis təhlükəsizlik qərarının mənbəyi deyil; mövcud canlı DB sessiya yoxlaması saxlanılır.
Commit-dən sonrakı realtime çatdırılma xətası artıq bağlanmış hesabı uğursuz silmə kimi göstərmir.
Şifrə API cavabına və auditə yazılmır. Yeni sorğu DTO-su şifrəni avtomatik `ToString` edən record deyil.

## API

- `GET /api/v1/profile/account-deletion`: `{ canDelete, blockers }` məlumatını mövcud cavab formatında qaytarır.
- `POST /api/v1/profile/account-deletion`: `{ currentPassword, confirm: true }`; uğur `204 No Content`.
- Uyğun olmayan rol `403 AccountDeletionCustomerOnly`; maneə `409 AccountDeletionBlocked`.
- Köhnə/bağlanmış cihaz `401 SessionInvalid`; səhv şifrə və limitlər mövcud təhlükəsizlik xəta müqavilələrini saxlayır.
- Route, DI, audit action və AZ/EN/RU mətnləri mərkəzi kataloqlara əlavə edilib.
- DB model dəyişikliyi və yeni migration tələb olunmur.

## Yoxlama

23 noyabr 2026 tarixində yekun nəticələr:

- .NET 10.0.404 build: 0 xəta, 0 xəbərdarlıq.
- Backend ümumi dəsti: **1845 keçdi, 0 uğursuz, 0 ötürülmüş test**.
- Yeni funksiyaya aid 33 yoxlama həmin ümumi nəticəyə daxildir: şifrə/rol/sessiya,
  8 maneə, geri ödəniş hüququ, 50-dən artıq tarixçə sətrinin səhifələnməsi,
  bütün cihazlar və bərpa linkləri, köhnə şəkil tokeninin bağlanması,
  16 paralel silmə, rezervasiya/profil aktivləşməsi ilə hər iki yarış sırası,
  rollback, səhv şifrə sayğacı və restoranın tarixi maliyyə nəzarəti.
- Testlər ayrıca localhost PostgreSQL 17 və Redis 7 ACL konteynerlərində işləyib.
  Mövcud Redis dayanma/bərpa, sessiya, icazə, rezervasiya, sifariş, stok,
  ödəniş, bildiriş və hesabat regresiyaları da ümumi dəstdə keçib.
- Frontend: **214 ssenari və build keçdi**; yeni axına aid 51 ssenari daxildir.
  HTTP/WebSocket mock-ları istifadə edilib, canlı hesab silinməyib.
- AZ/EN/RU üçün 29 yeni UI açarı backend kataloqu ilə eynidir.
  Mobil/masaüstü, işıqlı/qaranlıq və 3 dil üzrə 39 ekran şəkli yoxlanılıb.
- `git diff --check` keçdi; əvvəlki əlaqəsiz 4 faylın hash-ləri dəyişməyib.

Bu nəticələr canlı deploy və ya tam brauzer-canlı API end-to-end sınağı deyil:
frontend mock-larla, backend isə gerçək lokal DB və ayrılmış xarici xidmətlərlə yoxlanılıb.

Deploydan sonrakı qəbul yoxlamasını yalnız ayrıca test müştərisi ilə aparın:

1. İşçi və admin profilində müştərinin hesab silmə axınının açılmadığını yoxlayın.
2. Aktiv rezervasiya/açıq masa ilə maneənin aydın göstərildiyini yoxlayın.
3. Yanlış şifrə və təsdiqsiz sorğu hesabı bağlamamalıdır.
4. Borcsuz test müştərisi iki cihazda açıq ikən silinsin; hər iki cihazdan çıxmalıdır.
5. Köhnə şifrə və refresh sessiyası ilə giriş alınmamalıdır.
6. Sahibkar hesabatında əvvəlki ödəniş və satış məbləğləri dəyişməməlidir.
7. Adi müştərinin qeydiyyat, giriş, rezervasiya və sifariş axınları işləməlidir.

Canlı serverdə test hesabı belə avtomatik silinməyib. Commit, push və deploy edilməyib.
