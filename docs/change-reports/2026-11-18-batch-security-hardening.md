 # Toplu təhlükəsizlik yoxlaması

Tarix: 18.11.2026. Branch: `security/batch-hardening`.

## Əhatə və sərhədlər

Backendin birbaşa/dolayı NuGet paketləri, autentifikasiya və sessiyalar,
restoran üzrə personal idarəetməsi, fayl bağlama/yükləmə icazələri,
müqavilə sənədləri, CI/deploy ardıcıllığı yoxlanıldı. Rezervasiya, sifariş,
stok, ödəniş və bildirişlər mövcud regresiya testləri ilə təkrar yoxlanılır.
Bu iş tam pentest və ya bütün risklərin aradan qalxmasına zəmanət deyil.

Canlı serverdə dəyişiklik, commit və push edilməyib. Frontend dəyişməyib.
Əvvəlki iki `appsettings` faylı bayt səviyyəsində qorunub; əvvəlki
`tests/ECafe.Tests/TestResults/` faylları işə daxil edilməyib.
Yeni nəticələr ayrıca lokal `batch-security-results` qovluğuna yazılır.
DB miqrasiyası və ya yeni məxfi konfiqurasiya tələb olunmur.

## Aradan qaldırılan əsas risklər

| Sahə | Əvvəlki risk | Düzəliş |
| --- | --- | --- |
| Personal yaratma: kritik | Menecer/sahibkar API sorğusunda platforma admini rolunu verə bilərdi. | Yüksək səlahiyyətli rollar bütün əlaqəli yazı yollarında ayrıca qorunur. Restoran idarəçisi platforma adminini idarə edə bilməz. |
| Ortaq hesab: yüksək | Bir restoranın meneceri başqa restorana da çıxışı olan əməkdaşın bərpa e-poçtunu dəyişə və ya hesabını bütün restoranlarda deaktiv edə bilərdi. | Restoran personalı redaktəsində qlobal e-poçtu yalnız platforma admini dəyişir. Eyni e-poçtla normal redaktə qalır. Digər aktiv restoran aidiyyəti varsa hesab qlobal deaktiv edilmir. |
| Sessiya yarışı: yüksək | Refresh ilə şifrə bərpası/çıxış eyni anda olduqda yeni token ləğvdən yayına bilərdi. | Giriş, refresh, çıxış, cihaz ləğvi, şifrə təyini/bərpası və personalın təhlükəsizlik dəyişiklikləri eyni istifadəçi DB kilidi ilə ardıcıllaşdırılır. Ləğv commit-dən sonra keşdən təmizlənir. |
| Fayl bağlama: yüksək | Başqasının sərbəst faylını öz profilinə/menyuya/restorana bağlamaq, məxfi sənədi şəkil tipinə çevirmək mümkün idi. | Yükləyici aidiyyəti, mənbə tipi və şəkil uzantısı ortaq policy ilə yoxlanır. Admin də sənədi şəkil kimi ictimailəşdirə bilməz. |
| Müqavilələr: orta | Restorana aid olmaq müqavilə faylını görmək üçün kifayət edirdi. | Download, view, metadata və delete yollarında restoran sərhədinə əlavə olaraq mərkəzi `ViewRestaurantContracts` icazəsi yoxlanır. İcazə sorğunun başqa restoranına deyil, məhz faylın restoranına görə hesablanır. |
| CI nəzarəti | Build keçsə də testlər işləmədən deploy başlaya bilərdi. | NuGet audit, Release build və testlər deployun məcburi asılılığıdır. Adi manual dispatch başqa branch-dən dev deployunu başlatmır. Checkout yazı credential-ı saxlamır. |

Şifrə bərpa və ilkin təyin linkləri paralel sorğuda yalnız bir dəfə istifadə
olunur. İlkin təyin linki artıq müəyyən edilmiş şifrəni dəyişə bilməz.
Veb çıxışı da rotasiya olunmuş tokenin bütün cihaz sessiyasını ləğv edir.
Rol dəyişmədən şəxsin özünə token qaytarılan köməkçi yol da kilid altında
işləyir və köhnə/ləğv edilmiş sessiyadan yeni token vermir.

Fayl qaydası anonim qeydiyyata sərbəst `FileId` daxil edib başqasının faylını
götürməyə icazə vermir. Normal qeydiyyat dəyişmir; şəxsi şəkil autentifikasiya
olunduqdan sonra yüklənib profilə bağlana bilər. Adminin digər yükləyicinin
uyğun şəklini idarə etməsi saxlanıb; sənəd və format qoruması istisna edilmir.

## Avtomatik yoxlamalar

- NuGet audit bütün yeddi layihənin birbaşa və dolayı asılılıqlarında məlum zəif paket göstərmədi. Bu nəticə yoxlama anındakı advisory məlumatlarına aiddir.
- CI-də `NuGetAuditMode=all`; auditə çata bilməmək və NU1901-NU1904 boşluq xəbərdarlıqları build-i dayandırır. Boşluqlar gizlədilməyib və audit söndürülməyib.
- Release solution build: 0 xəbərdarlıq, 0 xəta.
- Yekun Release tam dəsti: **1678 keçdi, 0 xəta, 0 buraxılmış test** (5 dəqiqə 3 saniyə). Redis ACL-in beş testi və autentifikasiyalı SignalR backplane bərpa testi də daxildir. Əsas nəticə faylı: `batch-security-results/batch-release-full.trx` (repository xaricindəki lokal iş qovluğunda).
- Real PostgreSQL-də refresh/bərpa hər iki ardıcıllıqla, köhnə şifrə ilə paralel giriş, veb çıxışı, bütün cihazlardan çıxış, bir cihazın ləğvi, bərpa/təyin linkinin iki dəfə istifadəsi test olunur.
- Personalın rol yüksəltməsi, e-poçt yönləndirməsi, bir/çox restoranlı deaktivasiya və özünə aid şəkli normal bağlama yoxlanır.
- Real mərkəzi authorization handler ilə menecer/sahibkarın müqaviləni görməsi, ofisiant/mətbəxin görməməsi, başqa restoranın icazəsindən istifadənin rəddi yoxlanır.
- Ayrı PostgreSQL və Redis konteynerləri yalnız loopback portlarda istifadə olunur. Redis dayanma/bərpa testi yalnız özünün yaratdığı konteyneri dayandırır.
- Redis ACL generatorunun iki Python testi və workflow YAML/deploy asılılığının struktur yoxlaması keçir.
- GitHub Actions uzaqda işə salınmayıb: bunun üçün push/PR lazımdır. Lokal ekvivalent əmrlər yoxlanılıb.

