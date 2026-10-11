# Kod yazma standartları

## Hər dəyişiklikdən əvvəl

- Hər dəyişiklikdən əvvəl bu `AGENTS.md` faylını oxu və həmin işə aid qaydaları nəzərə al. Əvvəlki chat yaddaşına və ya köhnə fayl nüsxəsinə arxalanma.
- Bu tələb yeni funksionallıq, bug fix, refactoring, test, konfiqurasiya və sənədləşdirmə dəyişikliklərinə də aiddir. İşi yekunlaşdırmazdan əvvəl nəticənin bu standartlara uyğunluğunu yoxla.

## Git iş axını və istifadəçi icazəsi

- Hər yeni işə başlamazdan əvvəl cari branch-i, staged/unstaged dəyişiklikləri, untracked faylları və push olunmamış commit-ləri yoxla. Təkcə `git status`-un təmiz olması kifayət deyil; branch-in remote/upstream vəziyyətini də yoxla.
- Əvvəlki commit olunmamış dəyişiklik və ya push olunmamış commit varsa, istifadəçiyə onların hansı branch və fayllarda olduğunu bildir və necə davam ediləcəyini soruş. Onları özbaşına commit, push, stash, silmək, geri qaytarmaq və ya başqa branch-ə daşımaq olmaz.
- İş ağacı təmizdirsə və push olunmamış commit yoxdursa, `dev` branch-inə keç, `origin/dev` dəyişikliklərini `git pull --ff-only origin dev` ilə al və yenilənmiş `dev` üzərindən işə uyğun yeni branch aç. Yeni funksionallığı və düzəlişi həmin branch-də et; köhnə iş branch-ini təkrar istifadə etmə.
- Pull zamanı konflikt, branch fərqlənməsi və ya remote vəziyyətini yoxlamaq problemi yaranarsa, avtomatik merge/rebase/reset etmə. Problemi istifadəçiyə bildir.
- İstifadəçinin açıq icazəsi olmadan commit və push etmə. Kod dəyişdirmək icazəsi commit və ya push icazəsi sayılmır. Commit/push icazəsi yalnız istifadəçinin istədiyi işə aiddir, sonrakı işlərə avtomatik keçmir.
- İstifadəçinin əvvəlki dəyişikliklərini qoru. `reset --hard`, məcburi push və dəyişiklikləri itirən əməliyyatları açıq icazəsiz istifadə etmə.
- Backend dəyişikliklərini `C:\Users\mirvaleh.ibrahimli\Desktop\E-Cafe-Backend` daxilində et. Frontend işi tələb olunarsa, istifadəçinin seçdiyi frontend chatında icra olunmasını koordinasiya et; burada frontend fayllarını dəyişmə.

## SOLID və təmiz kod

- Hər yeni dəyişiklikdə SOLID prinsiplərinə riayət et: vahid məsuliyyət, mövcud davranışı pozmadan genişləndirmə, əvəzedilmə, məqsədə uyğun kiçik interfeyslər və asılılıqların abstraksiyalar üzərindən idarə edilməsi.
- Mövcud layihənin qatlarını, məsuliyyət sərhədlərini, dependency injection və repository/service qaydalarını qoru. Əvvəlcə əlaqəli kodu oxu, sonra dəyişiklik et.
- Aydın adlar, kiçik və məqsədli metodlar, oxunaqlı nəzarət axını və lazımsız dərin şərtləri azaldan yanaşmalar istifadə et. Davranışı sadələşdirərkən biznes qaydalarını itirmə.
- Kod təkrarlarının qarşısını al; mövcud ortaq metod, policy və komponentlərdən istifadə et. Yalnız real təkrarı və mürəkkəbliyi azaldan abstraksiya və pattern əlavə et; sadə məsələ üçün artıq arxitektura qurma.
- Biznes mesajlarını, route-ları və dəyişən konfiqurasiya dəyərlərini layihənin mövcud mərkəzləşdirilmiş mexanizmlərində saxla; eyni literalı müxtəlif yerlərdə təkrarlama. Təbii sabitləri süni şəkildə dinamikləşdirmə.
- Refactoring-i istənilən işin əhatəsində saxla; əlaqəsiz kodu və metadata-nı dəyişmə.

