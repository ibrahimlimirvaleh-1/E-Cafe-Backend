# Mərkəzi tərcümə kataloqu

## İş prinsipi

- Dəstəklənən dillər `az`, `en`, `ru`; standart və ehtiyat dil `az`.
- API sorğuları `Accept-Language` başlığından dil seçir. Texniki xəta kodu, status ID-si, status/rol parser dəyərləri, route, icazə və biznes qərarı dəyişmir. Statusun görünən tərcüməsini frontend sabit dəyərdən seçir; lokallaşdırılmış etiketi biznes şərtində müqayisə etmək olmaz.
- `core.translation_catalog` bir aktiv qaralama və reviziya saxlayır. `core.translation_releases` dəyişdirilməz, tam yayımlanmış snapshot-lardır.
- İlk yayımadək build ilə gələn lüğət işləyir. Super administrator redaktorda ilk dəfə dərc etdikdən sonra tam lüğət DB-də saxlanır. Oxuma əməliyyatı DB-yə yazmır.
- Build kataloqu `Services/Localization/Resources` daxilindədir. UI-nin üç JSON lüğəti frontend kataloqu ilə eyni açarlara malik olmalıdır. Yeni mətn üçün sabit açar və üç dil birlikdə əlavə edilir.
- Yeni build açarları köhnə DB yayımına əlavə edilir. Adminin mövcud redaktələri qorunur; parametr forması dəyişmiş köhnə mətn yeni təhlükəsiz build mətni ilə əvəzlənir.
- `Version` qeyri-şəffafdır: yayım ID-si və build məzmun hash-i birlikdə köhnə browser keşini etibarsız edir. Versiyanı rəqəm kimi müqayisə etməyin.

## Keş və Nasazlıq

Backend ayrıca ölçüsü məhdud `MemoryCache` daxilində yalnız bir dəyişdirilməz üçdilli snapshot saxlayır. Soyuq paralel sorğular bir yükləmədə birləşir. TTL 30 saniyədir; yayım commit olandan sonra cari prosesin keşi dərhal silinir. Digər server instansiyaları ən gec növbəti TTL oxunuşunda yenilənir. Redis tələb olunmur.

Browser `localStorage`-da hər dil üçün lüğət və ETag saxlayır. Versiya dəyişməyibsə `304` cavabı tam lüğətin təkrar göndərilməsinin qarşısını alır. Şəbəkə və ya storage problemi zamanı browserdəki/build lüğəti işləyir. Public cavab üçün `max-age=60`, `stale-while-revalidate=3600` tətbiq edilir; CDN/proxy əlavə ediləndə həmin gecikmə də nəzərə alınmalıdır.

DB oxunuşunda bağlantı/timeout/JSON xətası zamanı public lüğət build ehtiyatına qayıdır. Adminin uğursuz yazısı uğurlu sayılmır. API-nin qalan DB asılı funksiyalarının işləməsinə bu fallback zəmanət vermir.

## API

- `GET /api/v1/localization/dictionaries/{language}`: public yayımlanmış lüğət; `If-None-Match` və `304` dəstəyi.
- `GET/PUT /api/v1/localization/preference`: yalnız aktiv istifadəçinin öz dil seçimi; sessiya versiyası dəyişmir.
- `GET/PUT /api/v1/admin/localization/draft`: platforma super administratorunun qaralaması. PUT ən çox 250 açarı bir dəfəyə dəyişir.
- `POST /api/v1/admin/localization/publish`: `{ "expectedRevision": 0 }` ilə cari qaralamanı dərc edir.
- `GET /api/v1/admin/localization/releases`: son 50 yayımın metadata-sı.
- `POST /api/v1/admin/localization/releases/{releaseId}/rollback`: əvvəlki snapshot əsasında YENİ yayım yaradır.

Admin rolu həm token, həm canlı aktiv istifadəçi qeydi ilə yoxlanılır. PG transaction kilidindən sonra rol yenidən yoxlanılır. Köhnə redaktor reviziyası `409 LocalizationDraftConflict` qaytarır. Naməlum açar, boş mətn, HTML, həddən uzun mətn və uyğunsuz parametr dəsti rədd edilir.

## Mətn və Bildiriş Sərhədləri

Restoran/masa/menyu/müştəri adları, qeydlər, səbəblər və yüklənmiş sənədlər avtomatik tərcümə edilmir. PDF müqavilə şablonu və əvvəl göndərilmiş məktub ayrıca sənəddir; tarixçə yenidən yazılmır.

