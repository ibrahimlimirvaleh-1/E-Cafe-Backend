# Giriş üçün aktiv işçi profili tələbi

## Biznes Qaydası

- Hesabın saxlanmış rolu sahibkar, menecer, ofisiant və ya mətbəxdirsə, ən azı bir aktiv işçi profili tələb olunur.
- Başqa aktiv işçi profili qalanda yalnız seçilmiş restoran profili deaktiv edilir; digər restoran və cihaz sessiyaları saxlanır.
- Son aktiv işçi profili bağlananda bütün cihaz sessiyaları ləğv edilir. İşçi müştəri roluna keçirilmir; təkrar login və refresh rədd edilir.
- Adi müştəri hesabına restoran/işçi profili tələbi qoyulmur. Müştərinin əlavə işçi profili bağlansa belə onun həqiqi müştəri girişi saxlanır.
- Qlobal administrator restoran profili olmadan işləyə bilir. Qlobal hesab bloklaması ayrıca qoruma olaraq qalır.

`User.IsActive` və saxlanmış hesab rolu dəyişdirilmir. Bu səbəbdən işçinin yenidən işə qəbulu ümumi hesab blokunun açılması ilə qarışdırılmır. Profil aktivləşəndən sonra təzə giriş tələb olunur; köhnə sessiyalar dirildilmir.

## Backend

1. Login əvvəlcə şifrəni və qlobal hesab statusunu yoxlayır. Profilsiz işçi üçün yalnız düzgün şifrə yoxlandıqdan sonra `401 StaffProfileInactive (1141)` qaytarılır və uğursuz giriş qeydə alınır.
2. Refresh və JWT yaratma da eyni aktiv işçi profili tələbini yoxlayır. Həqiqi müştərinin boş profil siyahısı normal qalır.
3. Hər autentifikasiya olunmuş sorğuda sessiya statusu bazadan oxunur: qlobal aktivlik, versiya və tələb olunan işçi profilinin mövcudluğu bir SQL projection ilə gətirilir. Əvvəlki müştəri-fallback JWT-si və köhnə Redis icazəsi bu yoxlamanı keçə bilməz.
4. Ayrı deaktiv düyməsi və personal redaktəsinin deaktiv statusu eyni xidmət qaydasını istifadə edir.
5. İstifadəçi sətrinin transaction kilidi altında digər aktiv işçi profilləri yoxlanılır. Baza açarı `UserId + RestaurantId` olduğuna görə başqa profili ayırmaq üçün miras `Id` sahəsi istifadə edilmir.
6. Son profil bağlananda `SessionVersion` artırılır və bütün aktiv refresh tokenləri həmin transaction-da ləğv edilir. Şifrə, sessiya ailəsi və reuse qoruması dəyişdirilmir.
7. Commit-dən sonra sessiya və həmin restoran aidiyyətinin köhnə keş açarları təmizlənir. `RestaurantAccessChanged` hadisəsində son profil üçün `staff_access_revoked`, digər profil dəyişikliyi üçün əvvəlki `staff_deactivated` / `staff_activated` səbəbləri göndərilir.
8. Sıfır aktiv profildən ilk profil bərpa ediləndə də əvvəlki tokenlər ləğv edilir. Bu, əvvəlki deploydan qalan müştəri-fallback sessiyasının yenidən aktivləşməsinin qarşısını alır.
9. Mobil tətbiqə müştəri girişi və müştəri mətbəx/push bildirişləri yalnız həqiqi aktiv müştəri hesabları üçün qalır. Profilsiz işçi bu hüquqları əldə etmir; obyekt aidiyyəti, aktiv cihaz sessiyası və işçi push icazələri qorunur.

## Frontend Müqaviləsi

- `StaffProfileInactive` adı və ya `1141` kodu terminal giriş rəddidir. Bu cavabda refresh/replay dövrəsi yaradılmamalıdır.
- Login ekranında səbəb göstərilir: “Aktiv işçi profiliniz yoxdur. Giriş üçün restoran məsulu ilə əlaqə saxlayın.”
- Son profil hadisəsi və ya focus/poll/reconnect yoxlaması cari sessiyanın server cavabı ilə uzlaşdırılır. Gecikmiş köhnə hadisə, köhnə promise və ya refresh cavabı sonradan açılmış sağlam sessiyanı silməməlidir.
- Server səhvən `200 + boş profillər` ilə saxlanmış işçi rolunu qaytarsa belə frontend müştəriyə avtomatik keçirmir.
- Həqiqi `Customer` və `SuperAdmin` üçün boş profil siyahısı girişdən çıxış səbəbi deyil.
- Müqavilə və rol hadisələri üçün mövcud məlumat yenilənməsi, profil seçimi, auth revision və tenant məlumatlarının ayrılması qorunur.

## Test Əhatəsi