## Kod təhlükəsizliyi

- Giriş məlumatlarını serverdə yoxla; frontend yoxlanışına etibar etmə. Hər əməliyyatda istifadəçi səlahiyyətini, restoran aidiyyətini və obyektə giriş hüququnu qoru.
- Məxfi açarları və şəxsi/ödəniş məlumatlarını kodda, loglarda, xəta mesajlarında və icazəsiz API cavablarında göstərmə. Layihənin secret və şifrələmə mexanizmlərindən istifadə et.
- Parametrli sorğular və təhlükəsiz fayl/xarici servis inteqrasiyaları istifadə et. Lazımi timeout, cancellation və resurs məhdudiyyətlərini nəzərə al.
- Paralel əməliyyatlarda yarış vəziyyətlərini, transaction sərhədlərini və idempotentliyi yoxla. Biznes məlumatlarının bütövlüyünü optimizasiya naminə zəiflətmə.

## Optimizasiya və yoxlama

- Lazımsız bazaya/xarici servisə sorğuları, N+1 problemlərini və artıq məlumat yükləməsini azalt. Uyğun projection, pagination və yalnız oxuma sorğularında tracking-siz yanaşmadan istifadə et.
- Optimizasiyanı ölçülən və ya koddan aydın görünən problemə əsaslandır. Cache tətbiq edərkən restoran/istifadəçi sərhədlərini və məlumatın yenilənməsini qoru.
- Dəyişiklikdən sonra əlaqəli build və testləri icra et. Riskə uyğun olaraq biznes sərhədləri, səlahiyyətlər, xəta halları və paralel əməliyyatlar üçün test əlavə et.
- Nəyin dəyişdiyini, yoxlamaların nəticəsini və yoxlanıla bilməyən hissələri istifadəçiyə bildir. Test və yoxlama keçməyibsə, keçmiş kimi təqdim etmə.

## Private və köməkçi metodların izahı

- Yeni yazılan və ya dəyişdirilən hər private və köməkçi metodun üzərində Azərbaycan dilində qısa, açıqlayıcı rəy olmalıdır. Bu qayda static, async, extension və lokal köməkçi metodlara da aiddir.
- Sinif səviyyəsindəki metodlarda `/// <summary>` XML rəyi, lokal funksiyalarda isə `//` rəyi istifadə et. Mövcud XML sənədləşdirməni qoruyub yenilə.
- Rəy metodun nə iş gördüyünü və axında hansı məqsədə xidmət etdiyini izah etməlidir. Sadəcə metodun adını təkrarlama və hər kod sətrini ayrıca şərh etmə.
- Əhəmiyyətli olduqda biznes qaydasını, tarix/saat zonasını, bazaya və ya xarici servisə müraciəti, məlumat dəyişikliyini, bildiriş göndərilməsini və kilid/transaction davranışını izah et. Yalnız metodun həqiqətən etdiyi işi yaz.
- Parametr, nəticə və ya xəta davranışı aydın deyilsə, uyğun `param`, `returns` və `exception` XML teqləri ilə izah əlavə et. Sadə imzalar üçün lazımsız teqlər yaratma.
- Metodun davranışı dəyişdikdə rəyini də yenilə; köhnəlmiş izah saxlanılmamalıdır.
- Bu standartı tətbiq etmək üçün əlaqəsiz faylları kütləvi dəyişmə. Mövcud kodun geniş sənədləşdirilməsini yalnız ayrıca istək olduqda et.

Nümunə:

```csharp
/// <summary>
/// Seçilmiş anı restoranın yerli saatına çevirərək rezervasiyaların gün üzrə filtri üçün tarixi müəyyən edir.
/// </summary>
private DateOnly GetReservationDate(DateTimeOffset instant, TimeZoneInfo restaurantTimeZone)
```