NuGet nəzarətinin rəsmi izahı: [Microsoft NuGet Audit](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages).

## Qalan risklər və növbəti işlər

1. **Platforma dəstəyi:** layihə `net9.0` üzərində qalır. Microsoft .NET 9 dəstəyini 10.11.2026-da bitirib. .NET 10 LTS-ə keçid ayrıca prioritetli işdir: SDK/runtime Docker image-ləri, CI, migrator, paket uyğunluğu və tam regresiya birlikdə yoxlanmalıdır. Bu patch həmin geniş miqrasiyanı səssizcə etmir. Mənbə: [.NET dəstək cədvəli](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
2. **Paylanmış keşin köhnəlməsi:** DB-də daimi refresh tokenin ləğvdən yayınması bağlanıb. Amma keş silinməsi uğursuz olduqda və ya köhnə oxunuşla yeni yazı yarışdıqda qısa müddət köhnə icazə/sessiya vəziyyəti TTL bitənədək qala bilər. Bu, tokenin daimi sağ qalması ilə eyni deyil; ayrıca keş tutarlılığı işi tələb edir.
3. **GitHub/server ayarları:** environment branch restriction, qorunan branch-lər, SSH host fingerprint, real ehtiyat nüsxələr və server şəbəkəsi bu işdə dəyişməyib/yoxlanmayıb. Workflow-dakı branch şərti repository yazı icazəli şəxsin workflow-nu dəyişməsinin əvəzi deyil.
4. **Öz hesabında həssas dəyişikliklər:** istifadəçinin öz e-poçt dəyişməsinə yenidən autentifikasiya/e-poçt təsdiqi siyasəti ayrıca işdir. Bu patch restoran menecerinin başqa şəxsin qlobal bərpa ünvanını dəyişməsini bağlayır.

## Deploydan sonra birləşdirilmiş test siyahısı

Test hesabları və test restoranından istifadə edin. Paralel API hücum sınaqları
avtomatik testlərdədir; bunları canlı sistemdə təkrarlamaq lazım deyil.

1. Yeni müştəri qeydiyyatı, giriş və normal çıxış edin. Personal üçün ilkin şifrə e-poçtunu açıb parol təyin edin; eyni link ikinci dəfə işləməməlidir.
2. Menecerlə normal ofisiant yaradın/redaktə edin. Qorunan platforma admini/sahibkar rolu restoran personal idarəçisinə verilməməlidir. E-poçtu dəyişmədən normal redaktə işləməli, başqasının bərpa e-poçtunu dəyişmək rədd olunmalıdır.
3. İki restoranda işləyən test əməkdaşını yalnız birində deaktiv edin. Həmin aidiyyət bağlanmalı, digər restorana yenidən giriş qalmalıdır. Son aktiv aidiyyət bağlananda hesab deaktiv olmalıdır.
4. Eyni test hesabı ilə iki brauzerdə daxil olun. Bir cihazdan çıxış digərini bağlamamalıdır. Bütün cihazlardan çıxış və şifrə bərpası isə hər ikisinin köhnə sessiyasını yararsız etməlidir. Yeni şifrə ilə giriş işləməlidir.
5. Profil, menyu və restoran üçün öz normal JPG/PNG şəklinizi yükləyib saxlayın; şəkil açılmalıdır. PDF/TIFF kimi uyğun olmayan faylın şəkil yerinə verilməsi rədd edilməlidir.
6. Müvafiq müqavilə baxış icazəsi olan sahibkar/menecerlə sənədi açıb endirin. Bu icazəsi olmayan ofisiant/mətbəx həmin faylın birbaşa API ünvanını əldə etsə belə aça bilməməlidir. Başqa restoranın istifadəçisi də görməməlidir.
7. Bir tam biznes axını keçin: rezervasiya, əyləşmə, müştəri sifarişi, ofisiant təsdiqi, mətbəx, təqdim, test ödənişi, sessiya bağlanması. Stok düzgün azalmalı; borc varkən masa bağlanmamalıdır. Ləğv və qismən stok qaytarılmasını bir test sifarişində yoxlayın.
8. Müştəri/ofisiant/mətbəx bildirişini və internet kəsilib gələndə yenilənməni yoxlayın. RedisInsight yalnız baxış hesabı ilə oxumalı, yazma/silmə rədd olunmalıdır. Redis-i dayandırma sınağını canlı serverdə etməyin; yalnız ayrıca test mühitində icazə və sessiya qorumalarının qalmasını yoxlayın.
9. Bir şifrə bərpa e-poçtunun, mövcud PDF ixracının və hesabat/CSV-nin normal işlədiyini yoxlayın. Bu mərhələdə real kart ödənişi aparmayın.