- İki işçi profili: birinin bağlanması digər tenantın icazəsini və hər iki cihaz sessiyasını saxlayır.
- Son profil: ayrıca düymə və redaktə forması bütün cihazları bağlayır; login, refresh və profil/API sorğusu rədd edilir.
- Əvvəlki deploydan qalmış `Customer` claim-li işçi tokeni həqiqi DB rolu və profil vəziyyəti ilə rədd edilir.
- Login-də yanlış şifrə əvvəlki `InvalidCredentials` cavabını saxlayır; profilsiz işçi üçün uğurlu giriş/token yazısı yaranmır.
- Adi müştəri profilsiz login, refresh, profil və öz bildiriş axınını saxlayır.
- İlk profilin bərpasından sonra köhnə tokenlər bağlı qalır, yalnız təzə login işləyir.
- Son profil/refresh və iki profilin paralel deaktiv edilməsi real PostgreSQL user kilidi altında hər iki ardıcıllıqda yoxlanılır.
- Rollback, tenant sərhədi, öz hesabını deaktiv etmə və qlobal hesab bloklaması qorumaları saxlanır.
- Redis dayananda və bərpa olunanda köhnə “icazə var” məlumatı yeni profilsiz işçi vəziyyətini gizlətmir.
- Mobil qeydiyyat, mətbəx və push testləri adi müştəri ilə profilsiz işçini ayrı yoxlayır.

## Yoxlama Nəticələri

- Davam icrasında 202/202 fokus testi keçib; əvvəl dayandırılmış icradakı dörd uğursuzluq aradan qaldırılıb.
- Geniş icrada 1 784 testdən 1 783-ü keçib, skip yoxdur. Yeganə uğursuzluq `TranslationCatalogTests.NotificationsUseRecipientLanguageAndReaderCanChangeLanguageWithoutChangingArguments` daxilində `RegexMatchTimeoutException` olub; giriş/profil qaydası testləri keçib.
- Həmin tərcümə sinfi ayrıca təkrar işlədilib: 34/34 keçib, skip yoxdur. Mövcud regex vaxt limiti və tərcümə kodu dəyişdirilməyib. Geniş icranın ilk cəhddə tam yaşıl olduğu iddia edilmir; fasiləli timing riski ayrıca saxlanır.
- Frontend: 46/46 brauzer ssenarisi, 36/36 auth yarışı, 20/20 profil qaydası, 22/22 e-poçt ssenarisi keçib. TypeScript/Vite build, 2 386 AZ/EN/RU açar uyğunluğu və əlaqəli lokalizasiya yoxlamaları uğurludur.
- Desktop AZ/light və 320px RU/dark giriş ekranlarında səbəb formanın içindədir; mətn, düymə və xəbərdarlıq üst-üstə düşmür. Brauzer testləri həqiqi SignalR client-i ilə saxta HTTP/WebSocket istifadə edir; canlı E2E deyil.
- Backenddə real loopback PostgreSQL, Redis ACL və Redis stop/restart ssenariləri istifadə olunub. Gerçək telefona push, canlı SMTP və production məlumatına mutasiya edilməyib.
- Hər iki repoda `git diff --check` uğurludur. Əlaqəsiz dörd backend faylının SHA-256 məzmunu qorunub. Heç nə stage/commit/push edilməyib.

## Deploydan Sonra

1. Eyni işçi üçün A və B restoranlarını iki ayrı browser/cihazda açın. A profilini bağlayın: B işləməli, ümumi logout olmamalıdır.
2. Son B profilini də bağlayın: hər iki browser giriş ekranına keçməli və səbəbi göstərməlidir.
3. Həmin işçinin doğru şifrəsi ilə yenidən girişi sınayın: aktiv profil olmadığına görə rədd edilməlidir.
4. Köhnə access və refresh tokenləri ilə qorunan sorğunu sınayın: yeni icazə və sessiya alınmamalıdır.
5. Bir işçi profilini aktiv edin. Köhnə browser/refresh sessiyası dirilməməli; təzə login həmin restorana giriş verməlidir.
6. Heç vaxt işçi olmayan adi müştəri ilə giriş, rezervasiya/sifariş və öz bildirişlərini yoxlayın: profilsiz olması maneə yaratmamalıdır.
7. Gecikmiş hadisə və bağlantı bərpasını test mühitində sınayın. Köhnə hadisə sonradan açılmış sağlam sessiyanı bağlamamalıdır.

## Əhatə Və Məhdudiyyətlər

Yeni migration tələb edilmir. Əvvəl qlobal `User.IsActive=false` olmuş hesablar avtomatik açılmır. Canlı serverdə dəyişiklik və real telefona bildiriş göndərilməsi bu işə daxil deyil. Testlər ayrıca loopback PostgreSQL/Redis və saxta push/HTTP/WebSocket üzərində aparılır.

Əvvəlki əlaqəsiz sənədlər və `IUserRestaurantAccessCache.cs` rəyindəki boşluq olduğu kimi qorunur. Commit, push, merge, deploy və APK buraxılışı edilməyəcək.