Geri ödəniş cavabında `status` və `statusId` dəyişmir; göstərilən tərcümə `statusLabel` sahəsində gəlir. Tarixçədə `fromStatusId`, `toStatusId`, `fromStatusLabel`, `toStatusLabel` əlavə olunur. `reason` orijinal qalır; yalnız məlum provayder mərhələsinin serverdə yaradılmış səbəbi `reasonLabel` ilə tərcümə edilir. Naməlum/sərbəst səbəb üçün `reasonLabel=null` olduqda orijinal göstərilməlidir. `customerNextStep` və `eligibilityReason` yalnız kataloqda tanınan sistem mətnləri üzrə çevrilir; `cancellationReason` dəyişmir. Dil seçimi dəyişəndə frontend API-dən həmin dilin cavabını yenidən almalıdır; maliyyə əməliyyatını təkrarlamamalıdır. Bu görünüş sahələri üçün migration tələb olunmur.

Kodlu API xətaları kataloqdan gəlir. Köhnə sabit sistem mesajları tam uyğun build aliasları ilə tanınır; mətnin fraqmentləri və istifadəçi məzmunu sərbəst tərcümə edilmir. Naməlum əvvəlki mesajın orijinalı saxlanır. Yeni bildirişlərdə mümkün olduqca sabit açar/parametr kataloq sətri istifadə edilməlidir.

Yeni tanınan sistem bildirişi alıcının saxlanmış dilində hazırlanır; e-mail outbox-a bu mətn düşür. Bildiriş açarları və parametrləri saxlanır ki, ekranda sonradan dil dəyişmək mümkün olsun. Əvvəlki açarsız bildirişlər olduğu kimi qalır. Mobil kilid ekranında şəxsi sifariş/depozit məlumatı deyil, alıcının dilində ümumi yeni bildiriş mətni göstərilir. Push icazəsi, restoran scope-u və aktiv cihaz qaydaları dəyişmir. Tarixçədəki texniki status kodları sabitdir.

## Deploy

Migration: `20261018105154_AddTranslationCatalog`. Başqa migration-lar kimi əvvəl ehtiyat nüsxəsi və staging yoxlaması aparılmalıdır. Bu işdə server bazasına migration tətbiq edilməyib.

```powershell
dotnet ef database update --project src/ECafe.Infrastructure --startup-project src/ECafe.Api --context ECafeDbContext
```

Bu əmr yalnız seçilmiş mühitin mövcud təhlükəsiz connection konfiqurasiyası ilə işlədilməlidir; connection string və secret-i terminal tarixçəsinə yazmayın. Migration istifadəçiyə `preferred_language=az`, əvvəlki bildirişlərə `Language=az` verir, bildirişin orijinal mətnini və biznes məlumatlarını dəyişmir.

## Əl İlə Yoxlama

1. Təmiz browserdə AZ açılsın; EN və RU seçimindən sonra refresh-də seçilmiş dil qalsın.
2. Login/logout, müştəri, menecer, ofisiant və mətbəx ekranlarında dili dəyişin. Başlıq, düymə, tarix/say və modal mətnlərini yoxlayın.
3. Restoran, masa, məhsul və müştəri adlarının dəyişmədiyini yoxlayın.
4. Dil seçimini hesabda saxlayın; başqa browserdə həmin hesabla girişdə seçim bərpa olunsun.
5. Eyni uğursuz əməliyyatı üç dildə göndərin: xəta kodu/status eyni, mesaj seçilmiş dildə olsun.
6. Eyni lüğət sorğusunu ETag ilə təkrarlayın: `304`; başqa dil üçün fərqli ETag.
7. Super administrator bir mətni dəyişib yalnız saxlasın: public ekranda əvvəlki yayımlanmış mətn qalsın.
8. Dərc etsin: public lüğətin versiyası dəyişsin və browserin növbəti yeniləməsində yeni mətn görünsün.
9. Köhnə reviziya ilə ikinci tabdan saxlama/yayım `409` qaytarsın; ilk tabın işi itirilməsin.
10. Menecer/müştəri hesabı admin endpointlərini birbaşa çağırdıqda `403` alsın.
11. HTML və silinmiş/dəyişdirilmiş `{parametr}` ilə saxlama rədd edilsin.
12. Əvvəlki yayıma qayıdın: yeni release ID-si yaransın, əvvəlki tarixçə dəyişməsin.
13. Alıcının dili RU, əməliyyatçının dili EN olduqda yeni sistem bildirişi/e-mail RU, mobil ümumi push RU olsun.
14. Şəbəkəni bağlayıb əvvəl yüklənmiş dilə keçin: UI boş qalmasın; reconnect-dən sonra versiya yenidən yoxlanılsın.
15. Rezervasiya-depozit-sifariş-mətbəx-təqdim-hesablaşma-sessiya bağlama və refund axınlarını yenidən keçin: məbləğ, icazə, status keçidi və idempotentlik dəyişməməlidir.
